# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCPSharp is a .NET library for implementing the Model Context Protocol (MCP). It provides both server and client implementations using JSON-RPC over stdio and SSE transports.

## Build Commands

```bash
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release --verbosity normal

# Run a single test
dotnet test MCPSharp.Test --filter "FullyQualifiedName~TestMethodName" --configuration Release

# Create NuGet package
dotnet pack MCPSharp/MCPSharp.csproj --configuration Release
```

## Architecture

**Multi-targeting:** The library targets netstandard2.0, net8.0, and net9.0 (C# 13.0).

### Core Components

- **MCPServer** (`MCPSharp/Core/MCPServer.cs`) — Singleton server that discovers tools via attributes, manages JSON-RPC communication over stdio, and handles tool registration/invocation. Entry point is `MCPServer.StartAsync()`.
- **MCPClient** (`MCPSharp/Core/MCPClient.cs`) — Spawns a server process and communicates via JSON-RPC. Supports tool listing/calling, resource listing, and exporting tools as `AIFunction` objects (Microsoft.Extensions.AI).
- **ServerRpcTarget** (`MCPSharp/Core/ServerRpcTarget.cs`) — Implements MCP protocol methods (`initialize`, `tools/list`, `tools/call`, `resources/list`, `prompts/list`, `ping`) as `[JsonRpcMethod]` handlers.

### Tool System

- **ToolManager** (`MCPSharp/Core/Tools/ToolManager.cs`) — Scans assemblies for tools using three attribute types: `[McpTool]`, `[McpFunction]` (deprecated), and `[KernelFunction]` (Semantic Kernel). Extracts parameter schemas from method signatures and XML doc comments.
- **ToolHandler** (`MCPSharp/Core/Tools/ToolHandler.cs`) — Invokes tools via reflection, handling async/sync methods and JSON parameter deserialization to proper types.
- **ResourceManager** — Integrated into ToolManager; manages `[McpResource]`-annotated properties/methods.

### Transport Layer (`MCPSharp/Core/Transport/`)

- **DuplexPipe** — Bidirectional pipe using System.IO.Pipelines
- **StdioTransportPipe** — Stdio-based transport for process communication
- **SSETransportPipe** — Server-Sent Events transport for HTTP

### Attribute API

| Attribute | Purpose |
|---|---|
| `[McpTool]` | Marks a class or method as an MCP tool (name, description optional) |
| `[McpParameter]` | Configures method parameters (required, description) |
| `[McpResource]` | Marks properties/methods as MCP resources (name, uri, mimeType, description) |
| `[McpFunction]` | Deprecated — use `[McpTool]` instead |

Semantic Kernel's `[KernelFunction]` and `[Description]` attributes are also auto-discovered.

### Key Dependencies

- **StreamJsonRpc** — JSON-RPC protocol implementation
- **Microsoft.Extensions.AI** — AIFunction abstraction for tool export
- **Microsoft.SemanticKernel.Abstractions** — Semantic Kernel attribute support

## Test Framework

Tests use **MSTest** in `MCPSharp.Test/`. Key test files:
- `STDIOTransportTests.cs` — Tool listing, calling, dynamic tools, exception handling
- `ClientTests.cs` — Client initialization and communication
- `AIFunctionAbstractionTests.cs` — Microsoft.Extensions.AI integration
- `SSETransportTests.cs` — SSE transport testing

## Release Process

Releases are triggered manually via the `release.yml` GitHub Actions workflow with a version number input. The workflow builds, tests, packs, publishes to NuGet, and creates a GitHub release.

## Branching

- `master` — production/release
- `development` — active development; PRs target this branch
