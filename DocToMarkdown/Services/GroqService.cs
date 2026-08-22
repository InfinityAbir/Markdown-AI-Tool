using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocToMarkdown.Services
{
    public class GroqService
    {
        private readonly HttpClient _http;
        private readonly ILogger<GroqService> _logger;
        private readonly string? _apiKey;

        // Free tier has strict RPM limits; cap concurrent outbound calls
        // across the whole app so simultaneous uploads don't blow through it.
        private static readonly SemaphoreSlim Throttle = new(3);

        private const string Model = "openai/gpt-oss-20b";

        public GroqService(HttpClient http, IConfiguration config, ILogger<GroqService> logger)
        {
            _http = http;
            _logger = logger;
            _apiKey = config["Groq:ApiKey"];
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        /// <summary>
        /// Compresses text via Groq. Throws on failure/timeout/rate-limit —
        /// caller is expected to fall back to the regex cleaner, never to
        /// silently claim AI ran when it didn't.
        /// </summary>
        public async Task<string> CompressAsync(string text, CancellationToken ct = default)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Groq API key not configured");

            var payload = new
            {
                model = Model,
                temperature = 0.2,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You compress text for LLM context windows. Remove filler words, " +
                                  "merge redundant sentences, cut repetition. Preserve all facts, numbers, " +
                                  "and meaning exactly. Output only the compressed text, no commentary."
                    },
                    new { role = "user", content = text }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            await Throttle.WaitAsync(timeoutCts.Token);
            try
            {
                var response = await _http.SendAsync(request, timeoutCts.Token);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: timeoutCts.Token);
                var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                if (string.IsNullOrWhiteSpace(content))
                    throw new InvalidOperationException("Groq returned empty content");

                return content;
            }
            finally
            {
                Throttle.Release();
            }
        }
    }
}
