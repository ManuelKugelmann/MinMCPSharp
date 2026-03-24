// Ported from MCPSharp (MIT License) - Copyright (c) Allen Frise
// Replaced: System.Text.Json.JsonElement → Newtonsoft.Json.Linq.JToken
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SharpMinMCP
{
    public class ToolHandler
    {
        public readonly McpTool Tool;
        private readonly MethodInfo _method;

        public ToolHandler(McpTool tool, MethodInfo method)
        {
            Tool = tool;
            _method = method;
        }

        public async Task<McpCallToolResult> HandleAsync(
            Dictionary<string, object> parameters,
            CancellationToken ct = default)
        {
            try
            {
                var args = new List<object>();

                foreach (var param in _method.GetParameters())
                {
                    if (parameters != null && parameters.TryGetValue(param.Name, out var value))
                    {
                        // JToken → target type
                        if (value is JToken jt)
                            value = jt.ToObject(param.ParameterType);
                        else if (value != null && value.GetType() != param.ParameterType)
                            value = Convert.ChangeType(value, param.ParameterType);

                        args.Add(value);
                    }
                    else
                    {
                        // Default or null
                        args.Add(param.HasDefaultValue
                            ? param.DefaultValue
                            : param.ParameterType.IsValueType
                                ? Activator.CreateInstance(param.ParameterType)
                                : null);
                    }
                }

                if (ct.IsCancellationRequested)
                    return ErrorResult("Operation was cancelled");

                // Invoke — supports static and instance methods
                object instance = _method.IsStatic ? null : Activator.CreateInstance(_method.DeclaringType);
                var result = _method.Invoke(instance, args.ToArray());

                // Unwrap Task<T>
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    var resultProp = task.GetType().GetProperty("Result");
                    result = resultProp?.GetValue(task);
                }

                if (ct.IsCancellationRequested)
                    return ErrorResult("Operation was cancelled");

                return ToResult(result);
            }
            catch (Exception ex)
            {
                var e = ex is TargetInvocationException tie ? tie.InnerException ?? tie : ex;
                return new McpCallToolResult
                {
                    IsError = true,
                    Content = new[]
                    {
                        new McpTextContent(e.Message),
                        new McpTextContent("StackTrace:\n" + e.StackTrace)
                    }
                };
            }
        }

        private static McpCallToolResult ToResult(object result)
        {
            if (result == null)
                return ErrorResult("null");

            string text;
            if (result is string s)
                text = s;
            else if (result is string[] arr)
                text = string.Join("\n", arr);
            else
                text = JsonConvert.SerializeObject(result);

            return new McpCallToolResult
            {
                Content = new[] { new McpTextContent(text) }
            };
        }

        private static McpCallToolResult ErrorResult(string msg)
        {
            return new McpCallToolResult
            {
                IsError = true,
                Content = new[] { new McpTextContent(msg) }
            };
        }
    }
}
