using MCPSharp;
using MCPSharp.Example;

namespace MCPSharp.Test
{
    public class ResourceTestClass
    {
        [McpResource("test-resource", "test://resource", "text/plain", "A test resource")]
        public string TestResource() => "test";

        [McpResource("test-prop", "test://prop", "text/plain", "A test property")]
        public string TestProp { get; set; } = "value";
    }

    public class NoResourceClass
    {
        public string NotAResource { get; set; } = "nope";
    }

    [TestClass]
    public sealed class ResourceManagerTests
    {
        [TestMethod("Register discovers resource methods")]
        public void Test_RegisterMethods()
        {
            var mgr = new ResourceManager();
            mgr.Register<ResourceTestClass>();

            Assert.IsTrue(mgr.Resources.Any(r => r.Name == "test-resource"));
        }

        [TestMethod("Register discovers resource properties")]
        public void Test_RegisterProperties()
        {
            var mgr = new ResourceManager();
            mgr.Register<ResourceTestClass>();

            Assert.IsTrue(mgr.Resources.Any(r => r.Name == "test-prop"));
        }

        [TestMethod("Register sets all resource fields")]
        public void Test_RegisterSetsFields()
        {
            var mgr = new ResourceManager();
            mgr.Register<ResourceTestClass>();

            var resource = mgr.Resources.First(r => r.Name == "test-resource");
            Assert.AreEqual("test://resource", resource.Uri);
            Assert.AreEqual("text/plain", resource.MimeType);
            Assert.AreEqual("A test resource", resource.Description);
        }

        [TestMethod("Register ignores non-resource class")]
        public void Test_RegisterNoResources()
        {
            var mgr = new ResourceManager();
            mgr.Register<NoResourceClass>();
            Assert.AreEqual(0, mgr.Resources.Count);
        }

        [TestMethod("Register MCPDev discovers both method and property resources")]
        public void Test_RegisterMCPDev()
        {
            var mgr = new ResourceManager();
            mgr.Register<MCPDev>();

            // MCPDev has: Name method and Settings property
            Assert.AreEqual(2, mgr.Resources.Count);
            Assert.IsTrue(mgr.Resources.Any(r => r.Name == "name"));
            Assert.IsTrue(mgr.Resources.Any(r => r.Name == "settings"));
        }

        [TestMethod("Register<T> generic works same as Register(Type)")]
        public void Test_GenericRegister()
        {
            var mgr1 = new ResourceManager();
            mgr1.Register<ResourceTestClass>();

            var mgr2 = new ResourceManager();
            mgr2.Register(typeof(ResourceTestClass));

            Assert.AreEqual(mgr1.Resources.Count, mgr2.Resources.Count);
        }
    }
}
