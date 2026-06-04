    using OpenAI.Embeddings;

    namespace DocToMarkdown.Services
    {
        public class EmbeddingService
        {
            private readonly EmbeddingClient _client;

            public EmbeddingService(string apiKey)
            {
                _client = new EmbeddingClient("text-embedding-3-small", apiKey);
            }

            public async Task<List<float[]>> GenerateEmbeddings(List<string> chunks)
            {
                var embeddings = new List<float[]>();

                foreach (var chunk in chunks)
                {
                    var result = await _client.GenerateEmbeddingAsync(chunk);
                    embeddings.Add(result.Value.ToFloats().ToArray());
                }

                return embeddings;
            }
        }
    }