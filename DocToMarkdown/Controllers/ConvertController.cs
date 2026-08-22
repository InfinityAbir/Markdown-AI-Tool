using System.Linq;
using DocToMarkdown.Models;
using DocToMarkdown.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocToMarkdown.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConvertController : ControllerBase
    {
        private readonly IConversionService _service;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ConvertController> _logger;

        // Extension -> required magic bytes at the start of the file.
        // Prevents a renamed .exe/.html etc. from reaching pandoc/pdftotext.
        private static readonly Dictionary<string, byte[][]> MagicBytes = new()
        {
            [".pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }, // %PDF
            [".docx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } }, // PK.. (zip)
            [".xlsx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        };

        private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB — honest free-tier limit

        public ConvertController(IConversionService service, IWebHostEnvironment env, ILogger<ConvertController> logger)
        {
            _service = service;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Upload a file (PDF, DOCX, XLSX) and convert to Markdown with token optimization
        /// </summary>
        [HttpPost("convert")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        [EnableRateLimiting("convert")]
        public async Task<IActionResult> ConvertFile([FromForm] ConvertRequest request)
        {
            var file = request.File;

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            if (file.Length > MaxFileSizeBytes)
                return BadRequest(new { message = $"File exceeds the {MaxFileSizeBytes / 1024 / 1024}MB limit on this free instance" });

            var ext = Path.GetExtension(file.FileName).ToLower();

            if (!MagicBytes.ContainsKey(ext))
                return BadRequest(new { message = "Unsupported file type" });

            if (!await HasValidMagicBytes(file, ext))
                return BadRequest(new { message = "File content doesn't match its extension" });

            try
            {
                bool enableAICompression = request.EnableAICompression;

                var result = await _service.ConvertToMarkdownAsync(file, enableAICompression);

                return Ok(new ConvertResponse
                {
                    FileName = file.FileName,
                    MarkdownContent = result.Content,
                    Chunks = result.Chunks,
                    TokenReport = new TokenReport
                    {
                        OriginalTokens = result.OriginalTokens,
                        CleanedTokens = result.CleanedTokens,
                        ReductionPercent = Math.Round(result.ReductionPercentage, 2)
                    },
                    TotalPages = result.TotalPages,
                    ProcessedBatches = result.ProcessedBatches,
                    AiRequested = result.AiRequested,
                    AiFullyApplied = result.AiFullyApplied,
                    AiAttemptedBatches = result.AiAttemptedBatches,
                    AiSucceededBatches = result.AiSucceededBatches
                });
            }
            catch (ConversionException ex)
            {
                // Expected, user-actionable failure (e.g. scanned PDF) —
                // safe to show verbatim in any environment.
                return UnprocessableEntity(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Conversion failed for {FileName}", file.FileName);

                return StatusCode(500, new
                {
                    message = "Conversion failed",
                    // Don't leak internals (paths, tool output) outside Development
                    error = _env.IsDevelopment() ? ex.Message : "Internal error"
                });
            }
        }

        private static async Task<bool> HasValidMagicBytes(IFormFile file, string ext)
        {
            var signatures = MagicBytes[ext];
            var maxLen = signatures.Max(s => s.Length);
            var buffer = new byte[maxLen];

            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(buffer.AsMemory(0, maxLen));
            stream.Position = 0;

            if (read < maxLen) return false;

            return signatures.Any(sig => buffer.Take(sig.Length).SequenceEqual(sig));
        }
    }
}
