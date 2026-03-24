using System;
using System.Collections.Generic;
using System.Reflection;

namespace MCPSharp
{
    internal class ResourceManager
    {
        public readonly List<McpResource> Resources = new List<McpResource>();

        public void Register(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static |
                                                    BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attr = method.GetCustomAttribute<McpResourceAttribute>();
                if (attr == null) continue;

                Resources.Add(new McpResource
                {
                    Name = attr.Name,
                    Description = attr.Description,
                    Uri = attr.Uri,
                    MimeType = attr.MimeType
                });
            }

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static |
                                                     BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attr = prop.GetCustomAttribute<McpResourceAttribute>();
                if (attr == null) continue;

                Resources.Add(new McpResource
                {
                    Name = attr.Name,
                    Description = attr.Description,
                    Uri = attr.Uri,
                    MimeType = attr.MimeType
                });
            }
        }

        public void Register<T>() where T : class
        {
            Register(typeof(T));
        }
    }
}
