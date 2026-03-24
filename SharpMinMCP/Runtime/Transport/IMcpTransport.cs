using System;
using System.Threading.Tasks;

namespace SharpMinMCP
{
    /// <summary>
    /// Transport layer interface. Receives raw JSON-RPC request strings,
    /// dispatches them via the handler, and sends back responses.
    /// Implementations own their own threading model.
    /// </summary>
    public interface IMcpTransport : IDisposable
    {
        /// <summary>
        /// The function that processes a JSON-RPC request string
        /// and returns a JSON-RPC response string (or null for notifications).
        /// Set by McpServer before calling Start().
        /// </summary>
        Func<string, Task<string>> RequestHandler { get; set; }

        void Start(string endpoint, int port);
        void Stop();
        bool IsRunning { get; }
    }
}
