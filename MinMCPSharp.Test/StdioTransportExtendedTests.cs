using System.Collections.Concurrent;
using System.Text;
using MinMCPSharp;
using MinMCPSharp.Example;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MinMCPSharp.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class StdioTransportExtendedTests
    {
        private static McpServer _server = null!;
        private static BlockingStream _toServer = null!;
        private static BlockingStream _fromServer = null!;
        private static StreamReader _reader = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _toServer = new BlockingStream();
            _fromServer = new BlockingStream();

            _server = new McpServer("StdioExtServer", "1.0.0");
            _server.Register<MCPDev>();

            var transport = new StdioTransport(_toServer, _fromServer);
            _server.Start(transport, "stdio", 0);

            Thread.Sleep(200);
            _reader = new StreamReader(_fromServer, Encoding.UTF8);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _toServer?.Complete();
            _server?.Dispose();
        }

        private static async Task<JObject> SendAndReceiveAsync(string method, object? parms = null)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = method
            };
            if (parms != null)
                request["params"] = JToken.FromObject(parms);

            var json = request.ToString(Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            _toServer.Write(bytes, 0, bytes.Length);
            _toServer.Flush();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var responseLine = await Task.Run(() => _reader.ReadLine(), cts.Token);
            Assert.IsNotNull(responseLine, "No response from stdio transport");
            return JObject.Parse(responseLine);
        }

        [TestMethod("Stdio - tools/call Echo")]
        public async Task Test_StdioCallEcho()
        {
            var result = await SendAndReceiveAsync("tools/call", new
            {
                name = "Echo",
                arguments = new { input = "stdio echo test" }
            });
            var content = result["result"]!["content"] as JArray;
            Assert.IsNotNull(content);
            Assert.AreEqual("stdio echo test", content![0]!["text"]!.ToString());
            Assert.IsFalse((bool)result["result"]!["isError"]!);
        }

        [TestMethod("Stdio - tools/call Add with int params")]
        public async Task Test_StdioCallAdd()
        {
            var result = await SendAndReceiveAsync("tools/call", new
            {
                name = "Add",
                arguments = new { a = 5, b = 3 }
            });
            var content = result["result"]!["content"] as JArray;
            Assert.AreEqual("8", content![0]!["text"]!.ToString());
        }

        [TestMethod("Stdio - tools/call invalid tool")]
        public async Task Test_StdioCallInvalidTool()
        {
            var result = await SendAndReceiveAsync("tools/call", new
            {
                name = "NoSuchTool",
                arguments = new { }
            });
            Assert.IsTrue((bool)result["result"]!["isError"]!);
        }

        [TestMethod("Stdio - tools/call exception")]
        public async Task Test_StdioCallException()
        {
            var result = await SendAndReceiveAsync("tools/call", new
            {
                name = "throw_exception",
                arguments = new { }
            });
            Assert.IsTrue((bool)result["result"]!["isError"]!);
            var content = result["result"]!["content"] as JArray;
            Assert.AreEqual("This is an exception", content![0]!["text"]!.ToString());
        }

        [TestMethod("Stdio - resources/list")]
        public async Task Test_StdioResourcesList()
        {
            var result = await SendAndReceiveAsync("resources/list");
            var resources = result["result"]!["resources"] as JArray;
            Assert.IsNotNull(resources);
            Assert.IsTrue(resources!.Count > 0);
        }

        [TestMethod("Stdio - unknown method returns error")]
        public async Task Test_StdioUnknownMethod()
        {
            var result = await SendAndReceiveAsync("bogus/method");
            Assert.IsNotNull(result["error"]);
            Assert.AreEqual(-32601, (int)result["error"]!["code"]!);
        }

        [TestMethod("Stdio - invalid JSON returns parse error")]
        public async Task Test_StdioInvalidJson()
        {
            var bytes = Encoding.UTF8.GetBytes("not valid json\n");
            _toServer.Write(bytes, 0, bytes.Length);
            _toServer.Flush();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var responseLine = await Task.Run(() => _reader.ReadLine(), cts.Token);
            Assert.IsNotNull(responseLine);
            var result = JObject.Parse(responseLine!);
            Assert.AreEqual(-32700, (int)result["error"]!["code"]!);
        }

        [TestMethod("Stdio - complex object tool call")]
        public async Task Test_StdioComplexObject()
        {
            var result = await SendAndReceiveAsync("tools/call", new
            {
                name = "AddComplex",
                arguments = new
                {
                    obj = new { Name = "Stdio", Age = 10, Hobbies = new[] { "Testing" } }
                }
            });
            var content = result["result"]!["content"] as JArray;
            Assert.AreEqual("Name: Stdio, Age: 10, Hobbies: Testing", content![0]!["text"]!.ToString());
        }
    }
}
