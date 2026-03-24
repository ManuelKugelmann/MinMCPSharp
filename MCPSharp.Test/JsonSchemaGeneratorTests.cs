using MCPSharp;
using Newtonsoft.Json.Linq;

namespace MCPSharp.Test
{
    // Test types for JsonSchemaGenerator
    public class SimpleObject
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    public class NestedObject
    {
        public string Label { get; set; } = "";
        public SimpleObject Child { get; set; }
    }

    public class ObjectWithArrays
    {
        public string[] Tags { get; set; } = [];
        public int[] Numbers { get; set; } = [];
        public List<string> Items { get; set; } = new();
    }

    public enum Color { Red, Green, Blue }

    public class ObjectWithEnum
    {
        public Color Favorite { get; set; }
    }

    [TestClass]
    public sealed class JsonSchemaGeneratorTests
    {
        // -- GetJsonType tests ------------------------------------------------

        [TestMethod("GetJsonType - string")]
        public void Test_GetJsonType_String()
        {
            Assert.AreEqual("string", JsonSchemaGenerator.GetJsonType(typeof(string)));
        }

        [TestMethod("GetJsonType - DateTime")]
        public void Test_GetJsonType_DateTime()
        {
            Assert.AreEqual("string", JsonSchemaGenerator.GetJsonType(typeof(DateTime)));
        }

        [TestMethod("GetJsonType - bool")]
        public void Test_GetJsonType_Bool()
        {
            Assert.AreEqual("boolean", JsonSchemaGenerator.GetJsonType(typeof(bool)));
        }

        [TestMethod("GetJsonType - int")]
        public void Test_GetJsonType_Int()
        {
            Assert.AreEqual("integer", JsonSchemaGenerator.GetJsonType(typeof(int)));
        }

        [TestMethod("GetJsonType - long")]
        public void Test_GetJsonType_Long()
        {
            Assert.AreEqual("integer", JsonSchemaGenerator.GetJsonType(typeof(long)));
        }

        [TestMethod("GetJsonType - short")]
        public void Test_GetJsonType_Short()
        {
            Assert.AreEqual("integer", JsonSchemaGenerator.GetJsonType(typeof(short)));
        }

        [TestMethod("GetJsonType - byte")]
        public void Test_GetJsonType_Byte()
        {
            Assert.AreEqual("integer", JsonSchemaGenerator.GetJsonType(typeof(byte)));
        }

        [TestMethod("GetJsonType - float")]
        public void Test_GetJsonType_Float()
        {
            Assert.AreEqual("number", JsonSchemaGenerator.GetJsonType(typeof(float)));
        }

        [TestMethod("GetJsonType - double")]
        public void Test_GetJsonType_Double()
        {
            Assert.AreEqual("number", JsonSchemaGenerator.GetJsonType(typeof(double)));
        }

        [TestMethod("GetJsonType - decimal")]
        public void Test_GetJsonType_Decimal()
        {
            Assert.AreEqual("number", JsonSchemaGenerator.GetJsonType(typeof(decimal)));
        }

        [TestMethod("GetJsonType - array")]
        public void Test_GetJsonType_Array()
        {
            Assert.AreEqual("array", JsonSchemaGenerator.GetJsonType(typeof(string[])));
            Assert.AreEqual("array", JsonSchemaGenerator.GetJsonType(typeof(int[])));
        }

        [TestMethod("GetJsonType - List<T>")]
        public void Test_GetJsonType_GenericList()
        {
            Assert.AreEqual("array", JsonSchemaGenerator.GetJsonType(typeof(List<string>)));
        }

        [TestMethod("GetJsonType - enum")]
        public void Test_GetJsonType_Enum()
        {
            Assert.AreEqual("string", JsonSchemaGenerator.GetJsonType(typeof(Color)));
        }

        [TestMethod("GetJsonType - object/class")]
        public void Test_GetJsonType_Object()
        {
            Assert.AreEqual("object", JsonSchemaGenerator.GetJsonType(typeof(SimpleObject)));
        }

        // -- GenerateObjectSchema tests ---------------------------------------

        [TestMethod("GenerateObjectSchema - null returns null")]
        public void Test_GenerateObjectSchema_Null()
        {
            Assert.IsNull(JsonSchemaGenerator.GenerateObjectSchema(null));
        }

        [TestMethod("GenerateObjectSchema - non-object returns null")]
        public void Test_GenerateObjectSchema_NonObject()
        {
            Assert.IsNull(JsonSchemaGenerator.GenerateObjectSchema(typeof(string)));
            Assert.IsNull(JsonSchemaGenerator.GenerateObjectSchema(typeof(int)));
        }

        [TestMethod("GenerateObjectSchema - simple object")]
        public void Test_GenerateObjectSchema_SimpleObject()
        {
            var schema = JsonSchemaGenerator.GenerateObjectSchema(typeof(SimpleObject));
            Assert.IsNotNull(schema);
            Assert.AreEqual("string", schema["Name"]!["type"]!.ToString());
            Assert.AreEqual("integer", schema["Count"]!["type"]!.ToString());
        }

        [TestMethod("GenerateObjectSchema - nested object")]
        public void Test_GenerateObjectSchema_NestedObject()
        {
            var schema = JsonSchemaGenerator.GenerateObjectSchema(typeof(NestedObject));
            Assert.IsNotNull(schema);
            Assert.AreEqual("string", schema["Label"]!["type"]!.ToString());
            Assert.AreEqual("object", schema["Child"]!["type"]!.ToString());
        }

        [TestMethod("GenerateObjectSchema - arrays with items")]
        public void Test_GenerateObjectSchema_ArrayProperties()
        {
            var schema = JsonSchemaGenerator.GenerateObjectSchema(typeof(ObjectWithArrays));
            Assert.IsNotNull(schema);

            Assert.AreEqual("array", schema["Tags"]!["type"]!.ToString());
            Assert.AreEqual("string", schema["Tags"]!["items"]!["type"]!.ToString());

            Assert.AreEqual("array", schema["Numbers"]!["type"]!.ToString());
            Assert.AreEqual("integer", schema["Numbers"]!["items"]!["type"]!.ToString());

            Assert.AreEqual("array", schema["Items"]!["type"]!.ToString());
            Assert.AreEqual("string", schema["Items"]!["items"]!["type"]!.ToString());
        }

        [TestMethod("GenerateObjectSchema - enum property")]
        public void Test_GenerateObjectSchema_EnumProperty()
        {
            var schema = JsonSchemaGenerator.GenerateObjectSchema(typeof(ObjectWithEnum));
            Assert.IsNotNull(schema);
            Assert.AreEqual("string", schema["Favorite"]!["type"]!.ToString());
        }
    }
}
