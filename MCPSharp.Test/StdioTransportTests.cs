using System.Text;
using MCPSharp;
using MCPSharp.Example;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPSharp.Test
{
    [TestClass]
    public sealed class StdioTransportTests
    {
        private static McpServer _server = null!;
        private static MemoryStream _clientInput = null!;
        private static MemoryStream _serverOutput = null!;
        private static StreamWriter _writer = null!;
        private static StreamReader _reader = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Piped streams: client writes to clientInput, server reads from it
            // Server writes to serverOutput, client reads from it
            _clientInput = new MemoryStream();
            _serverOutput = new MemoryStream();

            _server = new McpServer("StdioTestServer", "1.0.0");
            _server.Register<MCPDev>();

            var transport = new StdioTransport(_clientInput, _serverOutput);
            _server.Start(transport, "stdio", 0);

            // Give server time to start read loop
            Thread.Sleep(200);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
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

            // Write to the stream the server reads from
            lock (_clientInput)
            {
                _clientInput.Write(bytes, 0, bytes.Length);
                _clientInput.Flush();
            }

            // Wait for server to process
            await Task.Delay(500);

            // Read server output
            _serverOutput.Position = 0;
            var reader = new StreamReader(_serverOutput, Encoding.UTF8);
            var responseLine = await reader.ReadLineAsync();
            // Reset for next read
            _serverOutput.SetLength(0);

            Assert.IsNotNull(responseLine, "No response received from stdio transport");
            return JObject.Parse(responseLine);
        }

        [TestMethod("Stdio - Initialize")]
        public async Task Test_StdioInitialize()
        {
            var result = await SendAndReceiveAsync("initialize", new { protocolVersion = "2025-03-26" });
            Assert.IsNotNull(result["result"]);
            Assert.AreEqual("StdioTestServer", result["result"]!["serverInfo"]!["name"]!.ToString());
        }

        [TestMethod("Stdio - Tools/List")]
        public async Task Test_StdioToolsList()
        {
            var result = await SendAndReceiveAsync("tools/list");
            var tools = result["result"]!["tools"] as JArray;
            Assert.IsNotNull(tools);
            Assert.IsTrue(tools!.Count > 0);
        }

        [TestMethod("Stdio - Ping")]
        public async Task Test_StdioPing()
        {
            var result = await SendAndReceiveAsync("ping");
            Assert.IsNotNull(result["result"]);
        }
    }
}
