using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MinMCPSharp
{
    /// <summary>
    /// MCP client. Connects to an MCP server via HTTP or stdio (child process).
    /// </summary>
    public class McpClient : IDisposable
    {
        private readonly string _name;
        private readonly string _version;
        private int _nextId = 1;

        // HTTP mode
        private HttpClient _http;
        private Uri _serverUri;

        // Stdio mode
        private Process _process;
        private StreamReader _stdoutReader;
        private StreamWriter _stdinWriter;

        /// <summary>Whether the client has been initialized with the server.</summary>
        public bool Initialized { get; private set; }

        /// <summary>Cached tool list from the server.</summary>
        public List<McpTool> Tools { get; private set; } = new List<McpTool>();

        /// <summary>
        /// Permission gate. Return false to block a tool call.
        /// Parameters dict contains "tool" (string) and "parameters" (dict).
        /// </summary>
        public Func<Dictionary<string, object>, bool> GetPermission = (_) => true;

        // -- Constructors -------------------------------------------------

        /// <summary>
        /// Connect to an MCP server over HTTP.
        /// </summary>
        /// <param name="name">Client name for initialization.</param>
        /// <param name="version">Client version.</param>
        /// <param name="serverUrl">Server URL, e.g. http://localhost:8080/mcp</param>
        public McpClient(string name, string version, Uri serverUrl)
        {
            _name = name;
            _version = version;
            _serverUri = serverUrl;
            _http = new HttpClient();
        }

        /// <summary>
        /// Connect to an MCP server by spawning a child process (stdio transport).
        /// </summary>
        /// <param name="name">Client name for initialization.</param>
        /// <param name="version">Client version.</param>
        /// <param name="command">Path to executable.</param>
        /// <param name="args">Command-line arguments.</param>
        /// <param name="env">Environment variables.</param>
        public McpClient(string name, string version, string command,
            string args = null, IDictionary<string, string> env = null)
        {
            _name = name;
            _version = version;

            var startInfo = new ProcessStartInfo(command, args ?? "")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (env != null)
            {
                foreach (var kv in env)
                    startInfo.EnvironmentVariables[kv.Key] = kv.Value;
            }

            _process = new Process { StartInfo = startInfo };
            _process.Start();

            _stdoutReader = _process.StandardOutput;
            _stdinWriter = _process.StandardInput;
        }

        // -- Initialization -----------------------------------------------

        /// <summary>
        /// Initialize the MCP session. Call once before other methods.
        /// </summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (Initialized) return;

            await SendRpcAsync("initialize", new
            {
                protocolVersion = "2025-03-26",
                capabilities = new
                {
                    roots = new { listChanged = false },
                    sampling = new { },
                    tools = new { listChanged = true }
                },
                clientInfo = new { name = _name, version = _version }
            }, ct);

            // Send initialized notification
            await SendNotificationAsync("notifications/initialized", ct);

            Initialized = true;
            await GetToolsAsync(ct);
        }

        // -- Tool operations ----------------------------------------------

        /// <summary>Get available tools from the server.</summary>
        public async Task<List<McpTool>> GetToolsAsync(CancellationToken ct = default)
        {
            var result = await SendRpcAsync("tools/list", null, ct);
            var toolsArray = result?["tools"] as JArray
                ?? throw new McpClientException(
                    "Server returned invalid tools/list response: missing 'tools' array", -32603);
            Tools = toolsArray.ToObject<List<McpTool>>();
            return Tools;
        }

        /// <summary>Call a tool by name with parameters.</summary>
        public async Task<McpCallToolResult> CallToolAsync(string name,
            Dictionary<string, object> parameters = null, CancellationToken ct = default)
        {
            parameters = parameters ?? new Dictionary<string, object>();

            if (!GetPermission(new Dictionary<string, object>
                { ["tool"] = name, ["parameters"] = parameters }))
            {
                return new McpCallToolResult
                {
                    IsError = true,
                    Content = new[] { new McpTextContent("Permission Denied.") }
                };
            }

            var result = await SendRpcAsync("tools/call", new
            {
                name = name,
                arguments = parameters
            }, ct);

            return result.ToObject<McpCallToolResult>()
                ?? throw new McpClientException(
                    "Failed to deserialize tools/call response", -32603);
        }

        // -- Resource operations ------------------------------------------

        /// <summary>Get available resources.</summary>
        public async Task<McpResourcesListResult> GetResourcesAsync(CancellationToken ct = default)
        {
            var result = await SendRpcAsync("resources/list", null, ct);
            return result.ToObject<McpResourcesListResult>()
                ?? throw new McpClientException(
                    "Failed to deserialize resources/list response", -32603);
        }

        // -- Prompt operations --------------------------------------------

        /// <summary>Get available prompts.</summary>
        public async Task<JObject> GetPromptListAsync(CancellationToken ct = default)
        {
            var result = await SendRpcAsync("prompts/list", null, ct);
            return result as JObject
                ?? throw new McpClientException(
                    "Failed to deserialize prompts/list response", -32603);
        }

        // -- Ping ---------------------------------------------------------

        /// <summary>Ping the server.</summary>
        public async Task SendPingAsync(CancellationToken ct = default)
        {
            await SendRpcAsync("ping", null, ct);
        }

        // -- JSON-RPC transport -------------------------------------------

        private async Task<JToken> SendRpcAsync(string method, object parms,
            CancellationToken ct = default)
        {
            var id = Interlocked.Increment(ref _nextId);
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method
            };
            if (parms != null)
                request["params"] = JToken.FromObject(parms);

            var responseJson = _http != null
                ? await SendHttpAsync(request, ct)
                : await SendStdioAsync(request, ct);

            if (responseJson == null)
                throw new McpClientException(
                    "No response received from server for method: " + method, -32603);

            var response = JObject.Parse(responseJson);

            if (response["error"] != null)
            {
                throw new McpClientException(
                    response["error"]["message"]?.ToString() ?? "Unknown error",
                    (int)(response["error"]["code"] ?? -32603));
            }

            return response["result"];
        }

        private async Task SendNotificationAsync(string method, CancellationToken ct = default)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method
            };

            if (_http != null)
                await SendHttpAsync(request, ct);
            else
                await SendStdioAsync(request, ct);
        }

        private async Task<string> SendHttpAsync(JObject request, CancellationToken ct)
        {
            var json = request.ToString(Formatting.None);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(_serverUri, content, ct);

            if ((int)response.StatusCode == 202)
                return null; // notification accepted

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private async Task<string> SendStdioAsync(JObject request, CancellationToken ct)
        {
            var json = request.ToString(Formatting.None);
            await _stdinWriter.WriteLineAsync(json);
            await _stdinWriter.FlushAsync();

            // For notifications (no id), don't wait for response
            if (request["id"] == null)
                return null;

            // Read response line
            var line = await _stdoutReader.ReadLineAsync();
            if (line == null)
                throw new McpClientException(
                    "Server process closed stdout unexpectedly", -32603);
            return line;
        }

        // -- Dispose ------------------------------------------------------

        public void Dispose()
        {
            _http?.Dispose();
            _http = null;

            if (_process != null)
            {
                try { _process.Kill(); } catch { }
                try { _process.WaitForExit(3000); } catch { }
                _process.Dispose();
                _process = null;
            }
        }
    }

    /// <summary>
    /// Exception thrown when the MCP server returns a JSON-RPC error.
    /// </summary>
    public class McpClientException : Exception
    {
        public int ErrorCode { get; }
        public McpClientException(string message, int code) : base(message)
        {
            ErrorCode = code;
        }
    }
}
