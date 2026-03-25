using System.Diagnostics;
using System.Net.Http;
using MinMCPSharp;

namespace MinMCPSharp.Test
{
    /// <summary>
    /// Standalone smoke tests that launch the example executables as child processes,
    /// verify they start correctly, and exercise them via McpClient.
    /// This ensures the examples shipped in the repo actually work end-to-end.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class ExampleSmokeTests
    {
        private static string _repoRoot = null!;

        [ClassInitialize]
        public static void ClassInit(TestContext ctx)
        {
            var testDll = typeof(ExampleSmokeTests).Assembly.Location;
            _repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testDll)!,
                "..", "..", "..", ".."));
        }

        [TestMethod("HTTP example starts and responds to MCP client")]
        public async Task Test_HttpExample_SmokeTest()
        {
            var port = TestPortAllocator.GetPort();
            var exampleDll = Path.Combine(_repoRoot, "MinMCPSharp.Example",
                "bin", "Release", "net9.0", "MinMCPSharp.Example.dll");

            Assert.IsTrue(File.Exists(exampleDll),
                $"Example not found at: {exampleDll}. Run 'dotnet build --configuration Release' first.");

            // The example hardcodes port 8080, so we can't easily override it.
            // Instead, we just verify the process starts and binds successfully,
            // then use a direct HTTP probe + McpClient against its known port.
            // For CI isolation we launch with a custom environment variable approach.
            // Since the example uses RunAsync(port: 8080), we test against that port.
            // NOTE: If the example is refactored to accept a port arg, update this.

            var psi = new ProcessStartInfo("dotnet", exampleDll)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)!;
            try
            {
                // Wait for the server to be ready (poll with timeout)
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var ready = await WaitForServerReady(http, "http://localhost:8080/mcp",
                    TimeSpan.FromSeconds(10));
                Assert.IsTrue(ready, "HTTP example server did not become ready within timeout");

                // Connect an McpClient and exercise the full lifecycle
                using var client = new McpClient("SmokeTestClient", "1.0.0",
                    new Uri("http://localhost:8080/mcp"));

                await client.InitializeAsync();
                Assert.IsTrue(client.Initialized);

                // List tools - should include tools from MCPDev + ExternalTool + dynamic
                var tools = await client.GetToolsAsync();
                Assert.IsTrue(tools.Count >= 6,
                    $"Expected at least 6 tools, got {tools.Count}");
                Assert.IsTrue(tools.Any(t => t.Name == "Hello"));
                Assert.IsTrue(tools.Any(t => t.Name == "Echo"));
                Assert.IsTrue(tools.Any(t => t.Name == "Add"));
                Assert.IsTrue(tools.Any(t => t.Name == "dynamicTool"));

                // Call a tool
                var echo = await client.CallToolAsync("Echo",
                    new Dictionary<string, object> { { "input", "smoke test" } });
                Assert.IsFalse(echo.IsError);
                Assert.AreEqual("smoke test", echo.Content[0].Text);

                // Call dynamic tool
                var dyn = await client.CallToolAsync("dynamicTool",
                    new Dictionary<string, object> { { "input", "hello" } });
                Assert.IsFalse(dyn.IsError);
                Assert.IsTrue(dyn.Content[0].Text.Contains("hello"));

                // Ping
                await client.SendPingAsync();

                // Resources
                var resources = await client.GetResourcesAsync();
                Assert.IsTrue(resources.Resources.Count > 0);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
        }

        [TestMethod("Stdio example starts and responds to MCP client")]
        public async Task Test_StdioExample_SmokeTest()
        {
            var serverDll = Path.Combine(_repoRoot, "MinMCPSharp.StdioTestServer",
                "bin", "Release", "net9.0", "StdioTestServer.dll");

            Assert.IsTrue(File.Exists(serverDll),
                $"StdioTestServer not found at: {serverDll}. Run 'dotnet build --configuration Release' first.");

            using var client = new McpClient("StdioSmokeClient", "1.0.0", "dotnet", serverDll);

            await client.InitializeAsync();
            Assert.IsTrue(client.Initialized);

            var tools = await client.GetToolsAsync();
            Assert.IsTrue(tools.Any(t => t.Name == "echo"));
            Assert.IsTrue(tools.Any(t => t.Name == "add"));

            var echo = await client.CallToolAsync("echo",
                new Dictionary<string, object> { { "message", "stdio smoke" } });
            Assert.IsFalse(echo.IsError);
            Assert.AreEqual("stdio smoke", echo.Content[0].Text);

            var add = await client.CallToolAsync("add",
                new Dictionary<string, object> { { "a", 10 }, { "b", 32 } });
            Assert.IsFalse(add.IsError);
            Assert.AreEqual("42", add.Content[0].Text);

            await client.SendPingAsync();
        }

        private static async Task<bool> WaitForServerReady(HttpClient http, string url,
            TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    // Send a minimal JSON-RPC ping to check if server is alive
                    var content = new StringContent(
                        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}",
                        System.Text.Encoding.UTF8, "application/json");
                    var response = await http.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                        return true;
                }
                catch
                {
                    // Server not ready yet
                }
                await Task.Delay(250);
            }
            return false;
        }
    }
}
