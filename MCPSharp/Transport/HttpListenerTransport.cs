using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MCPSharp
{
    /// <summary>
    /// MCP Streamable HTTP transport using System.Net.HttpListener.
    /// Runs on a background thread. Unity-compatible (no ASP.NET).
    /// Implements the MCP 2025-03-26 Streamable HTTP spec:
    ///   POST /mcp -> JSON-RPC request -> JSON-RPC response
    /// </summary>
    public class HttpListenerTransport : IMcpTransport
    {
        private HttpListener _listener;
        private Thread _thread;
        private CancellationTokenSource _cts;

        public Func<string, Task<string>> RequestHandler { get; set; }
        public bool IsRunning { get; private set; }

        /// <summary>Optional: restrict to these Origin values. Null = localhost only.</summary>
        public string[] AllowedOrigins { get; set; }

        public void Start(string endpoint, int port)
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();

            var prefix = string.Format("http://{0}:{1}/", endpoint, port);
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            IsRunning = true;

            _thread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "MCPSharp-HTTP"
            };
            _thread.Start();
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;

            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ListenLoop()
        {
            while (IsRunning && !_cts.IsCancellationRequested)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    if (!IsRunning) break;
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                var captured = ctx;
                ThreadPool.QueueUserWorkItem(_ => HandleContext(captured));
            }
        }

        private async void HandleContext(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var resp = ctx.Response;

                // CORS preflight
                if (req.HttpMethod == "OPTIONS")
                {
                    SetCorsHeaders(resp);
                    resp.StatusCode = 204;
                    resp.Close();
                    return;
                }

                SetCorsHeaders(resp);

                // DNS rebinding protection
                if (!ValidateOrigin(req))
                {
                    resp.StatusCode = 403;
                    WriteResponse(resp, "Forbidden: invalid origin");
                    return;
                }

                // Only POST
                if (req.HttpMethod != "POST")
                {
                    resp.StatusCode = 405;
                    WriteResponse(resp, "Method not allowed. POST to /mcp.");
                    return;
                }

                // Read body
                string body;
                using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                {
                    body = reader.ReadToEnd();
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    resp.StatusCode = 400;
                    WriteResponse(resp, "Empty request body");
                    return;
                }

                if (RequestHandler == null)
                {
                    resp.StatusCode = 503;
                    WriteResponse(resp, "Server not ready");
                    return;
                }

                var result = await RequestHandler(body);

                if (result == null)
                {
                    resp.StatusCode = 202;
                    resp.Close();
                    return;
                }

                resp.StatusCode = 200;
                resp.ContentType = "application/json";
                WriteResponse(resp, result);
            }
            catch (Exception ex)
            {
                try
                {
                    ctx.Response.StatusCode = 500;
                    WriteResponse(ctx.Response, "Internal error: " + ex.Message);
                }
                catch { }
            }
        }

        private bool ValidateOrigin(HttpListenerRequest req)
        {
            var origin = req.Headers["Origin"];
            if (string.IsNullOrEmpty(origin)) return true;

            if (AllowedOrigins != null)
            {
                foreach (var allowed in AllowedOrigins)
                {
                    if (string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            if (origin.Contains("://localhost") || origin.Contains("://127.0.0.1"))
                return true;

            return false;
        }

        private static void SetCorsHeaders(HttpListenerResponse resp)
        {
            resp.Headers.Set("Access-Control-Allow-Origin", "*");
            resp.Headers.Set("Access-Control-Allow-Methods", "POST, OPTIONS");
            resp.Headers.Set("Access-Control-Allow-Headers", "Content-Type, Mcp-Session-Id");
        }

        private static void WriteResponse(HttpListenerResponse resp, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }
    }
}
