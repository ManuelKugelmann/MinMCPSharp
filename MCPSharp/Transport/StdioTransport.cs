using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MCPSharp
{
    /// <summary>
    /// MCP stdio transport. Reads newline-delimited JSON-RPC from stdin,
    /// writes responses to stdout. For editor integrations and unit testing.
    /// </summary>
    public class StdioTransport : IMcpTransport
    {
        private Thread _thread;
        private CancellationTokenSource _cts;
        private Stream _input;
        private Stream _output;

        public Func<string, Task<string>> RequestHandler { get; set; }
        public bool IsRunning { get; private set; }

        public StdioTransport() : this(null, null) { }

        public StdioTransport(Stream input, Stream output)
        {
            _input = input;
            _output = output;
        }

        public void Start(string endpoint, int port)
        {
            if (IsRunning) return;

            _input = _input ?? Console.OpenStandardInput();
            _output = _output ?? Console.OpenStandardOutput();
            _cts = new CancellationTokenSource();
            IsRunning = true;

            _thread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "MCPSharp-Stdio"
            };
            _thread.Start();
        }

        public void Stop()
        {
            IsRunning = false;
            _cts?.Cancel();
        }

        public void Dispose()
        {
            Stop();
        }

        private async void ReadLoop()
        {
            var reader = new StreamReader(_input, Encoding.UTF8);

            while (IsRunning && !_cts.IsCancellationRequested)
            {
                string line;
                try
                {
                    line = reader.ReadLine();
                }
                catch (Exception)
                {
                    break;
                }

                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (RequestHandler == null) continue;

                try
                {
                    var response = await RequestHandler(line);
                    if (response != null)
                        WriteLine(response);
                }
                catch (Exception ex)
                {
                    var err = "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32603,\"message\":\""
                              + ex.Message.Replace("\"", "\\\"") + "\"}}";
                    WriteLine(err);
                }
            }
        }

        private void WriteLine(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text + "\n");
            lock (_output)
            {
                _output.Write(bytes, 0, bytes.Length);
                _output.Flush();
            }
        }
    }
}
