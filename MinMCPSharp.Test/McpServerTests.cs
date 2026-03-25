using MinMCPSharp;
using MinMCPSharp.Example;

namespace MinMCPSharp.Test
{
    [TestClass]
    public sealed class McpServerTests
    {
        [TestMethod("Constructor sets name and version")]
        public void Test_Constructor()
        {
            using var server = new McpServer("MyServer", "2.0.0");
            Assert.AreEqual("MyServer", server.ServerName);
            Assert.AreEqual("2.0.0", server.ServerVersion);
        }

        [TestMethod("Constructor defaults")]
        public void Test_ConstructorDefaults()
        {
            using var server = new McpServer();
            Assert.AreEqual("MinMCPSharp", server.ServerName);
            Assert.AreEqual("1.0.0", server.ServerVersion);
        }

        [TestMethod("IsRunning is false before Start")]
        public void Test_NotRunningBeforeStart()
        {
            using var server = new McpServer();
            Assert.IsFalse(server.IsRunning);
        }

        [TestMethod("Start and Stop lifecycle")]
        public void Test_StartStop()
        {
            var port = TestPortAllocator.GetPort();
            using var server = new McpServer("Test", "1.0.0");
            server.Register<MCPDev>();
            server.Start("localhost", port);

            Assert.IsTrue(server.IsRunning);

            server.Stop();
            Assert.IsFalse(server.IsRunning);
        }

        [TestMethod("Dispose stops server")]
        public void Test_Dispose()
        {
            var port = TestPortAllocator.GetPort();
            var server = new McpServer("Test", "1.0.0");
            server.Start("localhost", port);
            server.Dispose();

            Assert.IsFalse(server.IsRunning);
        }

        [TestMethod("RegisterAssembly discovers tools")]
        public void Test_RegisterAssembly()
        {
            var port = TestPortAllocator.GetPort();
            using var server = new McpServer("AssemblyTest", "1.0.0");
            server.RegisterAssembly(typeof(MCPDev).Assembly);
            server.Start("localhost", port);

            // Verify server is running and can respond
            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var request = new { jsonrpc = "2.0", id = 1, method = "tools/list" };
            var content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(request),
                System.Text.Encoding.UTF8, "application/json");
            var response = http.PostAsync("/mcp", content).Result;
            var body = response.Content.ReadAsStringAsync().Result;
            var result = Newtonsoft.Json.Linq.JObject.Parse(body);
            var tools = result["result"]!["tools"] as Newtonsoft.Json.Linq.JArray;

            Assert.IsNotNull(tools);
            Assert.IsTrue(tools!.Count > 0);
            var names = tools.Select(t => t["name"]!.ToString()).ToList();
            Assert.IsTrue(names.Contains("Hello"));
        }

        [TestMethod("RunAsync stops on cancellation")]
        public async Task Test_RunAsyncCancellation()
        {
            var port = TestPortAllocator.GetPort();
            var server = new McpServer("RunAsyncTest", "1.0.0");
            var cts = new CancellationTokenSource();

            var task = server.RunAsync(port: port, ct: cts.Token);

            // Give server time to start
            await Task.Delay(200);
            Assert.IsTrue(server.IsRunning);

            cts.Cancel();
            await task;

            Assert.IsFalse(server.IsRunning);
        }

        [TestMethod("DispatchWrapper wraps request handling")]
        public async Task Test_DispatchWrapper()
        {
            var port = TestPortAllocator.GetPort();
            bool wrapperCalled = false;

            using var server = new McpServer("WrapperTest", "1.0.0");
            server.Register<MCPDev>();
            server.DispatchWrapper = async (func) =>
            {
                wrapperCalled = true;
                return await func();
            };
            server.Start("localhost", port);

            using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
            var request = new { jsonrpc = "2.0", id = 1, method = "ping" };
            var content = new StringContent(
                Newtonsoft.Json.JsonConvert.SerializeObject(request),
                System.Text.Encoding.UTF8, "application/json");
            await http.PostAsync("/mcp", content);

            Assert.IsTrue(wrapperCalled);
        }

        [TestMethod("Log callback is invoked")]
        public void Test_LogCallback()
        {
            var port = TestPortAllocator.GetPort();
            var messages = new List<string>();

            using var server = new McpServer("LogTest", "1.0.0");
            server.Log = (msg) => messages.Add(msg);
            server.Start("localhost", port);

            Assert.IsTrue(messages.Count > 0);
            Assert.IsTrue(messages.Any(m => m.Contains("Started")));
        }

        [TestMethod("Double Start is ignored")]
        public void Test_DoubleStart()
        {
            var port = TestPortAllocator.GetPort();
            var messages = new List<string>();

            using var server = new McpServer("DoubleStart", "1.0.0");
            server.Log = (msg) => messages.Add(msg);
            server.Start("localhost", port);
            server.Start("localhost", port); // Should log "Already running"

            Assert.IsTrue(messages.Any(m => m.Contains("Already running")));
        }

        [TestMethod("Register returns server for chaining")]
        public void Test_FluentChaining()
        {
            using var server = new McpServer("Chain", "1.0.0");
            var result = server.Register<MCPDev>();
            Assert.AreSame(server, result);
        }
    }
}
