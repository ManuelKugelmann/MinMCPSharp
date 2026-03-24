using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using LoxSmoke.DocXml;

namespace MCPSharp
{
    internal class ToolManager
    {
        public readonly Dictionary<string, ToolHandler> Tools =
            new Dictionary<string, ToolHandler>();

        public Action OnToolsChanged = () => { };

        /// <summary>
        /// Scan a type for [McpTool] methods and register them.
        /// </summary>
        public void Register(Type type)
        {
            DocXmlReader docReader = null;
            try { docReader = new DocXmlReader(type.Assembly); }
            catch { /* XML docs may not be available */ }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static |
                                                    BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attr = method.GetCustomAttribute<McpToolAttribute>();
                if (attr == null) continue;

                var name = attr.Name ?? method.Name;

                // Description priority: attribute > XML doc > [Description] attribute
                string description = attr.Description;
                if (string.IsNullOrEmpty(description) && docReader != null)
                {
                    try { description = docReader.GetMethodComments(method)?.Summary; }
                    catch { }
                }
                if (string.IsNullOrEmpty(description))
                    description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

                var paramSchemas = new Dictionary<string, McpParameterSchema>();
                var required = new List<string>();

                var methodComments = docReader != null
                    ? TryGetMethodComments(docReader, method)
                    : null;

                foreach (var param in method.GetParameters())
                {
                    var schema = BuildParameterSchema(param, methodComments);
                    paramSchemas[param.Name] = schema;
                    if (schema.Required)
                        required.Add(param.Name);
                }

                Tools[name] = new ToolHandler(
                    new McpTool
                    {
                        Name = name,
                        Description = description,
                        InputSchema = new McpInputSchema
                        {
                            Properties = paramSchemas,
                            Required = required
                        }
                    },
                    method
                );
            }
        }

        public void Register<T>() where T : class
        {
            Register(typeof(T));
        }

        public void AddToolHandler(McpTool tool, Delegate func)
        {
            Tools[tool.Name] = new ToolHandler(tool, func.Method);
            OnToolsChanged();
        }

        private McpParameterSchema BuildParameterSchema(ParameterInfo param, MethodComments methodComments)
        {
            var jsonType = JsonSchemaGenerator.GetJsonType(param.ParameterType);

            var mcpAttr = param.GetCustomAttribute<McpParameterAttribute>();
            var descAttr = param.GetCustomAttribute<DescriptionAttribute>();

            // Parameter description: attribute > XML doc > [Description]
            string paramDoc = mcpAttr?.Description;
            if (string.IsNullOrEmpty(paramDoc) && methodComments != null)
            {
                var xmlParam = methodComments.Parameters
                    .FirstOrDefault(p => p.Name == param.Name);
                if (xmlParam.Name != null)
                    paramDoc = xmlParam.Text;
            }
            if (string.IsNullOrEmpty(paramDoc))
                paramDoc = descAttr?.Description ?? "";

            var schema = new McpParameterSchema
            {
                Type = jsonType,
                Description = paramDoc,
                Required = mcpAttr?.Required ?? false,
                Properties = jsonType == "object"
                    ? JsonSchemaGenerator.GenerateObjectSchema(param.ParameterType)
                    : null
            };

            return schema;
        }

        private static MethodComments TryGetMethodComments(DocXmlReader reader, MethodInfo method)
        {
            try { return reader.GetMethodComments(method); }
            catch { return null; }
        }
    }
}
