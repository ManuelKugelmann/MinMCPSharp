using System;

namespace MCPSharp
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class McpParameterAttribute : Attribute
    {
        public bool Required { get; set; }
        public string Description { get; set; }

        public McpParameterAttribute(bool required = false, string description = null)
        {
            Required = required;
            Description = description;
        }
    }
}
