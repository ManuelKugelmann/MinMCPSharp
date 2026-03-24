# MCPSharp

Lightweight [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server for **.NET** and **Unity**. Attribute-based API, HTTP and stdio transports, minimal dependencies.

## Features

- `[McpTool]` / `[McpResource]` attribute API — mark methods and your server is ready
- **HTTP transport** (Streamable HTTP, MCP 2025-03-26 spec) via `System.Net.HttpListener` — no ASP.NET required
- **Stdio transport** for editor integrations and piped communication
- **Unity-compatible** — netstandard2.0, no IL2CPP-breaking dependencies, main-thread dispatch included
- Dynamic tool registration at runtime
- XML doc comment support for tool/parameter descriptions (via [LoxSmoke.DocXml](https://github.com/loxsmoke/DocXml))
- Complex object parameters with automatic JSON Schema generation

## Dependencies

| Package | Purpose | Runtime deps |
|---|---|---|
| Newtonsoft.Json | JSON serialization | 0 |
| LoxSmoke.DocXml | XML doc comment parsing | 0 |
| PolySharp | C# 13 polyfills | compile-time only |

## Installation

```bash
dotnet add package MCPSharp
```

## Quick Start

### 1. Define Tools

```csharp
using MCPSharp;

public class Calculator
{
    /// <summary>Adds two numbers together</summary>
    /// <param name="a">First number</param>
    /// <param name="b">Second number</param>
    [McpTool]
    public static int Add([McpParameter(true)] int a, [McpParameter(true)] int b)
        => a + b;
}
```

### 2. Start the Server (HTTP)

```csharp
using MCPSharp;

var server = new McpServer("CalculatorServer", "1.0.0");
server.Register<Calculator>();
await server.RunAsync(port: 8080); // POST http://localhost:8080/mcp
```

### 3. Start the Server (Stdio)

```csharp
await server.RunStdioAsync();
```

## Dynamic Tool Registration

```csharp
server.AddTool(new McpTool
{
    Name = "greet",
    Description = "Greets a user",
    InputSchema = new McpInputSchema
    {
        Type = "object",
        Required = ["name"],
        Properties = new Dictionary<string, McpParameterSchema>
        {
            { "name", new McpParameterSchema { Type = "string", Description = "User name" } }
        }
    }
}, (string name) => $"Hello, {name}!");
```

## Unity Integration

MCPSharp includes Unity-specific components behind `#if UNITY_5_3_OR_NEWER`:

```csharp
// Option A: Inspector-driven
// Add McpServerBehaviour to a GameObject, configure in Inspector

// Option B: Code-driven
var behaviour = McpServerBehaviour.Create("MyGame", "1.0.0", port: 8080);
behaviour.Register<MyGameTools>();
behaviour.StartServer();
```

The `McpServerBehaviour` handles lifecycle (Awake/OnDestroy) and optionally dispatches tool calls to Unity's main thread via `MainThreadDispatcher`.

## API Reference

### Attributes

| Attribute | Target | Properties |
|---|---|---|
| `[McpTool]` | Method, Class | `Name`, `Description` |
| `[McpParameter]` | Parameter | `Required`, `Description` |
| `[McpResource]` | Method, Property | `Name`, `Uri`, `MimeType`, `Description` |

### McpServer

```csharp
var server = new McpServer(name, version);
server.Register<T>();                          // Register tool/resource class
server.RegisterAssembly(assembly);             // Scan assembly for attributed types
server.AddTool(tool, delegate);                // Register dynamic tool
server.Start(endpoint, port);                  // Start HTTP (non-blocking)
server.Start(transport, endpoint, port);       // Start with custom transport
await server.RunAsync(port: 8080);             // Start HTTP and block
await server.RunStdioAsync();                  // Start stdio and block
server.Stop();                                 // Stop server
```

### Transports

| Transport | Class | Use Case |
|---|---|---|
| HTTP | `HttpListenerTransport` | Web clients, cross-process, Unity |
| Stdio | `StdioTransport` | Editor integrations, piped I/O |
| Custom | Implement `IMcpTransport` | Your own transport |

## XML Documentation Support

MCPSharp automatically extracts tool and parameter descriptions from XML doc comments. Enable in your `.csproj`:

```xml
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

## Contributing

We welcome contributions! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License.
