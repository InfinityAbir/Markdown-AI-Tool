using DocToMarkdown.Helpers;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace DocToMarkdown.Services
{
    public class ConversionService : IConversionService
    {
        private readonly string _uploadPath;
        private readonly GroqService _groq;
        private readonly ILogger<ConversionService> _logger;

        public ConversionService(GroqService groq, ILogger<ConversionService> logger)
        {
            _groq = groq;
            _logger = logger;
            _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

            if (!Directory.Exists(_uploadPath))
                Directory.CreateDirectory(_uploadPath);
        }

        public async Task<ConvertResult> ConvertToMarkdownAsync(IFormFile file, bool enableAICompression)
        {
            var uniqueName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var inputPath = Path.Combine(_uploadPath, uniqueName);
            string? outputPath = null;

            try
            {
                using (var stream = new FileStream(inputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var ext = Path.GetExtension(file.FileName).ToLower();

                if (ext == ".pdf")
                    return await ConvertPdfAsync(inputPath, enableAICompression);

                if (ext == ".docx" || ext == ".xlsx")
                {
                    outputPath = Path.ChangeExtension(inputPath, ".md");
                    return await ConvertWithPandocAsync(inputPath, outputPath, enableAICompression);
                }

                throw new Exception("Unsupported file type");
            }
            finally
            {
                // Uploaded documents are user data — never keep them around
                // longer than it takes to process this one request.
                FileHelper.SafeDelete(inputPath);
                if (outputPath != null)
                    FileHelper.SafeDelete(outputPath);
            }
        }

        // ================= PDF =================

        private async Task<ConvertResult> ConvertPdfAsync(string inputPath, bool enableAICompression)
        {
            int totalPages = await GetTotalPages(inputPath);
            int batchSize = 10;

            var finalBuilder = new StringBuilder();
            int originalTokens = 0;
            int aiAttempted = 0;
            int aiSucceeded = 0;

            for (int i = 1; i <= totalPages; i += batchSize)
            {
                int end = Math.Min(i + batchSize - 1, totalPages);

                string extractedText = await ExtractPages(inputPath, i, end);

                originalTokens += CountTokens(extractedText);

                string cleaned = CleanText(extractedText);

                if (enableAICompression)
                {
                    aiAttempted++;
                    var (compressed, succeeded) = await TryGroqCompress(cleaned);
                    cleaned = compressed;
                    if (succeeded) aiSucceeded++;
                }

                finalBuilder.AppendLine(cleaned);
                finalBuilder.AppendLine();
            }

            string finalContent = finalBuilder.ToString().Trim();
            int cleanedTokens = CountTokens(finalContent);
            var chunks = ChunkText(finalContent, 300);

            return new ConvertResult
            {
                Content = finalContent,
                Chunks = chunks,
                OriginalTokens = originalTokens,
                CleanedTokens = cleanedTokens,
                TotalPages = totalPages,
                ProcessedBatches = (int)Math.Ceiling((double)totalPages / batchSize),
                AiRequested = enableAICompression,
                AiAttemptedBatches = aiAttempted,
                AiSucceededBatches = aiSucceeded
            };
        }

        private async Task<int> GetTotalPages(string filePath)
        {
            var (exitCode, output, error) = await ProcessHelper.RunProcess(
                "pdfinfo", $"\"{filePath}\"", timeoutSeconds: 30);

            if (exitCode != 0)
                throw new Exception($"pdfinfo failed: {error}");

            var match = Regex.Match(output, @"Pages:\s+(\d+)");
            if (!match.Success)
                throw new Exception("Could not determine total pages.");

            return int.Parse(match.Groups[1].Value);
        }

        private async Task<string> ExtractPages(string filePath, int start, int end)
        {
            string tempFile = Path.Combine(_uploadPath, $"{Guid.NewGuid()}.txt");

            try
            {
                var (exitCode, _, error) = await ProcessHelper.RunProcess(
                    "pdftotext", $"-f {start} -l {end} \"{filePath}\" \"{tempFile}\"", timeoutSeconds: 60);

                if (exitCode != 0 || !File.Exists(tempFile))
                {
                    _logger.LogWarning("pdftotext failed for pages {Start}-{End}: {Error}", start, end, error);
                    return "";
                }

                return await File.ReadAllTextAsync(tempFile);
            }
            finally
            {
                FileHelper.SafeDelete(tempFile);
            }
        }

        // ================= DOCX / XLSX =================

        private async Task<ConvertResult> ConvertWithPandocAsync(string inputPath, string outputPath, bool enableAICompression)
        {
            var (exitCode, _, error) = await ProcessHelper.RunProcess(
                "pandoc", $"\"{inputPath}\" -o \"{outputPath}\"", timeoutSeconds: 60);

            if (exitCode != 0)
                throw new Exception($"Pandoc error: {error}");

            var content = await File.ReadAllTextAsync(outputPath);

            int originalTokens = CountTokens(content);

            content = CleanText(content);

            int aiAttempted = 0;
            int aiSucceeded = 0;

            if (enableAICompression)
            {
                aiAttempted++;
                var (compressed, succeeded) = await TryGroqCompress(content);
                content = compressed;
                if (succeeded) aiSucceeded++;
            }

            int cleanedTokens = CountTokens(content);
            var chunks = ChunkText(content, 300);

            return new ConvertResult
            {
                Content = content,
                Chunks = chunks,
                OriginalTokens = originalTokens,
                CleanedTokens = cleanedTokens,
                TotalPages = 1,
                ProcessedBatches = 1,
                AiRequested = enableAICompression,
                AiAttemptedBatches = aiAttempted,
                AiSucceededBatches = aiSucceeded
            };
        }

        // ================= AI (GROQ) WITH HONEST FALLBACK =================

        private async Task<(string content, bool succeeded)> TryGroqCompress(string cleaned)
        {
            try
            {
                var result = await _groq.CompressAsync(cleaned);
                return (result, true);
            }
            catch (Exception ex)
            {
                // Never silently pretend AI ran. Fall back to the plain
                // regex cleanup and let the caller report AiFullyApplied=false.
                _logger.LogWarning(ex, "Groq compression unavailable, using fallback cleaning");
                return (FallbackClean(cleaned), false);
            }
        }

        // ================= CLEANING =================

        private string CleanText(string content)
        {
            content = content.Replace("\r\n", "\n");

            content = Regex.Replace(content, @"[ \t]+", " ");
            content = Regex.Replace(content, @"\n{3,}", "\n\n");

            content = Regex.Replace(content, @"\bPage\s+\d+\b", "", RegexOptions.IgnoreCase);

            content = Regex.Replace(content, @"(?i)references[\s\S]*$", "");
            content = Regex.Replace(content, @"\[\d+\]", "");
            content = Regex.Replace(content, @"\(\d{4}\)", "");
            content = Regex.Replace(content, @"(?i)(figure|table)\s*\d+[:.\-].*", "");

            content = string.Join("\n",
                content.Split('\n')
                       .Where(line => line.Trim().Length > 3));

            return content.Trim();
        }

        // Basic, honest fallback used only when Groq is unavailable/rate-limited.
        // Not marketed as AI — just filler-word stripping and exact-duplicate removal.
        private string FallbackClean(string content)
        {
            content = Regex.Replace(content,
                @"\b(very|really|basically|actually|in order to|it is important to note that)\b",
                "",
                RegexOptions.IgnoreCase);

            var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);

            var unique = sentences
                .Select(s => s.Trim())
                .Distinct();

            return string.Join(". ", unique);
        }

        // ================= COMMON =================

        // Approximate token count (~4 chars/token), the same rule of thumb
        // OpenAI's own docs use. A word-count split (the old approach) is
        // consistently wrong for tokenizer-based billing; this is closer
        // without pulling in a full BPE tokenizer dependency.
        private int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        private List<string> ChunkText(string text, int size)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<string>();

            for (int i = 0; i < words.Length; i += size)
            {
                chunks.Add(string.Join(" ", words.Skip(i).Take(size)));
            }

            return chunks;
        }

        public async Task<ConvertResult> ConvertLargePdfAsync(IFormFile file)
        {
            return await ConvertToMarkdownAsync(file, true);
        }

        public Task<ConvertResult> ConvertToMarkdownAsync(IFormFile file)
        {
            throw new NotImplementedException();
        }
    }
}
