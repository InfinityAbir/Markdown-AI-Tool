using Microsoft.AspNetCore.Http;

namespace DocToMarkdown.Services
{
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
    }
}