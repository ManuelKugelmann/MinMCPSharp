using MCPSharp;

namespace MCPSharp.Test
{
    // Test tool classes for ToolManager tests
    public class ToolManagerTestTools
    {
        [McpTool("test-tool", "A test tool")]
        public static string TestTool([McpParameter(true, "the input")] string input) => input;

        [McpTool]
        public static string DefaultNameTool() => "default";

        [McpTool("optional-params", "Tool with optional params")]
        public static string OptionalParams(
            [McpParameter(true)] string required,
            string optional = "default") => $"{required}-{optional}";
    }

    public class NoToolClass
    {
        public static string NotATool() => "nope";
    }

    [TestClass]
    public sealed class ToolManagerTests
    {
        [TestMethod("Register discovers McpTool methods")]
        public void Test_Register_DiscoversMethods()
        {
            var mgr = new ToolManager();
            mgr.Register<ToolManagerTestTools>();

            Assert.IsTrue(mgr.Tools.ContainsKey("test-tool"));
            Assert.IsTrue(mgr.Tools.ContainsKey("DefaultNameTool"));
            Assert.IsTrue(mgr.Tools.ContainsKey("optional-params"));
            Assert.AreEqual(3, mgr.Tools.Count);
        }

        [TestMethod("Register ignores classes without McpTool")]
        public void Test_Register_IgnoresNonTools()
        {
            var mgr = new ToolManager();
            mgr.Register<NoToolClass>();
            Assert.AreEqual(0, mgr.Tools.Count);
        }

        [TestMethod("Register sets tool name from attribute")]
        public void Test_Register_ToolName()
        {
            var mgr = new ToolManager();
            mgr.Register<ToolManagerTestTools>();

            var tool = mgr.Tools["test-tool"].Tool;
            Assert.AreEqual("test-tool", tool.Name);
            Assert.AreEqual("A test tool", tool.Description);
        }

        [TestMethod("Register defaults tool name to method name")]
        public void Test_Register_DefaultToolName()
        {
            var mgr = new ToolManager();
            mgr.Register<ToolManagerTestTools>();

            Assert.IsTrue(mgr.Tools.ContainsKey("DefaultNameTool"));
        }

        [TestMethod("Register builds parameter schema with required flag")]
        public void Test_Register_ParameterSchema()
        {
            var mgr = new ToolManager();
            mgr.Register<ToolManagerTestTools>();

            var tool = mgr.Tools["test-tool"].Tool;
            Assert.IsNotNull(tool.InputSchema.Properties);
            Assert.IsTrue(tool.InputSchema.Properties.ContainsKey("input"));
            Assert.AreEqual("string", tool.InputSchema.Properties["input"].Type);
            Assert.AreEqual("the input", tool.InputSchema.Properties["input"].Description);
            Assert.IsTrue(tool.InputSchema.Required.Contains("input"));
        }

        [TestMethod("Register distinguishes required and optional params")]
        public void Test_Register_RequiredVsOptional()
        {
            var mgr = new ToolManager();
            mgr.Register<ToolManagerTestTools>();

            var tool = mgr.Tools["optional-params"].Tool;
            Assert.IsTrue(tool.InputSchema.Required.Contains("required"));
            Assert.IsFalse(tool.InputSchema.Required.Contains("optional"));
        }

        [TestMethod("AddToolHandler registers dynamic tool")]
        public void Test_AddToolHandler()
        {
            var mgr = new ToolManager();
            var tool = new McpTool
            {
                Name = "dynamic",
                Description = "dynamic tool",
                InputSchema = new McpInputSchema
                {
                    Properties = new Dictionary<string, McpParameterSchema>()
                }
            };
            mgr.AddToolHandler(tool, (string x) => x);

            Assert.IsTrue(mgr.Tools.ContainsKey("dynamic"));
        }

        [TestMethod("AddToolHandler fires OnToolsChanged")]
        public void Test_AddToolHandler_FiresCallback()
        {
            var mgr = new ToolManager();
            bool fired = false;
            mgr.OnToolsChanged = () => fired = true;

            var tool = new McpTool
            {
                Name = "dyn",
                InputSchema = new McpInputSchema
                {
                    Properties = new Dictionary<string, McpParameterSchema>()
                }
            };
            mgr.AddToolHandler(tool, () => "hi");

            Assert.IsTrue(fired);
        }

        [TestMethod("Register handles complex object parameter")]
        public void Test_Register_ComplexObjectParam()
        {
            var mgr = new ToolManager();
            mgr.Register<MCPSharp.Example.MCPDev>();

            var tool = mgr.Tools["AddComplex"].Tool;
            var objParam = tool.InputSchema.Properties["obj"];
            Assert.AreEqual("object", objParam.Type);
            Assert.IsNotNull(objParam.Properties);
        }
    }
}
