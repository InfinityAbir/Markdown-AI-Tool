using System.Linq;
using DocToMarkdown.Models;
using DocToMarkdown.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocToMarkdown.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConvertController : ControllerBase
    {
        private readonly IConversionService _service;

        public ConvertController(IConversionService service)
        {
            _service = service;
        }

        /// <summary>
        /// Upload a file (PDF, DOCX, XLSX) and convert to Markdown with token optimization
        /// </summary>
        [HttpPost("convert")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ConvertFile([FromForm] ConvertRequest request)
        {
            var file = request.File;

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx" };
            var ext = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(ext))
                return BadRequest("Unsupported file type");

            try
            {
                // 🔥 NEW: get AI mode from request (default = true)
                bool enableAICompression = request.EnableAICompression;

                // 🔥 pass to service
                var result = await _service.ConvertToMarkdownAsync(file, enableAICompression);

                return Ok(new ConvertResponse
                {
                    FileName = file.FileName,

                    // 📄 Full markdown
                    MarkdownContent = result.Content,

                    // 🧩 Chunks for AI
                    Chunks = result.Chunks,

                    // 📊 Token analytics
                    TokenReport = new TokenReport
                    {
                        OriginalTokens = result.OriginalTokens,
                        CleanedTokens = result.CleanedTokens,
                        ReductionPercent = Math.Round(result.ReductionPercentage, 2)
                    },

                    // 🔥 Large PDF insights
                    TotalPages = result.TotalPages,
                    ProcessedBatches = result.ProcessedBatches
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");

                return StatusCode(500, new
                {
                    message = "Conversion failed",
                    error = ex.Message
                });
            }
        }
    }
}