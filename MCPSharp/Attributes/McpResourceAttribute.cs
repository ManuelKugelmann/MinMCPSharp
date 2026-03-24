using System;

namespace MCPSharp
{
    [AttributeUsage(AttributeTargets.All)]
    public class McpResourceAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Uri { get; set; }
        public string MimeType { get; set; }

        public McpResourceAttribute(string name = null, string uri = null,
            string mimeType = null, string description = null)
        {
            Name = name;
            Uri = uri;
            MimeType = mimeType;
            Description = description;
        }
    }
}
