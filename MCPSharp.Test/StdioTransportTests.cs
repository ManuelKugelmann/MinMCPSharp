using System.Collections.Concurrent;
using System.Text;
using MCPSharp;
using MCPSharp.Example;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPSharp.Test
{
    /// <summary>
    /// A stream backed by a BlockingCollection that blocks on Read until data is available,
    /// unlike MemoryStream which returns 0 (EOF) immediately.
    /// </summary>
    internal sealed class BlockingStream : Stream
    {
        private readonly BlockingCollection<byte[]> _chunks = new();
        private byte[] _current = Array.Empty<byte>();
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_offset >= _current.Length)
            {
                try { _current = _chunks.Take(); }
                catch (InvalidOperationException) { return 0; } // completed
                _offset = 0;
            }

            int toCopy = Math.Min(count, _current.Length - _offset);
            Buffer.BlockCopy(_current, _offset, buffer, offset, toCopy);
            _offset += toCopy;
            return toCopy;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var copy = new byte[count];
            Buffer.BlockCopy(buffer, offset, copy, 0, count);
            _chunks.Add(copy);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public void Complete() => _chunks.CompleteAdding();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _chunks.Dispose();
            base.Dispose(disposing);
        }
    }

    [TestClass]
    public sealed class StdioTransportTests
    {
        private static McpServer _server = null!;
        private static BlockingStream _toServer = null!;
        private static BlockingStream _fromServer = null!;
        private static StreamReader _reader = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _toServer = new BlockingStream();
            _fromServer = new BlockingStream();

            _server = new McpServer("StdioTestServer", "1.0.0");
            _server.Register<MCPDev>();

            var transport = new StdioTransport(_toServer, _fromServer);
            _server.Start(transport, "stdio", 0);

            // Give server time to start read loop
            Thread.Sleep(200);

            _reader = new StreamReader(_fromServer, Encoding.UTF8);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _toServer?.Complete();
            _server?.Dispose();
        }

        private static async Task<JObject> SendAndReceiveAsync(string method, object? parms = null)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = method
            };
            if (parms != null)
                request["params"] = JToken.FromObject(parms);

            var json = request.ToString(Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");

            _toServer.Write(bytes, 0, bytes.Length);
            _toServer.Flush();

            // Read the response line (blocks until data arrives)
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var responseLine = await Task.Run(() => _reader.ReadLine(), cts.Token);

            Assert.IsNotNull(responseLine, "No response received from stdio transport");
            return JObject.Parse(responseLine);
        }

        [TestMethod("Stdio - Initialize")]
        public async Task Test_StdioInitialize()
        {
            var result = await SendAndReceiveAsync("initialize", new { protocolVersion = "2025-03-26" });
            Assert.IsNotNull(result["result"]);
            Assert.AreEqual("StdioTestServer", result["result"]!["serverInfo"]!["name"]!.ToString());
        }

        [TestMethod("Stdio - Tools/List")]
        public async Task Test_StdioToolsList()
        {
            var result = await SendAndReceiveAsync("tools/list");
            var tools = result["result"]!["tools"] as JArray;
            Assert.IsNotNull(tools);
            Assert.IsTrue(tools!.Count > 0);
        }

        [TestMethod("Stdio - Ping")]
        public async Task Test_StdioPing()
        {
            var result = await SendAndReceiveAsync("ping");
            Assert.IsNotNull(result["result"]);
        }
    }
}
