namespace DocToMarkdown.Models
{
    public class ChatRequest
    {
        public List<string> Chunks { get; set; } = new();
        public string Question { get; set; } = string.Empty;
        public List<ChatHistoryItem> History { get; set; } = new();
    }

    public class ChatHistoryItem
    {
        public string Role { get; set; } = string.Empty; // "user" or "assistant"
        public string Content { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public string Answer { get; set; } = string.Empty;

        // True if the document was too long to fit whole and only the
        // most relevant chunks (keyword-matched) were used — the answer
        // may miss context that lives in an unselected part of the doc.
        public bool ContextTruncated { get; set; }
    }
}
