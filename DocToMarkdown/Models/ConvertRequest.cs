using Microsoft.AspNetCore.Http;

namespace DocToMarkdown.Models
{
    public class ConvertRequest
    {
        public IFormFile File { get; set; }

        public bool EnableAICompression { get; set; } = true;
    }
}