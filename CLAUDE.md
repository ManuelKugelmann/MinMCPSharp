# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCPSharp is a lightweight .NET library for implementing MCP (Model Context Protocol) servers. It provides HTTP and stdio transports with an attribute-based API, targeting .NET and Unity.

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

- **McpServer** (`MCPSharp/Core/McpServer.cs`) — Main server class with builder pattern. Entry points: `Start()`, `RunAsync()`, `RunStdioAsync()`.
- **McpRouter** (`MCPSharp/Core/McpRouter.cs`) — Stateless JSON-RPC 2.0 router. Handles `initialize`, `tools/list`, `tools/call`, `resources/list`, `ping`, etc.
- **ToolManager** (`MCPSharp/Core/ToolManager.cs`) — Scans types for `[McpTool]` methods, extracts parameter schemas via reflection and XML doc comments (LoxSmoke.DocXml).
- **ToolHandler** (`MCPSharp/Core/ToolHandler.cs`) — Invokes tools via reflection, handles async/sync methods and JSON parameter deserialization.
- **ResourceManager** (`MCPSharp/Core/ResourceManager.cs`) — Scans types for `[McpResource]` methods/properties.
- **JsonSchemaGenerator** (`MCPSharp/Core/JsonSchemaGenerator.cs`) — Maps C# types to JSON Schema for tool parameter definitions.

### Transport Layer (`MCPSharp/Transport/`)

- **IMcpTransport** — Transport interface
- **HttpListenerTransport** — HTTP transport using System.Net.HttpListener (no ASP.NET)
- **StdioTransport** — Stdio-based newline-delimited JSON-RPC

### Unity Integration (`MCPSharp/Unity/`)

- **MainThreadDispatcher** — Dispatches work to Unity's main thread
- **McpServerBehaviour** — MonoBehaviour wrapper for McpServer
- Both files are behind `#if UNITY_5_3_OR_NEWER`

### Attribute API

| Attribute | Purpose |
|---|---|
| `[McpTool]` | Marks a class or method as an MCP tool (name, description optional) |
| `[McpParameter]` | Configures method parameters (required, description) |
| `[McpResource]` | Marks properties/methods as MCP resources (name, uri, mimeType, description) |

### Key Dependencies

- **Newtonsoft.Json** — JSON serialization
- **LoxSmoke.DocXml** — XML documentation comment parsing
- **PolySharp** — C# language polyfills (compile-time only)

## Test Framework

Tests use **MSTest** in `MCPSharp.Test/`. Key test file:
- `HttpTransportTests.cs` — HTTP transport integration tests (starts server, sends JSON-RPC via HttpClient)

## Branching

- `master` — production/release
- `development` — active development; PRs target this branch
