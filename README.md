# Doc → Markdown

Converts PDF, DOCX, and XLSX files into clean, chunked Markdown — built for feeding documents into LLM/RAG pipelines, not just as a generic file converter.
---
[![Live Demo](https://img.shields.io/badge/Live_Demo-Visit_Site-2ea44f?style=for-the-badge&logo=googlechrome&logoColor=white)](https://doc-to-markdown-ui.onrender.com)

## What it does

- **Convert** PDF (via Poppler), DOCX/XLSX (via Pandoc) into Markdown
- **Clean** the output: strip page numbers, references, repeated boilerplate
- **AI cleanup (optional)** — a real LLM call (Groq, free tier) removes filler and redundant sentences while preserving meaning. If Groq is rate-limited or unavailable, the app falls back to basic regex cleaning automatically — and says so in the response (`aiFullyApplied: false`), rather than silently claiming AI ran when it didn't
- **Chunk** the result into fixed-size pieces, ready for embeddings/RAG ingestion
- **Token analytics** — approximate before/after token counts and reduction %
- **Export** as Markdown or as RAG-ready JSON (chunks + metadata)
- Handles large PDFs via page-batch processing

## Honest limitations

This runs on free infrastructure (Groq free tier, free hosting tier), so:
- 25MB file size cap
- AI cleanup may fall back to basic cleaning under rate limits
- Token counts are an approximation (chars/4), not a real BPE tokenizer count

## Tech stack

- **Backend:** ASP.NET Core 9, Pandoc, Poppler, Groq (OpenAI-compatible API)
- **Frontend:** React 19, Axios, react-markdown

## Running locally

### Backend

Requires [Pandoc](https://pandoc.org/installing.html) and [Poppler](https://github.com/oschwartz10612/poppler-windows/releases) on PATH.

```bash
cd DocToMarkdown
dotnet user-secrets init
dotnet user-secrets set "Groq:ApiKey" "your-groq-key"
dotnet run
```

API runs at `http://localhost:5256` (Swagger UI at `/` in Development only).

Get a free Groq API key at [console.groq.com](https://console.groq.com). Without one, conversions still work — they just always use the basic fallback cleaner instead of real AI cleanup.

### Frontend

```bash
cd doc-ui
npm install
cp .env.example .env   # set REACT_APP_API_URL if backend isn't on :5256
npm start
```

Runs at `http://localhost:3000`.

## Running with Docker

```bash
cd DocToMarkdown
docker build -t doc-to-markdown .
docker run -p 8080:8080 -e Groq__ApiKey=your-groq-key -e PORT=8080 doc-to-markdown
```

## Deploying (Render, free tier)

1. **Backend** — new Render "Web Service" from this repo, root directory `DocToMarkdown`, Docker runtime (uses the included `Dockerfile`). Set env vars:
   - `Groq__ApiKey` — your Groq key
   - `AllowedOrigins__0` — your deployed frontend URL
2. **Frontend** — new Render "Static Site", root directory `doc-ui`, build command `npm run build`, publish directory `build`. Set env var:
   - `REACT_APP_API_URL` — your deployed backend URL

Free tier spins the backend down after 15 minutes idle; first request after that takes ~30-50s to wake up.

## API

`POST /api/convert/convert` (multipart/form-data)

| Field | Type |
|---|---|
| `file` | PDF/DOCX/XLSX, max 25MB |
| `enableAICompression` | boolean |

Response:

```json
{
  "fileName": "document.pdf",
  "markdownContent": "...",
  "chunks": ["chunk1", "chunk2"],
  "tokenReport": {
    "originalTokens": 12000,
    "cleanedTokens": 8500,
    "reductionPercent": 29.16,
    "tokensSaved": 3500
  },
  "totalPages": 88,
  "processedBatches": 9,
  "aiRequested": true,
  "aiFullyApplied": true,
  "aiAttemptedBatches": 9,
  "aiSucceededBatches": 9
}
```

## Roadmap

- Real BPE tokenizer instead of the chars/4 approximation
- Semantic (not fixed-size) chunking
- Embedding generation
