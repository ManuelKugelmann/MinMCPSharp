using MinMCPSharp;
using Newtonsoft.Json.Linq;

namespace MinMCPSharp.Test
{
    // Test tool class with various method signatures
    public class ToolHandlerTestTools
    {
        [McpTool]
        public static async Task<string> AsyncTool([McpParameter(true)] string input)
        {
            await Task.Delay(10);
            return $"async:{input}";
        }

        [McpTool]
        public static async Task<int> AsyncIntTool()
        {
            await Task.Delay(10);
            return 42;
        }

        [McpTool]
        public static string[] ArrayTool() => new[] { "one", "two", "three" };

        [McpTool]
        public static string DefaultValueTool(string input = "default_value") => input;

        [McpTool]
        public static string? NullTool() => null;

        [McpTool]
        public static void VoidTool() { }

        [McpTool]
        public string InstanceTool() => "instance-result";
    }

    [TestClass]
    public sealed class ToolHandlerTests
    {
        private static ToolManager _mgr = null!;

        [ClassInitialize]
        public static void ClassInit(TestContext ctx)
        {
            _mgr = new ToolManager();
            _mgr.Register<ToolHandlerTestTools>();
        }

        [TestMethod("HandleAsync - async Task<string> method")]
        public async Task Test_AsyncStringTool()
        {
            var handler = _mgr.Tools["AsyncTool"];
            var result = await handler.HandleAsync(
                new Dictionary<string, object> { { "input", "hello" } });

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("async:hello", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - async Task<int> method")]
        public async Task Test_AsyncIntTool()
        {
            var handler = _mgr.Tools["AsyncIntTool"];
            var result = await handler.HandleAsync(new Dictionary<string, object>());

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("42", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - string[] result joins with newline")]
        public async Task Test_ArrayResult()
        {
            var handler = _mgr.Tools["ArrayTool"];
            var result = await handler.HandleAsync(new Dictionary<string, object>());

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("one\ntwo\nthree", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - default parameter value used when missing")]
        public async Task Test_DefaultValue()
        {
            var handler = _mgr.Tools["DefaultValueTool"];
            var result = await handler.HandleAsync(new Dictionary<string, object>());

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("default_value", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - default value overridden when provided")]
        public async Task Test_DefaultValueOverridden()
        {
            var handler = _mgr.Tools["DefaultValueTool"];
            var result = await handler.HandleAsync(
                new Dictionary<string, object> { { "input", "custom" } });

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("custom", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - null result returns error")]
        public async Task Test_NullResult()
        {
            var handler = _mgr.Tools["NullTool"];
            var result = await handler.HandleAsync(new Dictionary<string, object>());

            Assert.IsTrue(result.IsError);
            Assert.AreEqual("null", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - void method returns null error")]
        public async Task Test_VoidMethod()
        {
            var handler = _mgr.Tools["VoidTool"];
            var result = await handler.HandleAsync(new Dictionary<string, object>());

            // void returns null, which becomes an error result
            Assert.IsTrue(result.IsError);
        }

        [TestMethod("HandleAsync - instance method creates instance")]
        public async Task Test_InstanceMethod()
        {
            var handler = _mgr.Tools["InstanceTool"];
            var result = await handler.HandleAsync(new Dictionary<string, object>());

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("instance-result", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - JToken parameter conversion")]
        public async Task Test_JTokenConversion()
        {
            var handler = _mgr.Tools["DefaultValueTool"];
            var result = await handler.HandleAsync(
                new Dictionary<string, object> { { "input", new JValue("jtoken-value") } });

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("jtoken-value", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - null parameters uses defaults")]
        public async Task Test_NullParameters()
        {
            var handler = _mgr.Tools["DefaultValueTool"];
            var result = await handler.HandleAsync(null);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("default_value", result.Content[0].Text);
        }

        [TestMethod("HandleAsync - pre-cancelled token returns error")]
        public async Task Test_CancelledToken()
        {
            var handler = _mgr.Tools["AsyncTool"];
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await handler.HandleAsync(
                new Dictionary<string, object> { { "input", "test" } }, cts.Token);

            Assert.IsTrue(result.IsError);
            Assert.AreEqual("Operation was cancelled", result.Content[0].Text);
        }
    }
}
