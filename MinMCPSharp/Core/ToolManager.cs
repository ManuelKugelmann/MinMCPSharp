using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace MinMCPSharp
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

                // Description priority: attribute > [Description]
                string description = attr.Description;
                if (string.IsNullOrEmpty(description))
                    description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

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

            var mcpAttr = param.GetCustomAttribute<McpParameterAttribute>();
            var descAttr = param.GetCustomAttribute<DescriptionAttribute>();

            // Parameter description: attribute > [Description]
            string paramDoc = mcpAttr?.Description;
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
    }
}
