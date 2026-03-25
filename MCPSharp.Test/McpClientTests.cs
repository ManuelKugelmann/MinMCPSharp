using MCPSharp;
using MCPSharp.Example;

namespace MCPSharp.Test
{
    [TestClass]
    public sealed class McpClientTests
    {
        private static McpServer _server = null!;
        private static McpClient _client = null!;
        private static int _port;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            _port = TestPortAllocator.GetPort();
            _server = new McpServer("ClientTestServer", "1.0.0");
            _server.Register<MCPDev>();
            _server.Start("localhost", _port);

            _client = new McpClient("TestClient", "1.0.0",
                new Uri($"http://localhost:{_port}/mcp"));
            await _client.InitializeAsync();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _client?.Dispose();
            _server?.Dispose();
        }

        [TestMethod("Client - Initialized")]
        public void Test_ClientInitialized()
        {
            Assert.IsTrue(_client.Initialized);
        }

        [TestMethod("Client - Tools/List")]
        public async Task Test_ClientListTools()
        {
            var tools = await _client.GetToolsAsync();
            Assert.IsNotNull(tools);
            Assert.IsTrue(tools.Count > 0);
            foreach (var tool in tools)
            {
                Assert.IsFalse(string.IsNullOrEmpty(tool.Name));
            }
        }

        [TestMethod("Client - Tools/Call")]
        public async Task Test_ClientCallTool()
        {
            var result = await _client.CallToolAsync("Hello");
            Assert.IsFalse(result.IsError);
            Assert.AreEqual("hello, claude.", result.Content[0].Text);
        }

        [TestMethod("Client - Tools/Call with params")]
        public async Task Test_ClientCallToolWithParams()
        {
            var result = await _client.CallToolAsync("Echo",
                new Dictionary<string, object> { { "input", "test echo" } });
            Assert.IsFalse(result.IsError);
            Assert.AreEqual("test echo", result.Content[0].Text);
        }

        [TestMethod("Client - Tools/Call invalid")]
        public async Task Test_ClientCallInvalidTool()
        {
            var result = await _client.CallToolAsync("NotARealTool");
            Assert.IsTrue(result.IsError);
        }

        [TestMethod("Client - Resources/List")]
        public async Task Test_ClientListResources()
        {
            var result = await _client.GetResourcesAsync();
            Assert.IsNotNull(result.Resources);
            Assert.IsTrue(result.Resources.Count > 0);
        }

        [TestMethod("Client - Prompts/List")]
        public async Task Test_ClientListPrompts()
        {
            var result = await _client.GetPromptListAsync();
            Assert.IsNotNull(result["prompts"]);
        }

        [TestMethod("Client - Ping")]
        public async Task Test_ClientPing()
        {
            await _client.SendPingAsync();
            // No exception = success
        }

        [TestMethod("Client - Permission denied")]
        public async Task Test_ClientPermissionDenied()
        {
            var client = new McpClient("PermTestClient", "1.0.0",
                new Uri($"http://localhost:{_port}/mcp"));
            client.GetPermission = (_) => false;

            var result = await client.CallToolAsync("Hello");
            Assert.IsTrue(result.IsError);
            Assert.AreEqual("Permission Denied.", result.Content[0].Text);
            client.Dispose();
        }

        [TestMethod("Client - Complex object")]
        public async Task Test_ClientComplexObject()
        {
            var result = await _client.CallToolAsync("AddComplex",
                new Dictionary<string, object>
                {
                    { "obj", new { Name = "Claude", Age = 25, Hobbies = new[] { "Programming", "Gaming" } } }
                });
            Assert.IsFalse(result.IsError);
            Assert.AreEqual("Name: Claude, Age: 25, Hobbies: Programming, Gaming", result.Content[0].Text);
        }

        [TestMethod("Client - Exception handling")]
        public async Task Test_ClientExceptionHandling()
        {
            var result = await _client.CallToolAsync("throw_exception");
            Assert.IsTrue(result.IsError);
            Assert.AreEqual("This is an exception", result.Content[0].Text);
        }
    }
}
