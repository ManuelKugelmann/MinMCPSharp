using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace MCPSharp
{
    /// <summary>
    /// Maps C# types to JSON Schema.
    /// </summary>
    internal static class JsonSchemaGenerator
    {
        public static string GetJsonType(Type t)
        {
            if (t == typeof(string) || t == typeof(DateTime)) return "string";
            if (t == typeof(bool)) return "boolean";
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte))
                return "integer";
            if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
                return "number";
            if (t.IsArray || typeof(IEnumerable).IsAssignableFrom(t) && t != typeof(string))
                return "array";
            if (t.IsEnum) return "string";
            return "object";
        }

        /// <summary>
        /// Generate a JObject JSON Schema for a complex type's public properties.
        /// Used when a tool parameter is a class/struct (type == "object").
        /// </summary>
        public static JObject GenerateObjectSchema(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var jsonType = GetJsonType(type);
            if (jsonType != "object") return null;

            var props = new JObject();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propSchema = new JObject();
                propSchema["type"] = GetJsonType(prop.PropertyType);

                // Recurse for nested objects
                if (GetJsonType(prop.PropertyType) == "object" && prop.PropertyType != type)
                {
                    var nested = GenerateObjectSchema(prop.PropertyType);
                    if (nested != null)
                    {
                        foreach (var kv in nested)
                            propSchema[kv.Key] = kv.Value;
                    }
                }

                // Array element type
                if (GetJsonType(prop.PropertyType) == "array")
                {
                    var elemType = prop.PropertyType.IsArray
                        ? prop.PropertyType.GetElementType()
                        : prop.PropertyType.GetGenericArguments().FirstOrDefault();
                    if (elemType != null)
                    {
                        var items = new JObject();
                        items["type"] = GetJsonType(elemType);
                        propSchema["items"] = items;
                    }
                }

                props[prop.Name] = propSchema;
            }

            return props;
        }
    }
}
