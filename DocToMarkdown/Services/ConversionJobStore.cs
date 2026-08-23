using System.Collections.Concurrent;
using DocToMarkdown.Models;

namespace DocToMarkdown.Services
{
    public enum JobState { Pending, Done, Failed }

    public class ConversionJob
    {
        public JobState State { get; set; } = JobState.Pending;
        public ConvertResponse? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public int ErrorStatusCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // In-memory job tracker for the async convert flow — a long OCR/Groq
    // run must not sit inside a single HTTP request, since Render's proxy
    // kills any connection held open too long regardless of our own
    // timeouts. The client polls status instead. Single free instance,
    // so in-memory is enough; a redeploy mid-job just loses that job.
    public class ConversionJobStore
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
        private readonly ConcurrentDictionary<string, ConversionJob> _jobs = new();

        public string Create()
        {
            SweepExpired();
            var id = Guid.NewGuid().ToString("N");
            _jobs[id] = new ConversionJob { CreatedAt = DateTime.UtcNow };
            return id;
        }

        public ConversionJob? Get(string id) =>
            _jobs.TryGetValue(id, out var job) ? job : null;

        public void Complete(string id, ConvertResponse result)
        {
            if (_jobs.TryGetValue(id, out var job))
            {
                job.Result = result;
                job.State = JobState.Done;
            }
        }

        public void Fail(string id, string message, int statusCode)
        {
            if (_jobs.TryGetValue(id, out var job))
            {
                job.ErrorMessage = message;
                job.ErrorStatusCode = statusCode;
                job.State = JobState.Failed;
            }
        }

        private void SweepExpired()
        {
            var cutoff = DateTime.UtcNow - Ttl;
            foreach (var kvp in _jobs)
            {
                if (kvp.Value.CreatedAt < cutoff)
                    _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }
}
