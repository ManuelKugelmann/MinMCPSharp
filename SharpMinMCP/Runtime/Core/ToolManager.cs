// Ported from MCPSharp (MIT License) - Copyright (c) Allen Frise
// Removed: SemanticKernel, Microsoft.Extensions.AI
// Uses: Newtonsoft.Json, manual JSON Schema gen
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace SharpMinMCP
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
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static |
                                                    BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attr = method.GetCustomAttribute<McpToolAttribute>();
                if (attr == null) continue;

                var name = attr.Name ?? method.Name;
                var description = attr.Description
                    ?? method.GetXmlDocumentation()
                    ?? method.GetCustomAttribute<DescriptionAttribute>()?.Description
                    ?? "";

                var paramSchemas = new Dictionary<string, McpParameterSchema>();
                var required = new List<string>();

                foreach (var param in method.GetParameters())
                {
                    var schema = BuildParameterSchema(param);
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

        private McpParameterSchema BuildParameterSchema(ParameterInfo param)
        {
            var jsonType = JsonSchemaGenerator.GetJsonType(param.ParameterType);

            // Check [McpParameter] attribute
            var mcpAttr = param.GetCustomAttribute<McpParameterAttribute>();
            var descAttr = param.GetCustomAttribute<DescriptionAttribute>();
            var xmlDoc = param.GetXmlDocumentation();

            var schema = new McpParameterSchema
            {
                Type = jsonType,
                Description = mcpAttr?.Description ?? xmlDoc ?? descAttr?.Description ?? "",
                Required = mcpAttr?.Required ?? false,
                Properties = jsonType == "object"
                    ? JsonSchemaGenerator.GenerateObjectSchema(param.ParameterType)
                    : null
            };

            return schema;
        }
    }
}
