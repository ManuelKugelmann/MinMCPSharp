// Ported from MCPSharp (MIT License) - Copyright (c) Allen Frise
// Unchanged — no external deps
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace SharpMinMCP
{
    internal static class XmlDocumentationExtensions
    {
        public static string GetXmlDocumentation(this MemberInfo member)
        {
            try
            {
                var assemblyName = member.DeclaringType?.Assembly.GetName().Name;
                var xmlPath = Path.Combine(AppContext.BaseDirectory, assemblyName + ".xml");
                if (!File.Exists(xmlPath)) return null;

                var doc = XDocument.Load(xmlPath);
                string memberName;

                if (member is MethodInfo method)
                    memberName = GetMethodString(method);
                else if (member is PropertyInfo property)
                    memberName = "P:" + property.DeclaringType?.FullName + "." + property.Name;
                else if (member is FieldInfo field)
                    memberName = "F:" + field.DeclaringType?.FullName + "." + field.Name;
                else
                    memberName = "T:" + member.DeclaringType?.FullName;

                return doc.Descendants("member")
                    .FirstOrDefault(m => m.Attribute("name")?.Value == memberName)
                    ?.Element("summary")?.Value.Trim();
            }
            catch { return null; }
        }

        public static string GetXmlDocumentation(this ParameterInfo parameter)
        {
            try
            {
                var assemblyName = parameter.Member.DeclaringType?.Assembly.GetName().Name;
                var xmlPath = Path.Combine(AppContext.BaseDirectory, assemblyName + ".xml");
                if (!File.Exists(xmlPath)) return null;

                var doc = XDocument.Load(xmlPath);
                var method = parameter.Member as MethodInfo;
                if (method == null) return null;

                return doc.Descendants("member")
                    .FirstOrDefault(m => m.Attribute("name")?.Value == GetMethodString(method))
                    ?.Elements("param")
                    .FirstOrDefault(p => p.Attribute("name")?.Value == parameter.Name)
                    ?.Value.Trim();
            }
            catch { return null; }
        }

        private static string GetMethodString(MethodInfo method)
        {
            var parms = method.GetParameters();
            var paramStr = parms.Length == 0
                ? ""
                : "(" + string.Join(",", parms.Select(p => p.ParameterType.FullName)) + ")";
            return "M:" + method.DeclaringType?.FullName + "." + method.Name + paramStr;
        }
    }
}
