namespace DocQA.Models
{
    public class Options
    {
        public sealed class OllamaOptions
        {
            public string Endpoint { get; set; } = "http://localhost:11434";
            public string ChatModel { get; set; } = "llama3.2:3b";
            public string EmbeddingModel { get; set; } = "nomic-embed-text";
            public int TimeoutSeconds { get; set; } = 300;
        }

        public sealed class RagOptions
        {
            public int ChunkSize { get; set; } = 1800;
            public int ChunkOverlap { get; set; } = 200;
            public string DocumentPrefix { get; set; } = "search_document: ";
            public string QueryPrefix { get; set; } = "search_query: ";
            public int TopK { get; set; } = 5;

            /// <summary>Chunks farther than this are treated as irrelevant. Used from step 9.</summary>
            public double MaxDistance { get; set; } = 0.7;
        }
        public sealed class JwtOptions
        {
            public string Issuer { get; set; } = "DocQA";
            public string Audience { get; set; } = "DocQA.Client";
            public string Key { get; set; } = string.Empty;
            public int ExpiryMinutes { get; set; } = 60;
        }
    }
}
