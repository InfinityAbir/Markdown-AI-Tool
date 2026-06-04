namespace DocToMarkdown.Models
{
    public class ConvertResponse
    {
        public string FileName { get; set; } = string.Empty;

        // 📄 Full Markdown Output
        public string MarkdownContent { get; set; } = string.Empty;

        // 🧩 AI-ready chunks
        public List<string> Chunks { get; set; } = new();

        // 📊 Token analytics
        public TokenReport TokenReport { get; set; } = new();

        // 🔥 NEW: Large PDF Support Info
        public int TotalPages { get; set; }

        public int ProcessedBatches { get; set; }

        // ⚡ Optional but useful
        public int ChunkCount => Chunks?.Count ?? 0;
    }

    public class TokenReport
    {
        public int OriginalTokens { get; set; }

        public int CleanedTokens { get; set; }

        public double ReductionPercent { get; set; }

        // 🔥 Extra insight (optional but powerful)
        public int TokensSaved => OriginalTokens - CleanedTokens;
    }
}