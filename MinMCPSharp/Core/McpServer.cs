using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MinMCPSharp
{
    /// <summary>
    /// Plain C# MCP server. No framework dependencies.
    /// Builder: new McpServer("name","1.0").Register&lt;T&gt;().Start();
    /// </summary>
    public class McpServer : IDisposable
    {
        private readonly ToolManager _tools = new ToolManager();
        private readonly ResourceManager _resources = new ResourceManager();
        private readonly McpRouter _router;
        private IMcpTransport _transport;

        public string ServerName { get; private set; }
        public string ServerVersion { get; private set; }
        public bool IsRunning => _transport != null && _transport.IsRunning;

        /// <summary>Log callback. Default: Console.WriteLine.</summary>
        public Action<string> Log = (msg) => Console.WriteLine(msg);

        /// <summary>
        /// Optional request wrapper for thread dispatch.
        /// Default: null (direct execution on transport thread).
        /// Unity layer sets this to MainThreadDispatcher.EnqueueAsync.
        /// </summary>
        public Func<Func<Task<string>>, Task<string>> DispatchWrapper;

        /// <summary>
        /// Optional briefing returned as the MCP `instructions` field of the
        /// initialize result. Evaluated per initialize request (runs inside
        /// DispatchWrapper like all requests). Null: field omitted.
        /// </summary>
        public Func<string> Instructions;

        public McpServer(string name = "MinMCPSharp", string version = "1.0.0")
        {
            ServerName = name;
            ServerVersion = version;
            _router = new McpRouter(_tools, _resources,
                new McpImplementation(name, version),
                () => Instructions != null ? Instructions() : null);
        }

        // -- Registration -------------------------------------------------

        /// <summary>Scan assembly for [McpTool] and [McpResource] types.</summary>
        public McpServer RegisterAssembly(Assembly assembly)
        {
            var types = assembly.GetTypes().Where(t =>
                t.GetMethods(BindingFlags.Public | BindingFlags.Static |
                             BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Any(m => m.GetCustomAttribute<McpToolAttribute>() != null ||
                              m.GetCustomAttribute<McpResourceAttribute>() != null) ||
                t.GetProperties(BindingFlags.Public | BindingFlags.Static |
                                BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Any(p => p.GetCustomAttribute<McpResourceAttribute>() != null)
            );

            foreach (var type in types)
            {
                _tools.Register(type);
                _resources.Register(type);
                Log("[MinMCPSharp] Registered: " + type.Name);
            }

            return this;
        }

        /// <summary>Register a specific type.</summary>
        public McpServer Register<T>() where T : class
        {
            _tools.Register<T>();
            _resources.Register<T>();
            return this;
        }

        /// <summary>Register a dynamic tool.</summary>
        public McpServer AddTool(McpTool tool, Delegate func)
        {
            _tools.AddToolHandler(tool, func);
            return this;
        }

        // -- Lifecycle ----------------------------------------------------

        /// <summary>Start with a specific transport.</summary>
        public McpServer Start(IMcpTransport transport,
            string endpoint = "localhost", int port = 8080)
        {
            if (_transport != null && _transport.IsRunning)
            {
                Log("[MinMCPSharp] Already running");
                return this;
            }

            _transport = transport;
            WireTransport();
            _transport.Start(endpoint, port);
            Log(string.Format("[MinMCPSharp] Started ({0}) on {1}:{2}",
                _transport.GetType().Name, endpoint, port));
            return this;
        }

        /// <summary>Start with HttpListenerTransport on given port.</summary>
        public McpServer Start(string endpoint = "localhost", int port = 8080)
        {
            return Start(new HttpListenerTransport(), endpoint, port);
        }

        public void Stop()
        {
            if (_transport != null)
            {
                _transport.Stop();
                _transport.Dispose();
                _transport = null;
                Log("[MinMCPSharp] Stopped");
            }
        }

        public void Dispose()
        {
            Stop();
        }

        // -- Blocking entry points ----------------------------------------

        /// <summary>
        /// Start server and block until cancelled.
        /// For console apps, CLI tools, or any long-running host.
        /// </summary>
        public async Task RunAsync(IMcpTransport transport = null,
            string endpoint = "localhost", int port = 8080,
            CancellationToken ct = default)
        {
            Start(transport ?? new HttpListenerTransport(), endpoint, port);
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                Stop();
            }
        }

        /// <summary>
        /// Start with stdio transport and block until stdin closes.
        /// For MCP clients that launch the server as a child process.
        /// </summary>
        public async Task RunStdioAsync(CancellationToken ct = default)
        {
            await RunAsync(new StdioTransport(), "stdio", 0, ct);
        }

        // -- Internals ----------------------------------------------------

        private void WireTransport()
        {
            if (DispatchWrapper != null)
            {
                _transport.RequestHandler = (json) =>
                    DispatchWrapper(() => _router.HandleRequestAsync(json));
            }
            else
            {
                _transport.RequestHandler = (json) =>
                    _router.HandleRequestAsync(json);
            }
        }
    }
}
