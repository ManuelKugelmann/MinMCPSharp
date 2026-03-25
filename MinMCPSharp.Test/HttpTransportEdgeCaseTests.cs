using System.Net.Http;
using System.Text;
using MinMCPSharp;
using MinMCPSharp.Example;
using Newtonsoft.Json.Linq;

namespace MinMCPSharp.Test
{
    [TestClass]
    public sealed class HttpTransportEdgeCaseTests
    {
        private static McpServer _server = null!;
        private static HttpClient _http = null!;
        private static int _port;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _port = TestPortAllocator.GetPort();
            _server = new McpServer("EdgeCaseServer", "1.0.0");
            _server.Register<MCPDev>();

            // Dynamic tool with optional param
            _server.AddTool(new McpTool
            {
                Name = "greet",
                Description = "Greeting tool",
                InputSchema = new McpInputSchema
                {
                    Type = "object",
                    Required = ["name"],
                    Properties = new Dictionary<string, McpParameterSchema>
                    {
                        { "name", new McpParameterSchema { Type = "string", Description = "the name" } },
                        { "title", new McpParameterSchema { Type = "string", Description = "optional title" } }
                    }
                }
            }, (string name, string? title = null) =>
            {
                return title != null ? $"{title} {name}" : name;
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

        // -- HTTP method handling ---------------------------------------------

        [TestMethod("GET returns 405")]
        public async Task Test_GetMethodNotAllowed()
        {
            var response = await _http.GetAsync("/mcp");
            Assert.AreEqual(System.Net.HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        [TestMethod("PUT returns 405")]
        public async Task Test_PutMethodNotAllowed()
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _http.PutAsync("/mcp", content);
            Assert.AreEqual(System.Net.HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        [TestMethod("OPTIONS returns 204 (CORS preflight)")]
        public async Task Test_OptionsCorsPreflight()
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/mcp");
            var response = await _http.SendAsync(request);
            Assert.AreEqual(System.Net.HttpStatusCode.NoContent, response.StatusCode);
            Assert.IsTrue(response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.IsTrue(response.Headers.Contains("Access-Control-Allow-Methods"));
        }

        [TestMethod("Empty body returns 400")]
        public async Task Test_EmptyBody()
        {
            var content = new StringContent("", Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod("Whitespace-only body returns 400")]
        public async Task Test_WhitespaceBody()
        {
            var content = new StringContent("   ", Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }

        // -- CORS headers on normal responses ---------------------------------

        [TestMethod("POST response includes CORS headers")]
        public async Task Test_CorsHeadersOnPost()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "ping"
            };
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            Assert.IsTrue(response.Headers.Contains("Access-Control-Allow-Origin"));
        }

        // -- Origin validation ------------------------------------------------

        [TestMethod("Request with evil origin is rejected when AllowedOrigins set")]
        public async Task Test_OriginValidation()
        {
            // Create a separate server with restricted origins
            var port = TestPortAllocator.GetPort();
            var transport = new HttpListenerTransport();
            transport.AllowedOrigins = new[] { "http://localhost:" + port };

            using var server = new McpServer("OriginTest", "1.0.0");
            server.Register<MCPDev>();
            server.Start(transport, "localhost", port);

            using var http = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"http://localhost:{port}/mcp");
            request.Headers.Add("Origin", "http://evil.example.com");
            request.Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}",
                Encoding.UTF8, "application/json");

            var response = await http.SendAsync(request);
            Assert.AreEqual(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        }

        [TestMethod("Request with localhost origin is allowed by default")]
        public async Task Test_LocalhostOriginAllowed()
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"http://localhost:{_port}/mcp");
            request.Headers.Add("Origin", "http://localhost:3000");
            request.Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}",
                Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod("Request with non-localhost origin is rejected by default")]
        public async Task Test_NonLocalhostOriginRejected()
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"http://localhost:{_port}/mcp");
            request.Headers.Add("Origin", "http://attacker.com");
            request.Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}",
                Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            Assert.AreEqual(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        }

        // -- Protocol edge cases over HTTP ------------------------------------

        [TestMethod("resources/templates/list over HTTP")]
        public async Task Test_ResourceTemplatesListOverHttp()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "resources/templates/list"
            };
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(body);

            var templates = result["result"]!["resourceTemplates"] as JArray;
            Assert.IsNotNull(templates);
            Assert.AreEqual(0, templates!.Count);
        }

        [TestMethod("Void tool (write-to-console) over HTTP")]
        public async Task Test_VoidToolOverHttp()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = JToken.FromObject(new
                {
                    name = "write-to-console",
                    arguments = new { message = "test output" }
                })
            };
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(body);

            // void returns null -> isError with "null" message
            Assert.IsTrue((bool)result["result"]!["isError"]!);
        }

        [TestMethod("Dynamic tool with only required param (optional omitted)")]
        public async Task Test_DynamicToolOptionalOmitted()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = JToken.FromObject(new
                {
                    name = "greet",
                    arguments = new { name = "Alice" }
                })
            };
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(body);

            var text = result["result"]!["content"]![0]!["text"]!.ToString();
            Assert.AreEqual("Alice", text);
        }

        [TestMethod("Dynamic tool with both params provided")]
        public async Task Test_DynamicToolBothParams()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = JToken.FromObject(new
                {
                    name = "greet",
                    arguments = new { name = "Alice", title = "Dr." }
                })
            };
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(body);

            var text = result["result"]!["content"]![0]!["text"]!.ToString();
            Assert.AreEqual("Dr. Alice", text);
        }

        [TestMethod("Response content-type is application/json")]
        public async Task Test_ResponseContentType()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "ping"
            };
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        }

        [TestMethod("JSON-RPC response always has jsonrpc field")]
        public async Task Test_JsonRpcField()
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "ping"
            };
            var content = new StringContent(request.ToString(), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/mcp", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(body);
            Assert.AreEqual("2.0", result["jsonrpc"]!.ToString());
        }
    }
}
