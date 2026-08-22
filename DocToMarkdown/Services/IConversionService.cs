using Microsoft.AspNetCore.Http;

namespace DocToMarkdown.Services
{
    /// <summary>
    /// Expected, user-actionable conversion failures (e.g. a scanned PDF
    /// with no extractable text). Safe to show verbatim to the client in
    /// any environment — unlike a generic Exception, which may carry
    /// internal tool output and is hidden outside Development.
    /// </summary>
    public class ConversionException : Exception
    {
        public ConversionException(string message) : base(message) { }
    }

    public interface IConversionService
    {
        // 📄 Main method (supports all file types)
        Task<ConvertResult> ConvertToMarkdownAsync(IFormFile file, bool enableAICompression);

        // 📚 Optional: specifically for large PDFs (more control later)
        Task<ConvertResult> ConvertLargePdfAsync(IFormFile file);
    }

    // 📦 Clean structured response (better than tuple)
    public class ConvertResult
    {
        public string Content { get; set; } = string.Empty;

        public List<string> Chunks { get; set; } = new();

        public int OriginalTokens { get; set; }

        public int CleanedTokens { get; set; }

        public double ReductionPercentage =>
            OriginalTokens == 0 ? 0 :
            (1 - (double)CleanedTokens / OriginalTokens) * 100;

        public int TotalPages { get; set; }

        public int ProcessedBatches { get; set; }

        // Honesty fields: report what actually happened, not what was requested.
        public bool AiRequested { get; set; }
        public int AiAttemptedBatches { get; set; }
        public int AiSucceededBatches { get; set; }
        public bool AiFullyApplied => AiRequested && AiAttemptedBatches > 0 && AiAttemptedBatches == AiSucceededBatches;
    }
}