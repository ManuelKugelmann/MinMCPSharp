using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPSharp
{
    /// <summary>
    /// Stateless JSON-RPC router for MCP protocol.
    /// Feed it a JSON-RPC request string, get back a JSON-RPC response string.
    /// </summary>
    public class McpRouter
    {
        private readonly ToolManager _tools;
        private readonly ResourceManager _resources;
        private readonly McpImplementation _serverInfo;

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public McpRouter(ToolManager tools, ResourceManager resources, McpImplementation serverInfo)
        {
            _tools = tools;
            _resources = resources;
            _serverInfo = serverInfo;
        }

        /// <summary>
        /// Process a single JSON-RPC request string. Returns null for notifications (no id).
        /// </summary>
        public async Task<string> HandleRequestAsync(string json, CancellationToken ct = default)
        {
            JObject msg;
            try
            {
                msg = JObject.Parse(json);
            }
            catch (Exception)
            {
                return Serialize(new JsonRpcError(null, -32700, "Parse error"));
            }

            var id = msg["id"];
            var method = msg["method"]?.ToString();
            var parms = msg["params"] as JObject;

            // Notifications (no id) -- acknowledge silently
            if (id == null || id.Type == JTokenType.Null)
            {
                return null;
            }

            try
            {
                object result;
                switch (method)
                {
                    case "initialize":
                        result = HandleInitialize(parms);
                        break;
                    case "tools/list":
                        result = HandleToolsList();
                        break;
                    case "tools/call":
                        result = await HandleToolCallAsync(parms, ct);
                        break;
                    case "resources/list":
                        result = HandleResourcesList();
                        break;
                    case "resources/templates/list":
                        result = new { resourceTemplates = new object[0] };
                        break;
                    case "prompts/list":
                        result = new { prompts = new object[0] };
                        break;
                    case "ping":
                        result = new { };
                        break;
                    default:
                        return Serialize(new JsonRpcError(id, -32601, "Method not found: " + method));
                }

                return Serialize(new JsonRpcResponse(id, result));
            }
            catch (Exception ex)
            {
                return Serialize(new JsonRpcError(id, -32603, ex.Message));
            }
        }

        // -- MCP method handlers ------------------------------------------

        private McpInitializeResult HandleInitialize(JObject parms)
        {
            var protocolVersion = parms?["protocolVersion"]?.ToString() ?? "2025-03-26";
            return new McpInitializeResult
            {
                ProtocolVersion = protocolVersion,
                Capabilities = new McpServerCapabilities
                {
                    Tools = new Dictionary<string, bool> { { "listChanged", true } },
                    Resources = new Dictionary<string, bool> { { "listChanged", false } }
                },
                ServerInfo = _serverInfo
            };
        }

        private McpToolsListResult HandleToolsList()
        {
            return new McpToolsListResult
            {
                Tools = _tools.Tools.Values.Select(h => h.Tool).ToList()
            };
        }

        private async Task<McpCallToolResult> HandleToolCallAsync(JObject parms, CancellationToken ct)
        {
            var name = parms?["name"]?.ToString();
            if (string.IsNullOrEmpty(name) || !_tools.Tools.TryGetValue(name, out var handler))
            {
                return new McpCallToolResult
                {
                    IsError = true,
                    Content = new[] { new McpTextContent("Tool not found: " + name) }
                };
            }

            var argsToken = parms["arguments"] as JObject;
            var args = argsToken?.ToObject<Dictionary<string, object>>()
                       ?? new Dictionary<string, object>();

            return await handler.HandleAsync(args, ct);
        }

        private McpResourcesListResult HandleResourcesList()
        {
            return new McpResourcesListResult { Resources = _resources.Resources };
        }

        // -- Serialization ------------------------------------------------

        private static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, SerializerSettings);
        }
    }
}
