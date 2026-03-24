# SharpMinMCP — Integration & Development Plan

## Origin

Port of [MCPSharp](https://github.com/afrise/MCPSharp) (MIT, Allen Frise) to Unity.
Stripped: StreamJsonRpc, Kestrel, Microsoft.Extensions.AI, SemanticKernel, System.IO.Pipelines.
Added: HttpListener Streamable HTTP transport, stdio transport, main-thread dispatcher, MonoBehaviour lifecycle.
Result: 1522 LOC (1284 plain C#, 238 Unity-guarded), single dep (Newtonsoft.Json).

---

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│ #if UNITY_5_3_OR_NEWER — Unity Integration Layer         │
│                                                          │
│  McpServerBehaviour  ← MonoBehaviour, inspector, lifecycle│
│  MainThreadDispatcher ← ConcurrentQueue + Update()       │
├──────────────────────────────────────────────────────────┤
│ Transport Layer  (plain C#, no Unity deps)               │
│                                                          │
│  IMcpTransport                                           │
│    ├── HttpListenerTransport  (Streamable HTTP)          │
│    ├── StdioTransport         (editor/CLI/testing)       │
│    └── (future) WebSocketTransport                       │
│                                                          │
│  Contract: Func<string, Task<string>> RequestHandler     │
├──────────────────────────────────────────────────────────┤
│ Core Layer  (plain C#, no Unity deps, no transport deps) │
│                                                          │
│  McpServer         ← plain C# entry point, builder       │
│                      RunAsync(), RunStdioAsync()          │
│  McpRouter         ← JSON-RPC 2.0 dispatch               │
│  ToolManager       ← [McpTool] discovery + schema        │
│  ToolHandler       ← reflection invoke + Task unwrap     │
│  ResourceManager   ← [McpResource] discovery             │
│  JsonSchemaGenerator ← C# type → JSON Schema             │
│  XmlDocExtensions  ← XML comment extraction              │
│  McpModels         ← all protocol DTOs                   │
│  Attributes        ← McpTool, McpParameter, McpResource  │
└──────────────────────────────────────────────────────────┘
```

Compilation modes:
- **Plain .NET** (no UNITY define): Core + Transport compile. McpServer.RunAsync() available. Unity/ files excluded.
- **Unity** (UNITY_5_3_OR_NEWER auto-defined): All layers compile. McpServerBehaviour wraps McpServer.

Key invariant: Core/ + Transport/ have zero Unity references. Testable standalone, NuGet-publishable.

---

## Phase 1 — Validate in Unity (Day 1-2)

### 1.1 Import package
- Create test Unity project (2021.3+ LTS or Unity 6)
- Add via Package Manager → git URL or local folder
- Verify: compiles, no errors, Newtonsoft.Json resolves

### 1.2 Create test tools
```csharp
public class TestTools
{
    [McpTool("echo", "Returns input")]
    public static string Echo([McpParameter(true, "Input text")] string input)
        => input;

    [McpTool("add", "Add two numbers")]
    public static float Add(
        [McpParameter(true)] float a,
        [McpParameter(true)] float b) => a + b;

    [McpTool("scene_info", "Current scene name")]
    public static string SceneInfo()
        => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
}
```

### 1.3 Wire up server
- Option A (Unity): Add McpServerBehaviour component to a GameObject, set startOnAwake=true
- Option B (Unity, code-driven):
```csharp
McpServerBehaviour.Create("TestGame", "0.1.0", 8080)
    .RegisterAssembly(typeof(TestTools).Assembly)
    .StartServer();
```
- Option C (Plain C#, HTTP):
```csharp
var server = new McpServer("TestGame", "0.1.0")
    .RegisterAssembly(typeof(TestTools).Assembly)
    .Start("localhost", 8080);
// server.Stop() when done, or use Dispose()
```
- Option D (Plain C#, stdio, blocking):
```csharp
await new McpServer("TestGame", "0.1.0")
    .RegisterAssembly(typeof(TestTools).Assembly)
    .RunStdioAsync(cancellationToken);
```

### 1.4 Test with curl
```bash
# Initialize
curl -X POST http://localhost:8080/ \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'

# List tools
curl -X POST http://localhost:8080/ \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'

# Call tool
curl -X POST http://localhost:8080/ \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"input":"hello"}}}'

# Ping
curl -X POST http://localhost:8080/ \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":4,"method":"ping","params":{}}'
```

### 1.5 Test with Claude Code (HTTP)
```bash
claude mcp add unity-mcp --transport http http://localhost:8080/
```
Verify tool discovery and invocation from Claude.

### 1.6 Test with Claude Code (stdio)
For editor integrations where Claude launches the process directly:
```bash
claude mcp add unity-mcp-stdio -- dotnet run --project /path/to/project
```
Or for testing, pipe JSON-RPC via stdin:
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | dotnet run
```

### 1.7 Test with MCP Inspector
```bash
npx @modelcontextprotocol/inspector http://localhost:8080/
```

---

## Phase 2 — Fix Known Issues (Day 2-3)

### 2.1 HttpListener on macOS/Linux
`HttpListener` requires elevated permissions on non-Windows for non-localhost prefixes.
For localhost (default), it should work. Test on each target.
Fallback: implement `TcpListener`-based transport (~100 LOC more, no permission issues).

### 2.2 Stdio transport in Unity
Unity hijacks stdout for its own logging. `Console.OpenStandardOutput()` bypasses
this but test that it actually works in:
- Unity Editor play mode (unlikely useful — HTTP is better here)
- Standalone builds launched as child process by an MCP client
- `dotnet run` console project using Core layer only (no Unity deps)
If stdout is poisoned by Unity log output, the JSON-RPC stream will be corrupt.
Mitigation: stderr for all logging when stdio transport is active.

### 2.3 IL2CPP compatibility
Reflection-heavy code (ToolManager, ToolHandler) may need:
- `[Preserve]` attributes on tool classes
- link.xml to prevent stripping
- Test: make IL2CPP build, invoke all tools

```xml
<!-- link.xml -->
<linker>
    <assembly fullname="SharpMinMCP" preserve="all"/>
    <assembly fullname="Newtonsoft.Json" preserve="all"/>
</linker>
```

### 2.4 Main thread dispatch correctness
Current `EnqueueAsync<T>(Func<Task<T>>)` has a subtle issue: the inner async
lambda runs on main thread but `await` inside it will resume on main thread
only if there's a SynchronizationContext. Unity has one in editor, may not in
builds. Options:
- Simplify: only support sync tool methods (90% use case)
- Or: use `UniTask` / custom awaiter if async tools needed

### 2.5 Parameter deserialization edge cases
MCPSharp had issues with nested JObject → Dictionary<string,object> conversion.
The `JToken.ToObject<T>()` path should handle most cases but test with:
- Nested objects (class with class properties)
- Arrays of primitives
- Nullable types
- Enums (string → enum)
- Default parameter values

### 2.6 ToolHandler instance creation
Current code: `Activator.CreateInstance(_method.DeclaringType)` for instance methods.
This creates a new instance per invocation. Decide:
- Static methods only (simplest, recommend for v1)
- Singleton instance cache per type
- DI/factory pattern (overkill for games)

---

## Phase 3 — Harden (Day 3-5)

### 3.1 Error handling
- Transport: malformed JSON, oversized bodies, connection drops
- Router: unknown methods, missing params, type mismatches
- ToolHandler: exceptions in user code → clean MCP error response
- Add configurable max request body size (default 1MB)

### 3.2 Logging
- Replace Debug.Log with injectable ILogger or Action<string>
- Log levels: Error, Warn, Info, Debug
- Structured: "[SharpMinMCP] {method} {tool} {elapsed}ms"

### 3.3 Security
Per MCP spec:
- [x] Origin validation (implemented, localhost default)
- [ ] Mcp-Session-Id header support (stateful mode)
- [ ] Optional API key via Authorization header
- [ ] Rate limiting (requests/sec per client)
- [ ] Bind to localhost only by default (currently configurable)

### 3.4 Thread safety
- ToolManager.Tools dictionary: read-heavy, write-rare → ConcurrentDictionary or lock
- ResourceManager.Resources list: same
- MainThreadDispatcher queue: already ConcurrentQueue ✓

### 3.5 Graceful shutdown
- Drain pending requests on Stop()
- Cancel in-flight tool calls via CancellationToken
- HttpListener.Stop() can block — add timeout

---

## Phase 4 — Features (Week 2)

### 4.1 resources/read endpoint
Currently only `resources/list` is implemented. Add:
- Route `resources/read` → find resource by URI → invoke method → return content
- ResourceHandler class (mirror ToolHandler)

### 4.2 Streamable HTTP session support
MCP spec optional features:
- Mcp-Session-Id generation on initialize
- Session state tracking (which tools were listed, etc.)
- DELETE endpoint for session cleanup
- GET endpoint for server-initiated SSE stream (optional)

### 4.3 Batch JSON-RPC
MCP spec allows JSON-RPC batch requests (array of requests).
Currently not handled — add array detection in McpRouter.

### 4.4 WebSocket transport
Alternative IMcpTransport for bidirectional comms:
- Better for high-frequency tool calls
- Unity has built-in WebSocket support
- ~150 LOC implementation
- Enables server-initiated notifications (tools/list_changed)

### 4.5 MCP Client (future slot)
Unity calling external MCP servers (e.g. LLM tool servers, game services).
Separate from server functionality. Would use HttpClient or WebSocket as client
transport. McpRouter already parses the same protocol shapes — share models.
~200 LOC estimated. Only build if a concrete use case emerges.

### 4.6 Tool categories / annotations
Extend McpToolAttribute:
```csharp
[McpTool("spawn_enemy", "Spawn enemy", Category = "Gameplay", Destructive = true)]
```
Surface via tool annotations in MCP spec (2025-03-26 addition).

### 4.7 Hot reload
Detect assembly reload (editor) or explicit trigger:
- Re-scan assemblies for tools
- Send `notifications/tools/list_changed` to connected clients
- Requires persistent SSE/WebSocket connection (not stateless HTTP POST)

---

## Phase 5 — Build & Ship (Week 2-3)

### 5.1 Standalone build testing
Test matrix:
| Platform | Mono | IL2CPP | HttpListener | Notes |
|----------|------|--------|--------------|-------|
| Windows  | ✓    | ✓      | ✓            | Primary target |
| macOS    | ✓    | ✓      | ✓*           | *localhost only without entitlements |
| Linux    | ✓    | ✓      | ✓*           | *localhost only without sudo |
| Android  | —    | ✓      | ?            | Test. May need wifi permissions |
| iOS      | —    | ✓      | ?            | Unlikely use case, low priority |
| WebGL    | —    | —      | ✗            | No socket access. Not supported. |

### 5.2 Package structure for UPM
```
SharpMinMCP/
├── package.json
├── LICENSE.md          ← MIT (from MCPSharp) + your additions
├── README.md
├── CHANGELOG.md
├── Runtime/
│   ├── SharpMinMCP.asmdef
│   ├── Core/
│   ├── Transport/
│   └── Unity/
├── Editor/             ← future: inspector, config window
│   └── SharpMinMCP.Editor.asmdef
├── Samples~/           ← optional example scenes
│   └── BasicSetup/
└── Tests~/
    ├── Runtime/
    └── Editor/
```

### 5.3 CI
- GitHub Actions: build on Unity 2021 LTS + Unity 6
- Run NUnit tests (Core layer, no Unity deps)
- Validate package structure

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| HttpListener needs admin on some platforms | Blocks non-dev users | TcpListener fallback |
| IL2CPP strips reflection targets | Tools silently fail | link.xml + [Preserve] + test matrix |
| Newtonsoft.Json version conflicts | Build errors in projects already using it | Use Unity's bundled package |
| Main thread dispatch latency | 1-frame delay (16ms @60fps) | Acceptable for AI tool calls |
| MCP spec evolves | Breaking changes | Pin to 2025-03-26, update incrementally |
| Large tool responses | Memory pressure | Stream responses (future), size limits |

---

## Non-Goals (v1)

- **MCP Client** — Unity calling external MCP servers. Different axis. Clean
  addition later since McpRouter already parses the same protocol shapes.
  Slot reserved in Phase 4 if needed.
- **SSE transport** — deprecated in MCP spec (2025-03-26). Streamable HTTP only.
- Prompts/sampling support
- OAuth/PKCE auth flow
- Multi-session state management
- Resource templates with URI patterns

---

## File Inventory

| File | LOC | Guard | Origin | Changes |
|------|-----|-------|--------|---------|
| Core/Attributes/McpToolAttribute.cs | 19 | — | MCPSharp | Minor cleanup |
| Core/Attributes/McpParameterAttribute.cs | 18 | — | MCPSharp | STJ attrs removed |
| Core/Attributes/McpResourceAttribute.cs | 23 | — | MCPSharp | Verbatim |
| Core/Model/McpModels.cs | 138 | — | MCPSharp | Consolidated, STJ→Newtonsoft |
| Core/McpServer.cs | 168 | — | New | Plain C# entry point |
| Core/McpRouter.cs | 156 | — | New | Replaces ServerRpcTarget+StreamJsonRpc |
| Core/ToolManager.cs | 96 | — | MCPSharp | Removed SK, AI.Extensions |
| Core/ToolHandler.cs | 119 | — | MCPSharp | JsonElement→JToken |
| Core/ResourceManager.cs | 50 | — | MCPSharp | Verbatim |
| Core/JsonSchemaGenerator.cs | 80 | — | New | Replaces AIJsonUtilities |
| Core/XmlDocExtensions.cs | 70 | — | MCPSharp | Verbatim |
| Transport/IMcpTransport.cs | 24 | — | New | Interface |
| Transport/HttpListenerTransport.cs | 206 | — | New | Replaces Kestrel+SSE |
| Transport/StdioTransport.cs | 111 | — | New | Editor/CLI/testing |
| Unity/McpServerBehaviour.cs | 136 | `#if UNITY` | New | MonoBehaviour wrapper |
| Unity/MainThreadDispatcher.cs | 102 | `#if UNITY` | New | Main-thread queue |
| | | | | |
| **Core+Transport (plain C#)** | **1284** | | | |
| **Unity (guarded)** | **238** | | | |
| **Total** | **1522** | | | |

---

## Quick Start Checklist

```
[ ] Import package into Unity project
[ ] Verify compilation (no errors)
[ ] Create a class with [McpTool] methods
[ ] Add McpServer component to scene (or Create() from code)
[ ] Enter play mode
[ ] curl POST to http://localhost:8080/ with initialize
[ ] curl tools/list — verify tools appear
[ ] curl tools/call — verify tool executes
[ ] Connect Claude Code: claude mcp add unity-mcp --transport http http://localhost:8080/
[ ] Verify Claude discovers and calls tools
[ ] Test in standalone build (Windows first)
```
