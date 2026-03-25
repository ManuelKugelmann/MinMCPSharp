namespace MinMCPSharp.ExternalExample
{
    /// <summary>
    /// Demonstrates tools defined in a separate assembly.
    /// Register with: server.Register&lt;ExternalTool&gt;();
    /// </summary>
    public class ExternalTool
    {
        /// <summary>
        /// A tool loaded from an external assembly DLL. Should return 'success'.
        /// </summary>
        [McpTool("dll-tool", "attempts to use a tool loaded from an external assembly dll")]
        public static async Task<string> UseAsync()
        {
            return await Task.Run(() => "success");
        }
    }
}
