using DocToMarkdown.Models;
using DocToMarkdown.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocToMarkdown.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly GroqService _groq;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ChatController> _logger;

        private const int MaxQuestionLength = 2000;
        private const int MaxHistoryTurns = 12;

        public ChatController(ChatService chatService, GroqService groq, IWebHostEnvironment env, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _groq = groq;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Ask a question about a previously converted document. Stateless —
        /// the client resends the chunks it already has from /convert, so
        /// nothing about the document is stored server-side.
        /// </summary>
        [HttpPost("ask")]
        [RequestSizeLimit(2 * 1024 * 1024)]
        [EnableRateLimiting("chat")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (!_groq.IsConfigured)
                return StatusCode(503, new { message = "Chat isn't available right now — AI service isn't configured." });

            if (request.Chunks == null || request.Chunks.Count == 0)
                return BadRequest(new { message = "No document content provided" });

            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { message = "Question is empty" });

            if (request.Question.Length > MaxQuestionLength)
                return BadRequest(new { message = $"Question is too long (max {MaxQuestionLength} characters)" });

            var history = (request.History ?? new())
                .TakeLast(MaxHistoryTurns)
                .Select(h => new ChatTurn(h.Role == "assistant" ? "assistant" : "user", h.Content));

            try
            {
                var result = await _chatService.AskAsync(request.Chunks, request.Question, history);

                return Ok(new ChatResponse
                {
                    Answer = result.Answer,
                    ContextTruncated = result.ContextTruncated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat request failed");

                return StatusCode(502, new
                {
                    message = "AI is unavailable right now (rate limit or outage) — try again shortly.",
                    error = _env.IsDevelopment() ? ex.Message : null
                });
            }
        }
    }
}
