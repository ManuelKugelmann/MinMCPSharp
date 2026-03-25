using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MinMCPSharp
{
    /// <summary>
    /// MCP Streamable HTTP transport using System.Net.HttpListener.
    /// Runs on a background thread. Unity-compatible (no ASP.NET).
    /// Implements the MCP 2025-03-26 Streamable HTTP spec:
    ///   POST /mcp -> JSON-RPC request -> JSON-RPC response
    ///
    /// Platform support:
    ///   - Windows desktop (Mono and IL2CPP): fully supported
    ///   - macOS desktop (Mono and IL2CPP): fully supported
    ///   - Linux: fully supported
    ///   - Mobile / WebGL: HttpListener is not available.
    ///     A future TcpListenerTransport with raw HTTP parsing
    ///     would cover these platforms if needed.
    /// </summary>
    public class HttpListenerTransport : IMcpTransport
    {
        private HttpListener _listener;
        private Thread _thread;
        private CancellationTokenSource _cts;
        private CidrRule[] _clientRules;

        public Func<string, Task<string>> RequestHandler { get; set; }
        public bool IsRunning { get; private set; }

        /// <summary>Optional: restrict to these Origin values. Null = localhost only.</summary>
        public string[] AllowedOrigins { get; set; }

        /// <summary>
        /// Optional: restrict connecting client IPs. Supports individual IPs and CIDR notation.
        /// Examples: "192.168.1.10", "192.168.1.0/24", "10.0.0.0/8".
        /// Null or empty = no client IP filtering (rely on bind address for access control).
        /// Loopback (127.0.0.1, ::1) is always allowed.
        /// </summary>
        public string[] AllowedClients
        {
            get => _allowedClientsRaw;
            set
            {
                _allowedClientsRaw = value;
                _clientRules = CidrRule.Parse(value);
            }
        }
        private string[] _allowedClientsRaw;

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
                Name = "MinMCPSharp-HTTP"
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

                // Client IP filtering
                if (!ValidateClient(req.RemoteEndPoint))
                {
                    resp.StatusCode = 403;
                    WriteResponse(resp, "Forbidden: client IP not allowed");
                    return;
                }

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

        private bool ValidateClient(IPEndPoint remoteEndPoint)
        {
            if (_clientRules == null || _clientRules.Length == 0)
                return true;

            if (remoteEndPoint == null)
                return false;

            var addr = remoteEndPoint.Address;

            // Loopback is always allowed
            if (IPAddress.IsLoopback(addr))
                return true;

            foreach (var rule in _clientRules)
            {
                if (rule.Contains(addr))
                    return true;
            }

            return false;
        }

        private static void WriteResponse(HttpListenerResponse resp, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.Close();
        }
    }

    /// <summary>
    /// Represents a CIDR rule (single IP or IP/prefix) for client filtering.
    /// IPv4 only. Supports "192.168.1.10" or "192.168.1.0/24" notation.
    /// </summary>
    internal struct CidrRule
    {
        private uint _network;
        private uint _mask;

        public CidrRule(uint network, uint mask)
        {
            _network = network & mask;
            _mask = mask;
        }

        public bool Contains(IPAddress address)
        {
            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return false;

            var bytes = address.GetAddressBytes();
            uint ip = (uint)bytes[0] << 24 | (uint)bytes[1] << 16
                    | (uint)bytes[2] << 8 | bytes[3];

            return (ip & _mask) == _network;
        }

        public static CidrRule[] Parse(string[] entries)
        {
            if (entries == null || entries.Length == 0)
                return null;

            var rules = new CidrRule[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                rules[i] = ParseOne(entries[i].Trim());
            }
            return rules;
        }

        static CidrRule ParseOne(string entry)
        {
            int slash = entry.IndexOf('/');
            if (slash < 0)
            {
                // Single IP — /32 mask
                uint ip = IPToUint(entry);
                return new CidrRule(ip, 0xFFFFFFFF);
            }

            uint network = IPToUint(entry.Substring(0, slash));
            int prefixLen = int.Parse(entry.Substring(slash + 1));
            if (prefixLen < 0 || prefixLen > 32)
                throw new ArgumentException("CIDR prefix must be 0-32: " + entry);

            uint mask = prefixLen == 0 ? 0 : 0xFFFFFFFF << (32 - prefixLen);
            return new CidrRule(network, mask);
        }

        static uint IPToUint(string ip)
        {
            var addr = IPAddress.Parse(ip);
            var bytes = addr.GetAddressBytes();
            return (uint)bytes[0] << 24 | (uint)bytes[1] << 16
                 | (uint)bytes[2] << 8 | bytes[3];
        }
    }
}
