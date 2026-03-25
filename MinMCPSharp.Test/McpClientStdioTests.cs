using MinMCPSharp;

namespace MinMCPSharp.Test
{
    /// <summary>
    /// Tests McpClient's stdio transport by spawning a real child process.
    /// This exercises the process-spawn, stdin/stdout piping, and JSON-RPC
    /// serialization over stdio - the only code path not covered by HTTP tests.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class McpClientStdioTests
    {
        private static string _serverDllPath = null!;

        [ClassInitialize]
        public static void ClassInit(TestContext ctx)
        {
            // Find the built StdioTestServer DLL by walking up from the test assembly location
            var testDll = typeof(McpClientStdioTests).Assembly.Location;
            var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testDll)!,
                "..", "..", "..", ".."));
            _serverDllPath = Path.Combine(repoRoot, "MinMCPSharp.StdioTestServer",
                "bin", "Release", "net9.0", "StdioTestServer.dll");

            Assert.IsTrue(File.Exists(_serverDllPath),
                $"StdioTestServer not found at: {_serverDllPath}. Run 'dotnet build --configuration Release' first.");
        }

        private McpClient CreateStdioClient()
        {
            // "dotnet" is always on PATH when running inside dotnet test.
            // ProcessStartInfo resolves it via PATH - no need to locate the binary.
            return new McpClient("StdioTestClient", "1.0.0", "dotnet", _serverDllPath);
        }

        [TestMethod("Stdio client - initialize")]
        public async Task Test_StdioClientInitialize()
        {
            using var client = CreateStdioClient();
            await client.InitializeAsync();
            Assert.IsTrue(client.Initialized);
        }

        [TestMethod("Stdio client - list tools")]
        public async Task Test_StdioClientListTools()
        {
            using var client = CreateStdioClient();
            await client.InitializeAsync();

            var tools = await client.GetToolsAsync();
            Assert.IsTrue(tools.Count >= 2);
            Assert.IsTrue(tools.Any(t => t.Name == "echo"));
            Assert.IsTrue(tools.Any(t => t.Name == "add"));
        }

        [TestMethod("Stdio client - call echo tool")]
        public async Task Test_StdioClientCallEcho()
        {
            using var client = CreateStdioClient();
            await client.InitializeAsync();

            var result = await client.CallToolAsync("echo",
                new Dictionary<string, object> { { "message", "hello stdio" } });
            Assert.IsFalse(result.IsError);
            Assert.AreEqual("hello stdio", result.Content[0].Text);
        }

        [TestMethod("Stdio client - call add tool")]
        public async Task Test_StdioClientCallAdd()
        {
            using var client = CreateStdioClient();
            await client.InitializeAsync();

            var result = await client.CallToolAsync("add",
                new Dictionary<string, object> { { "a", 17 }, { "b", 25 } });
            Assert.IsFalse(result.IsError);
            Assert.AreEqual("42", result.Content[0].Text);
        }

        [TestMethod("Stdio client - call invalid tool")]
        public async Task Test_StdioClientCallInvalidTool()
        {
            using var client = CreateStdioClient();
            await client.InitializeAsync();

            var result = await client.CallToolAsync("nonexistent");
            Assert.IsTrue(result.IsError);
        }

        [TestMethod("Stdio client - ping")]
        public async Task Test_StdioClientPing()
        {
            using var client = CreateStdioClient();
            await client.InitializeAsync();

            await client.SendPingAsync();
            // No exception = success
        }

        [TestMethod("Stdio client - tools cached after init")]
        public async Task Test_StdioClientToolsCached()
        {
            using var client = CreateStdioClient();
            Assert.AreEqual(0, client.Tools.Count);

            await client.InitializeAsync();
            Assert.IsTrue(client.Tools.Count >= 2);
        }

        [TestMethod("Stdio client - full lifecycle")]
        public async Task Test_StdioClientFullLifecycle()
        {
            using var client = CreateStdioClient();

            // Init
            await client.InitializeAsync();
            Assert.IsTrue(client.Initialized);

            // List tools
            var tools = await client.GetToolsAsync();
            Assert.IsTrue(tools.Count >= 2);

            // Call tools
            var echo = await client.CallToolAsync("echo",
                new Dictionary<string, object> { { "message", "lifecycle test" } });
            Assert.IsFalse(echo.IsError);
            Assert.AreEqual("lifecycle test", echo.Content[0].Text);

            var add = await client.CallToolAsync("add",
                new Dictionary<string, object> { { "a", 1 }, { "b", 2 } });
            Assert.IsFalse(add.IsError);
            Assert.AreEqual("3", add.Content[0].Text);

            // Error case
            var bad = await client.CallToolAsync("missing");
            Assert.IsTrue(bad.IsError);

            // Ping
            await client.SendPingAsync();
        }
    }
}
