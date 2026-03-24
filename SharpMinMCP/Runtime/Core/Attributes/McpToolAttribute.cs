// Ported from MCPSharp (MIT License) - Copyright (c) Allen Frise
// Modified: Unity-compatible, Newtonsoft.Json, HttpListener transport
using System;

namespace SharpMinMCP
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class McpToolAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public McpToolAttribute(string name = null, string description = null)
        {
            Name = name;
            Description = description;
        }
    }
}
