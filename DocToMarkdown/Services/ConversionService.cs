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
            var inputPath = await SaveUploadedFileAsync(file);
            return await ConvertFromSavedFileAsync(inputPath, file.FileName, enableAICompression);
        }

        // Saves the upload to disk while the request's IFormFile stream is
        // still valid, so the caller can hand the returned path off to a
        // background job that outlives this HTTP request.
        public async Task<string> SaveUploadedFileAsync(IFormFile file)
        {
            var uniqueName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var inputPath = Path.Combine(_uploadPath, uniqueName);

            using (var stream = new FileStream(inputPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return inputPath;
        }

        public async Task<ConvertResult> ConvertFromSavedFileAsync(string inputPath, string originalFileName, bool enableAICompression)
        {
            string? outputPath = null;

            try
            {
                var ext = Path.GetExtension(originalFileName).ToLower();

                if (ext == ".pdf")
                    return await ConvertPdfAsync(inputPath, enableAICompression);

                if (ext == ".docx" || ext == ".xlsx")
                {
                    outputPath = Path.ChangeExtension(inputPath, ".md");
                    return await ConvertWithPandocAsync(inputPath, outputPath, enableAICompression);
                }

                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                    return await ConvertImageAsync(inputPath, enableAICompression);

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

        // OCR is CPU-heavy and Render's free tier has very limited/shared
        // CPU. Cap scanned-PDF OCR to a page count that won't just hang or
        // blow the per-operation timeout.
        private const int OcrMaxPages = 15;

        private async Task<ConvertResult> ConvertPdfAsync(string inputPath, bool enableAICompression)
        {
            int totalPages = await GetTotalPages(inputPath);
            int batchSize = 10;

            var finalBuilder = new StringBuilder();
            int originalTokens = 0;
            int aiAttempted = 0;
            int aiSucceeded = 0;
            bool ocrUsed = false;

            for (int i = 1; i <= totalPages; i += batchSize)
            {
                int end = Math.Min(i + batchSize - 1, totalPages);

                string extractedText = await ExtractPages(inputPath, i, end);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    // No text layer for this range — likely a scanned/image
                    // page. Fall back to OCR rather than silently skipping it.
                    if (totalPages > OcrMaxPages)
                        throw new ConversionException(
                            $"This looks like a scanned PDF with no text layer. OCR on this free instance is limited to {OcrMaxPages} pages (CPU limits) — this file has {totalPages}.");

                    extractedText = await OcrExtractPages(inputPath, i, end);
                    ocrUsed = true;
                }

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

            // Should only trip if OCR itself found nothing (blank pages,
            // extremely low-quality scan) — the no-text-layer case above is
            // already handled by falling back to OCR.
            if (originalTokens == 0)
                throw new ConversionException(
                    "No extractable text found, even after OCR. This file may be blank or too low-quality to read.");

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
                AiSucceededBatches = aiSucceeded,
                OcrUsed = ocrUsed
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

        // ================= OCR (scanned PDFs) =================

        private async Task<string> OcrExtractPages(string filePath, int start, int end)
        {
            string prefix = Path.Combine(_uploadPath, Guid.NewGuid().ToString());
            var imageFiles = new List<string>();

            try
            {
                // Rasterize the page range to PNGs (pdftoppm numbers output
                // files by page, e.g. prefix-1.png, prefix-2.png, ...).
                var (rasterExit, _, rasterError) = await ProcessHelper.RunProcess(
                    "pdftoppm",
                    $"-png -f {start} -l {end} -r 200 \"{filePath}\" \"{prefix}\"",
                    timeoutSeconds: 90);

                if (rasterExit != 0)
                {
                    _logger.LogWarning("pdftoppm failed for pages {Start}-{End}: {Error}", start, end, rasterError);
                    return "";
                }

                imageFiles = Directory.GetFiles(_uploadPath, $"{Path.GetFileName(prefix)}-*.png")
                    .OrderBy(f => f)
                    .ToList();

                var ocrBuilder = new StringBuilder();
                foreach (var imagePath in imageFiles)
                {
                    // "stdout" tells tesseract to print the result instead of
                    // writing a .txt file.
                    var (ocrExit, ocrOutput, ocrError) = await ProcessHelper.RunProcess(
                        "tesseract", $"\"{imagePath}\" stdout --oem 1", timeoutSeconds: 120);

                    if (ocrExit != 0)
                    {
                        _logger.LogWarning("tesseract failed for {Image}: {Error}", imagePath, ocrError);
                        continue;
                    }

                    ocrBuilder.AppendLine(ocrOutput);
                }

                return ocrBuilder.ToString();
            }
            finally
            {
                foreach (var img in imageFiles)
                    FileHelper.SafeDelete(img);
            }
        }

        // ================= CAMERA / IMAGE SCAN =================

        private async Task<ConvertResult> ConvertImageAsync(string inputPath, bool enableAICompression)
        {
            var (exitCode, ocrOutput, error) = await ProcessHelper.RunProcess(
                "tesseract", $"\"{inputPath}\" stdout --oem 1", timeoutSeconds: 120);

            if (exitCode != 0)
                throw new Exception($"OCR failed: {error}");

            if (string.IsNullOrWhiteSpace(ocrOutput))
                throw new ConversionException(
                    "No readable text found in this photo. Try a clearer, well-lit, straight-on shot.");

            int originalTokens = CountTokens(ocrOutput);
            string content = CleanText(ocrOutput);

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
                AiSucceededBatches = aiSucceeded,
                OcrUsed = true
            };
        }

        // ================= DOCX / XLSX =================

        private async Task<ConvertResult> ConvertWithPandocAsync(string inputPath, string outputPath, bool enableAICompression)
        {
            var (exitCode, _, error) = await ProcessHelper.RunProcess(
                "pandoc", $"\"{inputPath}\" -o \"{outputPath}\"", timeoutSeconds: 60);

            if (exitCode != 0)
                throw new Exception($"Pandoc error: {error}");

            var content = await File.ReadAllTextAsync(outputPath);

            if (string.IsNullOrWhiteSpace(content))
                throw new ConversionException("No extractable text found in this file.");

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
