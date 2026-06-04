using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DocToMarkdown.Services
{
    public class ConversionService : IConversionService
    {
        private readonly string _uploadPath;

        public ConversionService()
        {
            _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

            if (!Directory.Exists(_uploadPath))
                Directory.CreateDirectory(_uploadPath);
        }

        // 🔥 NOW RECEIVES FLAG FROM UI
        public async Task<ConvertResult> ConvertToMarkdownAsync(IFormFile file, bool enableAICompression)
        {
            var uniqueName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var inputPath = Path.Combine(_uploadPath, uniqueName);

            using (var stream = new FileStream(inputPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var ext = Path.GetExtension(file.FileName).ToLower();

            if (ext == ".pdf")
                return await ConvertPdfAsync(inputPath, enableAICompression);

            if (ext == ".docx" || ext == ".xlsx")
                return await ConvertWithPandocAsync(inputPath, enableAICompression);

            throw new Exception("Unsupported file type");
        }

        // ================= PDF =================

        private async Task<ConvertResult> ConvertPdfAsync(string inputPath, bool enableAICompression)
        {
            int totalPages = await GetTotalPages(inputPath);
            int batchSize = 10;

            var finalBuilder = new StringBuilder();
            int originalTokens = 0;

            for (int i = 1; i <= totalPages; i += batchSize)
            {
                int end = Math.Min(i + batchSize - 1, totalPages);

                string extractedText = await ExtractPages(inputPath, i, end);

                originalTokens += CountTokens(extractedText);

                string cleaned = CleanText(extractedText);

                // 🔥 APPLY BASED ON UI
                if (enableAICompression)
                    cleaned = AICompress(cleaned);

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
                ProcessedBatches = (int)Math.Ceiling((double)totalPages / batchSize)
            };
        }

        private async Task<int> GetTotalPages(string filePath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pdfinfo",
                    Arguments = $"\"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var match = Regex.Match(output, @"Pages:\s+(\d+)");
            if (!match.Success)
                throw new Exception("Could not determine total pages.");

            return int.Parse(match.Groups[1].Value);
        }

        private async Task<string> ExtractPages(string filePath, int start, int end)
        {
            string tempFile = Path.Combine(_uploadPath, $"{Guid.NewGuid()}.txt");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pdftotext",
                    Arguments = $"-f {start} -l {end} \"{filePath}\" \"{tempFile}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (!File.Exists(tempFile))
                return "";

            string text = await File.ReadAllTextAsync(tempFile);
            File.Delete(tempFile);

            return text;
        }

        // ================= DOCX / XLSX =================

        private async Task<ConvertResult> ConvertWithPandocAsync(string inputPath, bool enableAICompression)
        {
            string outputPath = Path.ChangeExtension(inputPath, ".md");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pandoc",
                    Arguments = $"\"{inputPath}\" -o \"{outputPath}\"",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"Pandoc error: {error}");

            var content = await File.ReadAllTextAsync(outputPath);

            int originalTokens = CountTokens(content);

            content = CleanText(content);

            if (enableAICompression)
                content = AICompress(content);

            int cleanedTokens = CountTokens(content);

            var chunks = ChunkText(content, 300);

            return new ConvertResult
            {
                Content = content,
                Chunks = chunks,
                OriginalTokens = originalTokens,
                CleanedTokens = cleanedTokens,
                TotalPages = 1,
                ProcessedBatches = 1
            };
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

        // ================= AI COMPRESSION =================

        private string AICompress(string content)
        {
            content = Regex.Replace(content,
                @"\b(very|really|basically|actually|in order to|it is important to note that)\b",
                "",
                RegexOptions.IgnoreCase);

            var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);

            var unique = sentences
                .Select(s => s.Trim())
                .Distinct();

            content = string.Join(". ", unique);

            content = Regex.Replace(content, @"\bwhich is\b", ":");
            content = Regex.Replace(content, @"\bthat is\b", ":");

            return content;
        }

        // ================= COMMON =================

        private int CountTokens(string text)
        {
            return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private List<string> ChunkText(string text, int size)
        {
            var words = text.Split(' ');
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