using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MinMCPSharp
{
    // -- JSON-RPC envelope ------------------------------------------------

    public class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")] public string JsonRpc = "2.0";
        [JsonProperty("id")]      public object Id;
        [JsonProperty("method")]  public string Method;
        [JsonProperty("params")]  public JObject Params;
    }

    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")] public string JsonRpc = "2.0";
        [JsonProperty("id")]      public object Id;
        [JsonProperty("result")]  public object Result;

        public JsonRpcResponse() { }
        public JsonRpcResponse(object id, object result) { Id = id; Result = result; }
    }

    public class JsonRpcError
    {
        [JsonProperty("jsonrpc")] public string JsonRpc = "2.0";
        [JsonProperty("id")]      public object Id;
        [JsonProperty("error")]   public RpcErrorDetail Error;

        public JsonRpcError() { }
        public JsonRpcError(object id, int code, string message)
        {
            Id = id;
            Error = new RpcErrorDetail { Code = code, Message = message };
        }
    }

    public class RpcErrorDetail
    {
        [JsonProperty("code")]    public int Code;
        [JsonProperty("message")] public string Message;
    }

    // -- MCP tool definition ----------------------------------------------

    public class McpTool
    {
        [JsonProperty("name")]        public string Name;
        [JsonProperty("description")] public string Description;
        [JsonProperty("inputSchema")] public McpInputSchema InputSchema;
    }

    public class McpInputSchema
    {
        [JsonProperty("type")]                 public string Type = "object";
        [JsonProperty("properties")]           public Dictionary<string, McpParameterSchema> Properties;
        [JsonProperty("required")]             public List<string> Required;
        [JsonProperty("additionalProperties")] public bool AdditionalProperties = false;
    }

    public class McpParameterSchema
    {
        [JsonProperty("type")]        public string Type;
        [JsonProperty("description")] public string Description = "";

        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        public JObject Properties;

        [JsonIgnore] public bool Required;
    }

    // -- MCP resource definition ------------------------------------------

    public class McpResource
    {
        [JsonProperty("name")]        public string Name;
        [JsonProperty("uri")]         public string Uri;
        [JsonProperty("description")] public string Description;
        [JsonProperty("mimeType")]    public string MimeType;
    }

    // -- MCP implementation info ------------------------------------------

    public class McpImplementation
    {
        [JsonProperty("name")]    public string Name = "MinMCPSharp";
        [JsonProperty("version")] public string Version = "1.0.0";

        public McpImplementation() { }
        public McpImplementation(string name, string version) { Name = name; Version = version; }
    }

    // -- MCP result types -------------------------------------------------

    public class McpTextContent
    {
        [JsonProperty("type")] public string Type = "text";
        [JsonProperty("text")] public string Text;

        public McpTextContent() { }
        public McpTextContent(string text) { Text = text; }
    }

    public class McpCallToolResult
    {
        [JsonProperty("isError")] public bool IsError;
        [JsonProperty("content")] public McpTextContent[] Content;
    }

    public class McpInitializeResult
    {
        [JsonProperty("protocolVersion")] public string ProtocolVersion;
        [JsonProperty("capabilities")]    public McpServerCapabilities Capabilities;
        [JsonProperty("serverInfo")]      public McpImplementation ServerInfo;
    }

    public class McpServerCapabilities
    {
        [JsonProperty("tools")]     public Dictionary<string, bool> Tools = new Dictionary<string, bool>();
        [JsonProperty("resources")] public Dictionary<string, bool> Resources = new Dictionary<string, bool>();
    }

    public class McpToolsListResult
    {
        [JsonProperty("tools")] public List<McpTool> Tools;
    }

    public class McpResourcesListResult
    {
        [JsonProperty("resources")] public List<McpResource> Resources;
    }
}
