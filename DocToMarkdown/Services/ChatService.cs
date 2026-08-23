using System.Text.RegularExpressions;

namespace DocToMarkdown.Services
{
    public record ChatAnswer(string Answer, bool ContextTruncated);

    public class ChatService
    {
        private readonly GroqService _groq;

        // Comfortably within the model's context window while keeping
        // latency/cost reasonable. Most uploads (1-2 page docs) fit inside
        // this whole, no chunk selection needed — the common case gets the
        // full document, not a lossy approximation of it.
        private const int MaxContextChars = 24000;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "is", "are", "was", "were", "of", "in", "on", "at", "to", "for",
            "and", "or", "but", "this", "that", "these", "those", "with", "from", "by", "as",
            "it", "its", "be", "been", "has", "have", "had", "what", "when", "where", "how",
            "why", "who", "does", "do", "did", "can", "could", "would", "should", "will",
        };

        public ChatService(GroqService groq)
        {
            _groq = groq;
        }

        public async Task<ChatAnswer> AskAsync(List<string> chunks, string question, IEnumerable<ChatTurn> history, CancellationToken ct = default)
        {
            var (context, truncated) = BuildContext(chunks, question);
            var answer = await _groq.AskAsync(context, question, history, ct);
            return new ChatAnswer(answer, truncated);
        }

        private static (string context, bool truncated) BuildContext(List<string> chunks, string question)
        {
            string full = string.Join("\n\n", chunks);
            if (full.Length <= MaxContextChars)
                return (full, false);

            // Long document: fall back to the chunks most relevant to the
            // question (keyword overlap, no embeddings needed) instead of a
            // blind truncation that might cut off the relevant part.
            var questionWords = Tokenize(question);

            var ranked = chunks
                .Select(c => (chunk: c, score: Tokenize(c).Count(questionWords.Contains)))
                .OrderByDescending(x => x.score)
                .ToList();

            var selected = new List<string>();
            int used = 0;
            foreach (var (chunk, _) in ranked)
            {
                if (used + chunk.Length > MaxContextChars && selected.Count > 0)
                    break;
                selected.Add(chunk);
                used += chunk.Length;
            }

            return (string.Join("\n\n", selected), true);
        }

        private static HashSet<string> Tokenize(string text)
        {
            return Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9]+")
                .Select(m => m.Value)
                .Where(w => w.Length > 2 && !StopWords.Contains(w))
                .ToHashSet();
        }
    }
}
