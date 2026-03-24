using Newtonsoft.Json;

namespace MCPSharp.Example
{
    /// <summary>
    /// A complicated object
    /// </summary>
    public class ComplicatedObject
    {
        /// <summary>The name of the object</summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>The age of the object</summary>
        [JsonProperty("age")]
        public int Age { get; set; } = 0;

        /// <summary>The hobbies of the object</summary>
        [JsonProperty("hobbies")]
        public string[] Hobbies { get; set; } = [];
    }
}
