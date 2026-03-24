using System.Reflection;
using UnityEngine;

namespace MCPSharp
{
    /// <summary>
    /// Unity MonoBehaviour wrapper for McpServer.
    /// Handles lifecycle (Awake/OnDestroy) and main-thread dispatch.
    /// Add to a GameObject or use McpServerBehaviour.Create().
    ///
    /// Drop-in script: copy this file and MainThreadDispatcher.cs into your
    /// Unity project alongside the MCPSharp netstandard2.0 DLL.
    /// </summary>
    [AddComponentMenu("MCPSharp/MCP Server")]
    public class McpServerBehaviour : MonoBehaviour
    {
        [Header("Server Config")]
        [SerializeField] private string serverName = "MCPSharp";
        [SerializeField] private string serverVersion = "1.0.0";
        [SerializeField] private int port = 8080;
        [SerializeField] private string endpoint = "localhost";
        [SerializeField] private bool startOnAwake = true;
        [SerializeField] private bool dispatchOnMainThread = true;

        [Header("Transport")]
        [SerializeField] private TransportType transportType = TransportType.Http;

        public enum TransportType { Http, Stdio }

        private McpServer _server;

        public McpServer Server => _server;
        public bool IsRunning => _server != null && _server.IsRunning;

        // -- Static factory -----------------------------------------------

        public static McpServerBehaviour Create(string name = "MCPSharp",
            string version = "1.0.0", int port = 8080, string endpoint = "localhost")
        {
            var go = new GameObject("[MCPSharp-Server]");
            DontDestroyOnLoad(go);
            var behaviour = go.AddComponent<McpServerBehaviour>();
            behaviour.serverName = name;
            behaviour.serverVersion = version;
            behaviour.port = port;
            behaviour.endpoint = endpoint;
            behaviour.startOnAwake = false;
            return behaviour;
        }

        // -- Builder pass-through -----------------------------------------

        public McpServerBehaviour RegisterAssembly(Assembly assembly)
        {
            EnsureServer();
            _server.RegisterAssembly(assembly);
            return this;
        }

        public McpServerBehaviour Register<T>() where T : class
        {
            EnsureServer();
            _server.Register<T>();
            return this;
        }

        public McpServerBehaviour AddTool(McpTool tool, System.Delegate func)
        {
            EnsureServer();
            _server.AddTool(tool, func);
            return this;
        }

        // -- Start / Stop -------------------------------------------------

        public void StartServer()
        {
            EnsureServer();

            if (dispatchOnMainThread)
            {
                var _ = MainThreadDispatcher.Instance;
                _server.DispatchWrapper = MainThreadDispatcher.EnqueueAsync;
            }

            IMcpTransport transport;
            switch (transportType)
            {
                case TransportType.Stdio:
                    transport = new StdioTransport();
                    break;
                default:
                    transport = new HttpListenerTransport();
                    break;
            }

            _server.Start(transport, endpoint, port);
        }

        public void StopServer()
        {
            if (_server != null)
                _server.Stop();
        }

        // -- Unity lifecycle ----------------------------------------------

        private void Awake()
        {
            EnsureServer();
            if (startOnAwake)
            {
                RegisterAssembly(Assembly.GetExecutingAssembly());
                StartServer();
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        // -- Internals ----------------------------------------------------

        private void EnsureServer()
        {
            if (_server != null) return;
            _server = new McpServer(serverName, serverVersion);
            _server.Log = (msg) => Debug.Log(msg);
        }
    }
}
