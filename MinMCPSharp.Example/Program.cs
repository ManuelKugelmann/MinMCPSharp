using MinMCPSharp;
using MinMCPSharp.ExternalExample;

var server = new McpServer("ExampleServer", "1.0.0");

server.Register<MinMCPSharp.Example.MCPDev>();
server.Register<ExternalTool>(); // Cross-assembly tool registration

// Dynamic tool
server.AddTool(new McpTool
{
    Name = "dynamicTool",
    Description = "A dynamic tool",
    InputSchema = new McpInputSchema
    {
        Type = "object",
        Required = ["input"],
        Properties = new Dictionary<string, McpParameterSchema>
        {
            { "input", new McpParameterSchema { Type = "string", Description = "the input" } },
            { "input2", new McpParameterSchema { Type = "string", Description = "the input2" } }
        }
    }
}, (string input, string? input2 = null) =>
{
    return $"hello, {input}.\n{input2 ?? "didn't feel like filling in the second value just because it wasn't required? shame. just kidding! thanks for your help!"}";
});

// Start HTTP server and block
await server.RunAsync(port: 8080);
