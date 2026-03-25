using System.Net.Http;
using System.Text;
using MinMCPSharp;
using MinMCPSharp.Example;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MinMCPSharp.Test
{
    [TestClass]
    public sealed class HttpTransportTests
    {
        private static McpServer _server = null!;
        private static HttpClient _http = null!;
        private static int _port;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _port = TestPortAllocator.GetPort();
            _server = new McpServer("TestServer", "1.0.0");
            _server.Register<MCPDev>();

            _server.AddTool(new McpTool
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
                return $"hello, {input}.\n{input2 ?? "no second value"}";
            });

            _server.Start("localhost", _port);
            _http = new HttpClient { BaseAddress = new Uri($"http://localhost:{_port}") };
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _http?.Dispose();
            _server?.Dispose();
        }

        private static async Task<JObject> SendRpcAsync(string method, object? parms = null, object? id = null)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JToken.FromObject(id ?? 1),
                ["method"] = method
            };
            if (parms != null)
                request["params"] = JToken.FromObject(parms);

            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            var body = await response.Content.ReadAsStringAsync();
            return JObject.Parse(body);
        }

        // -- Initialize ---------------------------------------------------

        [TestMethod("Initialize")]
        public async Task Test_Initialize()
        {
            var result = await SendRpcAsync("initialize", new { protocolVersion = "2025-03-26" });
            Assert.IsNotNull(result["result"]);
            Assert.AreEqual("2025-03-26", result["result"]!["protocolVersion"]!.ToString());
            Assert.AreEqual("TestServer", result["result"]!["serverInfo"]!["name"]!.ToString());
        }

        // -- Tools --------------------------------------------------------

        [TestMethod("Tools/List")]
        public async Task Test_ListTools()
        {
            var result = await SendRpcAsync("tools/list");
            var tools = result["result"]!["tools"] as JArray;
            Assert.IsNotNull(tools);
            Assert.IsTrue(tools!.Count > 0);

            foreach (var tool in tools)
            {
                Assert.IsFalse(string.IsNullOrEmpty(tool["name"]?.ToString()));
            }
        }

        [TestMethod("Tools/Call - Hello")]
        public async Task Test_CallTool_Hello()
        {
            var result = await SendRpcAsync("tools/call", new { name = "Hello", arguments = new { } });
            var content = result["result"]!["content"] as JArray;
            Assert.IsNotNull(content);
            Assert.AreEqual("hello, claude.", content![0]!["text"]!.ToString());
        }

        [TestMethod("Tools/Call - Echo")]
        public async Task Test_CallTool_Echo()
        {
            var result = await SendRpcAsync("tools/call", new
            {
                name = "Echo",
                arguments = new { input = "this is a test of the echo function" }
            });
            var content = result["result"]!["content"] as JArray;
            Assert.AreEqual("this is a test of the echo function", content![0]!["text"]!.ToString());
            Assert.IsFalse((bool)result["result"]!["isError"]!);
        }

        [TestMethod("Tools/Call - Dynamic Tool")]
        public async Task Test_CallTool_Dynamic()
        {
            var result = await SendRpcAsync("tools/call", new
            {
                name = "dynamicTool",
                arguments = new { input = "test string", input2 = "another string" }
            });
            var content = result["result"]!["content"] as JArray;
            Assert.AreEqual("hello, test string.\nanother string", content![0]!["text"]!.ToString());
        }

        [TestMethod("Tools/Call - Add")]
        public async Task Test_CallTool_Add()
        {
            var result = await SendRpcAsync("tools/call", new
            {
                name = "Add",
                arguments = new { a = 3, b = 7 }
            });
            var content = result["result"]!["content"] as JArray;
            Assert.AreEqual("10", content![0]!["text"]!.ToString());
        }

        [TestMethod("Tools/Call - Complex Object")]
        public async Task Test_CallTool_ComplexObject()
        {
            var result = await SendRpcAsync("tools/call", new
            {
                name = "AddComplex",
                arguments = new
                {
                    obj = new { Name = "Claude", Age = 25, Hobbies = new[] { "Programming", "Gaming" } }
                }
            });
            var content = result["result"]!["content"] as JArray;
            Assert.AreEqual("Name: Claude, Age: 25, Hobbies: Programming, Gaming", content![0]!["text"]!.ToString());
        }

        [TestMethod("Tools/Call - Invalid Tool")]
        public async Task Test_CallTool_InvalidTool()
        {
            var result = await SendRpcAsync("tools/call", new { name = "NotARealTool", arguments = new { } });
            Assert.IsTrue((bool)result["result"]!["isError"]!);
        }

        [TestMethod("Tools/Call - Exception Handling")]
        public async Task Test_CallTool_Exception()
        {
            var result = await SendRpcAsync("tools/call", new { name = "throw_exception", arguments = new { } });
            Assert.IsTrue((bool)result["result"]!["isError"]!);
            var content = result["result"]!["content"] as JArray;
            Assert.AreEqual("This is an exception", content![0]!["text"]!.ToString());
        }

        // -- Resources ----------------------------------------------------

        [TestMethod("Resources/List")]
        public async Task Test_ListResources()
        {
            var result = await SendRpcAsync("resources/list");
            var resources = result["result"]!["resources"] as JArray;
            Assert.IsNotNull(resources);
            Assert.IsTrue(resources!.Count > 0);
        }

        // -- Prompts ------------------------------------------------------

        [TestMethod("Prompts/List")]
        public async Task Test_ListPrompts()
        {
            var result = await SendRpcAsync("prompts/list");
            var prompts = result["result"]!["prompts"] as JArray;
            Assert.IsNotNull(prompts);
            Assert.AreEqual(0, prompts!.Count);
        }

        // -- Ping ---------------------------------------------------------

        [TestMethod("Ping")]
        public async Task Test_Ping()
        {
            var result = await SendRpcAsync("ping");
            Assert.IsNotNull(result["result"]);
        }

        // -- Notifications ------------------------------------------------

        [TestMethod("Notification returns 202")]
        public async Task Test_Notification()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/initialized"
            };
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            Assert.AreEqual(202, (int)response.StatusCode);
        }

        // -- Error cases --------------------------------------------------

        [TestMethod("Unknown method returns error")]
        public async Task Test_UnknownMethod()
        {
            var result = await SendRpcAsync("nonexistent/method");
            Assert.IsNotNull(result["error"]);
            Assert.AreEqual(-32601, (int)result["error"]!["code"]!);
        }

        [TestMethod("Invalid JSON returns parse error")]
        public async Task Test_InvalidJson()
        {
            var content = new StringContent("not json", Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(body);
            Assert.IsNotNull(result["error"]);
            Assert.AreEqual(-32700, (int)result["error"]!["code"]!);
        }
    }
}
