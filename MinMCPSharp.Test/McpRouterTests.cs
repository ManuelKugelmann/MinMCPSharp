using MinMCPSharp;
using Newtonsoft.Json.Linq;

namespace MinMCPSharp.Test
{
    [TestClass]
    public sealed class McpRouterTests
    {
        private static McpRouter _router = null!;

        [ClassInitialize]
        public static void ClassInit(TestContext ctx)
        {
            var tools = new ToolManager();
            tools.Register<MinMCPSharp.Example.MCPDev>();
            var resources = new ResourceManager();
            resources.Register<MinMCPSharp.Example.MCPDev>();
            _router = new McpRouter(tools, resources,
                new McpImplementation("RouterTest", "1.0.0"));
        }

        private static JObject MakeRequest(string method, object parms = null, object id = null)
        {
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JToken.FromObject(id ?? 1),
                ["method"] = method
            };
            if (parms != null)
                req["params"] = JToken.FromObject(parms);
            return req;
        }

        [TestMethod("HandleRequestAsync - resources/templates/list returns empty")]
        public async Task Test_ResourceTemplatesList()
        {
            var req = MakeRequest("resources/templates/list");
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);

            Assert.IsNotNull(result["result"]);
            var templates = result["result"]!["resourceTemplates"] as JArray;
            Assert.IsNotNull(templates);
            Assert.AreEqual(0, templates!.Count);
        }

        [TestMethod("HandleRequestAsync - notification returns null")]
        public async Task Test_NotificationReturnsNull()
        {
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "notifications/initialized"
            };
            var response = await _router.HandleRequestAsync(req.ToString());
            Assert.IsNull(response);
        }

        [TestMethod("HandleRequestAsync - null id treated as notification")]
        public async Task Test_NullIdNotification()
        {
            var req = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JValue.CreateNull(),
                ["method"] = "ping"
            };
            var response = await _router.HandleRequestAsync(req.ToString());
            Assert.IsNull(response);
        }

        [TestMethod("HandleRequestAsync - parse error")]
        public async Task Test_ParseError()
        {
            var response = await _router.HandleRequestAsync("not json at all");
            var result = JObject.Parse(response);
            Assert.AreEqual(-32700, (int)result["error"]!["code"]!);
        }

        [TestMethod("HandleRequestAsync - unknown method")]
        public async Task Test_UnknownMethod()
        {
            var req = MakeRequest("fake/method");
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            Assert.AreEqual(-32601, (int)result["error"]!["code"]!);
            Assert.IsTrue(result["error"]!["message"]!.ToString().Contains("fake/method"));
        }

        [TestMethod("HandleRequestAsync - initialize returns server info")]
        public async Task Test_Initialize()
        {
            var req = MakeRequest("initialize", new { protocolVersion = "2025-03-26" });
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            Assert.AreEqual("RouterTest", result["result"]!["serverInfo"]!["name"]!.ToString());
            Assert.AreEqual("2025-03-26", result["result"]!["protocolVersion"]!.ToString());
        }

        [TestMethod("HandleRequestAsync - initialize without protocolVersion uses default")]
        public async Task Test_InitializeDefaultProtocol()
        {
            var req = MakeRequest("initialize");
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            Assert.AreEqual("2025-03-26", result["result"]!["protocolVersion"]!.ToString());
        }

        [TestMethod("HandleRequestAsync - tools/list returns registered tools")]
        public async Task Test_ToolsList()
        {
            var req = MakeRequest("tools/list");
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            var tools = result["result"]!["tools"] as JArray;
            Assert.IsNotNull(tools);
            Assert.IsTrue(tools!.Count > 0);

            var names = tools.Select(t => t["name"]!.ToString()).ToList();
            Assert.IsTrue(names.Contains("Hello"));
            Assert.IsTrue(names.Contains("Echo"));
            Assert.IsTrue(names.Contains("Add"));
        }

        [TestMethod("HandleRequestAsync - tools/call with missing tool")]
        public async Task Test_ToolCallMissing()
        {
            var req = MakeRequest("tools/call", new { name = "nonexistent", arguments = new { } });
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            Assert.IsTrue((bool)result["result"]!["isError"]!);
        }

        [TestMethod("HandleRequestAsync - tools/call success")]
        public async Task Test_ToolCallSuccess()
        {
            var req = MakeRequest("tools/call", new { name = "Echo", arguments = new { input = "test" } });
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            Assert.IsFalse((bool)result["result"]!["isError"]!);
            Assert.AreEqual("test", result["result"]!["content"]![0]!["text"]!.ToString());
        }

        [TestMethod("HandleRequestAsync - resources/list returns resources")]
        public async Task Test_ResourcesList()
        {
            var req = MakeRequest("resources/list");
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            var resources = result["result"]!["resources"] as JArray;
            Assert.IsNotNull(resources);
            Assert.IsTrue(resources!.Count > 0);
        }

        [TestMethod("HandleRequestAsync - ping returns empty result")]
        public async Task Test_Ping()
        {
            var req = MakeRequest("ping");
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            Assert.IsNotNull(result["result"]);
        }

        [TestMethod("HandleRequestAsync - preserves request id")]
        public async Task Test_PreservesId()
        {
            var req = MakeRequest("ping", null, 42);
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            Assert.AreEqual(42, (int)result["id"]!);
        }

        [TestMethod("HandleRequestAsync - string id")]
        public async Task Test_StringId()
        {
            var req = MakeRequest("ping", null, "my-id");
            var response = await _router.HandleRequestAsync(req.ToString());
            var result = JObject.Parse(response);
            Assert.AreEqual("my-id", result["id"]!.ToString());
        }
    }
}
