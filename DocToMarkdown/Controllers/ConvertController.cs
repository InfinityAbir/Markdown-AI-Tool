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
        private readonly ConversionJobStore _jobStore;
        private readonly IServiceScopeFactory _scopeFactory;

        // Extension -> required magic bytes at the start of the file.
        // Prevents a renamed .exe/.html etc. from reaching pandoc/pdftotext.
        private static readonly Dictionary<string, byte[][]> MagicBytes = new()
        {
            [".pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }, // %PDF
            [".docx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } }, // PK.. (zip)
            [".xlsx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
            [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        };

        private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB — honest free-tier limit

        public ConvertController(
            IConversionService service,
            IWebHostEnvironment env,
            ILogger<ConvertController> logger,
            ConversionJobStore jobStore,
            IServiceScopeFactory scopeFactory)
        {
            _service = service;
            _env = env;
            _logger = logger;
            _jobStore = jobStore;
            _scopeFactory = scopeFactory;
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

            string savedPath;
            try
            {
                savedPath = await _service.SaveUploadedFileAsync(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save uploaded file {FileName}", file.FileName);
                return StatusCode(500, new { message = "Failed to save uploaded file" });
            }

            var originalFileName = file.FileName;
            bool enableAICompression = request.EnableAICompression;
            var jobId = _jobStore.Create();

            // Conversion (OCR/Groq) can run well past what any proxy holds a
            // connection open for — Render's included one killed a 90s
            // request outright with no response at all. Run it detached and
            // let the client poll GetStatus instead, so no single HTTP call
            // is ever held open longer than a status lookup.
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedService = scope.ServiceProvider.GetRequiredService<IConversionService>();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<ConvertController>>();

                try
                {
                    var result = await scopedService.ConvertFromSavedFileAsync(savedPath, originalFileName, enableAICompression);

                    _jobStore.Complete(jobId, new ConvertResponse
                    {
                        FileName = originalFileName,
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
                        AiSucceededBatches = result.AiSucceededBatches,
                        OcrUsed = result.OcrUsed
                    });
                }
                catch (ConversionException ex)
                {
                    // Expected, user-actionable failure (e.g. scanned PDF) —
                    // safe to show verbatim in any environment.
                    _jobStore.Fail(jobId, ex.Message, 422);
                }
                catch (Exception ex)
                {
                    scopedLogger.LogError(ex, "Conversion failed for {FileName}", originalFileName);
                    _jobStore.Fail(jobId, "Conversion failed", 500);
                }
            });

            return Accepted(new { jobId });
        }

        /// <summary>
        /// Poll the status of a conversion job started via POST /convert.
        /// </summary>
        [HttpGet("status/{jobId}")]
        public IActionResult GetStatus(string jobId)
        {
            var job = _jobStore.Get(jobId);

            if (job == null)
                return NotFound(new { message = "Job not found (it may have expired)" });

            return job.State switch
            {
                JobState.Pending => Ok(new { status = "pending" }),
                JobState.Done => Ok(new { status = "done", result = job.Result }),
                JobState.Failed => Ok(new { status = "failed", message = job.ErrorMessage, statusCode = job.ErrorStatusCode }),
                _ => Ok(new { status = "unknown" })
            };
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
