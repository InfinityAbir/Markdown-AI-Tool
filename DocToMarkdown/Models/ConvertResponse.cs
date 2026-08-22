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

        // Honest AI status — did Groq actually clean this document, or did
        // it fall back to basic regex cleaning (rate limit, outage, etc.)?
        public bool AiRequested { get; set; }
        public bool AiFullyApplied { get; set; }
        public int AiAttemptedBatches { get; set; }
        public int AiSucceededBatches { get; set; }

        // Text came from OCR (no text layer in the source), not native
        // extraction — quality depends on scan clarity, worth flagging.
        public bool OcrUsed { get; set; }
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