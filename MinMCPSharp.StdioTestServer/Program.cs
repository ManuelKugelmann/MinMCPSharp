using MinMCPSharp;

// Minimal stdio MCP server for testing McpClient stdio transport
var server = new McpServer("StdioTestHelper", "1.0.0");
server.Log = (_) => { }; // suppress log output to stdout

server.AddTool(new McpTool
{
    Name = "echo",
    Description = "Echo tool",
    InputSchema = new McpInputSchema
    {
        Type = "object",
        Required = ["message"],
        Properties = new Dictionary<string, McpParameterSchema>
        {
            { "message", new McpParameterSchema { Type = "string", Description = "the message" } }
        }
    }
}, (string message) => message);

server.AddTool(new McpTool
{
    Name = "add",
    Description = "Add two numbers",
    InputSchema = new McpInputSchema
    {
        Type = "object",
        Required = ["a", "b"],
        Properties = new Dictionary<string, McpParameterSchema>
        {
            { "a", new McpParameterSchema { Type = "integer", Description = "first" } },
            { "b", new McpParameterSchema { Type = "integer", Description = "second" } }
        }
    }
}, (int a, int b) => (a + b).ToString());

await server.RunStdioAsync();
