namespace MCPSharp.Test
{
    /// <summary>
    /// Thread-safe port allocator for tests. Each call returns a unique port,
    /// avoiding collisions when tests run in parallel.
    /// </summary>
    internal static class TestPortAllocator
    {
        private static int _nextPort = 18000;

        public static int GetPort() => Interlocked.Increment(ref _nextPort);
    }
}
