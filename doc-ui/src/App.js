import React, { useState } from "react";
import axios from "axios";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

const styles = `
  @import url('https://fonts.googleapis.com/css2?family=Instrument+Serif:ital@0;1&family=DM+Mono:wght@300;400;500&display=swap');

  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

  :root {
    --bg: #0a0a0b;
    --surface: #111113;
    --surface2: #18181b;
    --border: #27272a;
    --border2: #3f3f46;
    --accent: #e8d5b0;
    --accent2: #c9a96e;
    --text: #fafafa;
    --muted: #71717a;
    --muted2: #a1a1aa;
    --green: #86efac;
    --green-bg: #052e16;
    --red: #fca5a5;
    --red-bg: #450a0a;
  }

  body {
    background: var(--bg);
    color: var(--text);
    font-family: 'DM Mono', monospace;
    min-height: 100vh;
  }

  .app {
    max-width: 900px;
    margin: 0 auto;
    padding: 48px 24px 80px;
  }

  /* ── Header ── */
  .header {
    margin-bottom: 56px;
  }

  .header-eyebrow {
    font-size: 11px;
    letter-spacing: 0.18em;
    text-transform: uppercase;
    color: var(--accent2);
    margin-bottom: 12px;
  }

  .header-title {
    font-family: 'Instrument Serif', serif;
    font-size: clamp(2.2rem, 5vw, 3.4rem);
    font-weight: 400;
    line-height: 1.1;
    color: var(--text);
    letter-spacing: -0.02em;
  }

  .header-title em {
    font-style: italic;
    color: var(--accent);
  }

  .header-desc {
    margin-top: 14px;
    font-size: 13px;
    color: var(--muted2);
    line-height: 1.7;
    max-width: 480px;
  }

  /* ── Upload Zone ── */
  .upload-zone {
    border: 1px dashed var(--border2);
    border-radius: 12px;
    padding: 40px 24px;
    text-align: center;
    cursor: pointer;
    transition: border-color 0.2s, background 0.2s;
    background: var(--surface);
    position: relative;
    overflow: hidden;
  }

  .upload-zone:hover, .upload-zone.drag-over {
    border-color: var(--accent2);
    background: var(--surface2);
  }

  .upload-zone input[type="file"] {
    position: absolute;
    inset: 0;
    opacity: 0;
    cursor: pointer;
    width: 100%;
    height: 100%;
  }

  .upload-icon {
    width: 40px;
    height: 40px;
    margin: 0 auto 14px;
    border-radius: 10px;
    background: var(--surface2);
    border: 1px solid var(--border);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
  }

  .upload-label {
    font-size: 13px;
    color: var(--muted2);
  }

  .upload-label strong {
    color: var(--accent);
    font-weight: 500;
  }

  .upload-hint {
    margin-top: 6px;
    font-size: 11px;
    color: var(--muted);
    letter-spacing: 0.05em;
  }

  /* ── File pill ── */
  .file-pill {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    margin-top: 16px;
    padding: 6px 12px;
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 20px;
    font-size: 12px;
    color: var(--muted2);
  }

  .file-pill-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--accent2);
    flex-shrink: 0;
  }

  /* ── Actions ── */
  .actions {
    display: flex;
    gap: 10px;
    margin-top: 20px;
  }

  .btn {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    padding: 10px 20px;
    border-radius: 8px;
    font-family: 'DM Mono', monospace;
    font-size: 13px;
    font-weight: 500;
    cursor: pointer;
    border: none;
    transition: opacity 0.15s, transform 0.1s;
    letter-spacing: 0.02em;
  }

  .btn:active { transform: scale(0.98); }
  .btn:disabled { opacity: 0.4; cursor: not-allowed; }

  .btn-primary {
    background: var(--accent);
    color: #0a0a0b;
  }

  .btn-primary:hover:not(:disabled) { opacity: 0.88; }

  .btn-secondary {
    background: var(--surface2);
    color: var(--muted2);
    border: 1px solid var(--border);
  }

  .btn-secondary:hover:not(:disabled) {
    color: var(--text);
    border-color: var(--border2);
  }

  /* ── Progress / status bar ── */
  .status-bar {
    margin-top: 20px;
    padding: 12px 16px;
    border-radius: 8px;
    font-size: 12px;
    display: flex;
    align-items: center;
    gap: 10px;
    border: 1px solid transparent;
  }

  .status-bar.loading {
    background: var(--surface2);
    border-color: var(--border);
    color: var(--muted2);
  }

  .status-bar.success {
    background: var(--green-bg);
    border-color: #166534;
    color: var(--green);
  }

  .status-bar.error {
    background: var(--red-bg);
    border-color: #991b1b;
    color: var(--red);
  }

  .spinner {
    width: 14px;
    height: 14px;
    border: 2px solid var(--border2);
    border-top-color: var(--accent);
    border-radius: 50%;
    animation: spin 0.7s linear infinite;
    flex-shrink: 0;
  }

  @keyframes spin { to { transform: rotate(360deg); } }

  /* ── Divider ── */
  .section-divider {
    display: flex;
    align-items: center;
    gap: 16px;
    margin: 48px 0 32px;
  }

  .section-divider-line {
    flex: 1;
    height: 1px;
    background: var(--border);
  }

  .section-divider-label {
    font-size: 11px;
    letter-spacing: 0.15em;
    text-transform: uppercase;
    color: var(--muted);
  }

  /* ── Chunk cards ── */
  .chunks-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 20px;
  }

  .chunks-count {
    font-size: 12px;
    color: var(--muted);
  }

  .chunk-card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 10px;
    margin-bottom: 12px;
    overflow: hidden;
    transition: border-color 0.2s;
  }

  .chunk-card:hover { border-color: var(--border2); }

  .chunk-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 10px 16px;
    border-bottom: 1px solid var(--border);
    background: var(--surface2);
    cursor: pointer;
    user-select: none;
  }

  .chunk-meta {
    display: flex;
    align-items: center;
    gap: 10px;
  }

  .chunk-index {
    font-size: 10px;
    letter-spacing: 0.1em;
    color: var(--accent2);
    text-transform: uppercase;
  }

  .chunk-chars {
    font-size: 10px;
    color: var(--muted);
  }

  .chunk-toggle {
    font-size: 11px;
    color: var(--muted);
    transition: transform 0.2s;
  }

  .chunk-toggle.open { transform: rotate(180deg); }

  .chunk-body {
    padding: 20px;
    font-size: 13px;
    line-height: 1.75;
    color: var(--muted2);
  }

  /* Markdown inside chunk */
  .chunk-body h1, .chunk-body h2, .chunk-body h3 {
    font-family: 'Instrument Serif', serif;
    font-weight: 400;
    color: var(--text);
    margin: 1em 0 0.4em;
  }
  .chunk-body h1 { font-size: 1.5em; }
  .chunk-body h2 { font-size: 1.25em; }
  .chunk-body h3 { font-size: 1.1em; }
  .chunk-body p { margin-bottom: 0.8em; }
  .chunk-body code {
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 1px 5px;
    font-size: 12px;
    color: var(--accent);
  }
  .chunk-body pre {
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 14px;
    overflow-x: auto;
    margin: 10px 0;
  }
  .chunk-body pre code {
    background: none;
    border: none;
    padding: 0;
    color: var(--muted2);
  }
  .chunk-body table {
    width: 100%;
    border-collapse: collapse;
    font-size: 12px;
    margin: 10px 0;
  }
  .chunk-body th, .chunk-body td {
    border: 1px solid var(--border);
    padding: 8px 12px;
    text-align: left;
  }
  .chunk-body th {
    background: var(--surface2);
    color: var(--text);
    font-size: 11px;
    letter-spacing: 0.05em;
    text-transform: uppercase;
  }
  .chunk-body a { color: var(--accent2); }
  .chunk-body ul, .chunk-body ol { padding-left: 1.4em; margin-bottom: 0.8em; }
  .chunk-body li { margin-bottom: 0.3em; }
  .chunk-body blockquote {
    border-left: 2px solid var(--accent2);
    padding-left: 14px;
    color: var(--muted);
    margin: 10px 0;
  }

  /* ── Empty state ── */
  .empty-state {
    text-align: center;
    padding: 60px 20px;
    color: var(--muted);
  }
  .empty-state-icon { font-size: 32px; margin-bottom: 12px; opacity: 0.4; }
  .empty-state p { font-size: 13px; }
`;

function ChunkCard({ chunk, index }) {
  const [open, setOpen] = useState(true);
  return (
    <div className="chunk-card">
      <div className="chunk-header" onClick={() => setOpen(!open)}>
        <div className="chunk-meta">
          <span className="chunk-index">Chunk #{index + 1}</span>
          <span className="chunk-chars">{chunk.length} chars</span>
        </div>
        <span className={`chunk-toggle ${open ? "open" : ""}`}>▾</span>
      </div>
      {open && (
        <div className="chunk-body">
          <ReactMarkdown remarkPlugins={[remarkGfm]}>{chunk}</ReactMarkdown>
        </div>
      )}
    </div>
  );
}

export default function App() {
  const [file, setFile] = useState(null);
  const [chunks, setChunks] = useState([]);
  const [markdown, setMarkdown] = useState("");
  const [tokenReport, setTokenReport] = useState(null);
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState(null); // { type: 'success'|'error'|'loading', msg }
  const [dragOver, setDragOver] = useState(false);
  const [totalPages, setTotalPages] = useState(0);
  const [processedBatches, setProcessedBatches] = useState(0);
  const [aiMode, setAiMode] = useState(true);

  const handleFile = (f) => {
    if (f) {
      setFile(f);
      setStatus(null);
    }
  };

  const uploadFile = async () => {
    if (!file) {
      setStatus({ type: "error", msg: "Please select a file first." });
      return;
    }
    const formData = new FormData();
    formData.append("file", file);
    formData.append("enableAICompression", aiMode);
    try {
      setLoading(true);
      setStatus({ type: "loading", msg: "Converting document…" });
      const res = await axios.post(
        "https://localhost:7286/api/convert/convert",
        formData,
      );
      setChunks(res.data.chunks);
      setMarkdown(res.data.markdownContent);
      setTokenReport(res.data.tokenReport);
      setTotalPages(res.data.totalPages);
      setProcessedBatches(res.data.processedBatches);
      setStatus({
        type: "success",
        msg: `Done — ${res.data.chunks.length} chunks • ${res.data.totalPages} pages • ${aiMode ? "AI Mode ON 🔥" : "Normal Mode"}`,
      });
    } catch (err) {
      console.error(err);
      setStatus({
        type: "error",
        msg:
          err?.response?.data?.message || "Conversion failed. Check console.",
      });
    } finally {
      setLoading(false);
    }
  };

  const downloadMarkdown = () => {
    if (!chunks.length) return;
    const blob = new Blob([chunks.join("\n\n---\n\n")], {
      type: "text/markdown",
    });
    const url = URL.createObjectURL(blob);
    const a = Object.assign(document.createElement("a"), {
      href: url,
      download: "converted.md",
    });
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return (
    <>
      <style>{styles}</style>
      <div className="app">
        {/* Header */}
        <header className="header">
          <p className="header-eyebrow">Conversion Tool</p>
          <h1 className="header-title">
            Doc → <em>Markdown</em>
          </h1>
          <p className="header-desc">
            Upload a PDF, DOCX, or XLSX file and get back clean, structured
            Markdown — chunked and ready to use.
          </p>
        </header>

        {/* Upload zone */}
        <div
          className={`upload-zone ${dragOver ? "drag-over" : ""}`}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            handleFile(e.dataTransfer.files[0]);
          }}
        >
          <input
            type="file"
            accept=".pdf,.docx,.xlsx"
            onChange={(e) => handleFile(e.target.files[0])}
          />
          <div className="upload-icon">📂</div>
          <p className="upload-label">
            <strong>Click to browse</strong> or drag & drop
          </p>
          <p className="upload-hint">PDF · DOCX · XLSX</p>
          {file && (
            <div className="file-pill">
              <div className="file-pill-dot" />
              {file.name}
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="actions">
          <div style={{ marginTop: "10px" }}>
            <div
              style={{
                marginTop: "12px",
                padding: "10px 14px",
                borderRadius: "8px",
                background: "#111113",
                border: "1px solid #27272a",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
              }}
            >
              <div style={{ display: "flex", flexDirection: "column" }}>
                <span
                  style={{
                    fontSize: "12px",
                    color: "#fafafa",
                    fontWeight: 500,
                  }}
                >
                  AI Compression Mode 🔥
                </span>
                <span style={{ fontSize: "10px", color: "#71717a" }}>
                  Reduce tokens for AI usage (may slightly change wording)
                </span>
              </div>

              <label
                style={{
                  position: "relative",
                  display: "inline-block",
                  width: "36px",
                  height: "20px",
                }}
              >
                <input
                  type="checkbox"
                  checked={aiMode}
                  onChange={(e) => setAiMode(e.target.checked)}
                  style={{ opacity: 0, width: 0, height: 0 }}
                />
                <span
                  style={{
                    position: "absolute",
                    cursor: "pointer",
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: aiMode ? "#e8d5b0" : "#27272a",
                    borderRadius: "20px",
                    transition: "0.3s",
                  }}
                >
                  <span
                    style={{
                      position: "absolute",
                      height: "14px",
                      width: "14px",
                      left: aiMode ? "18px" : "3px",
                      bottom: "3px",
                      backgroundColor: "#0a0a0b",
                      borderRadius: "50%",
                      transition: "0.3s",
                    }}
                  />
                </span>
              </label>
            </div>
          </div>
          <button
            className="btn btn-primary"
            onClick={uploadFile}
            disabled={loading || !file}
          >
            {loading ? (
              <>
                <span className="spinner" /> Converting…
              </>
            ) : (
              "↑ Convert"
            )}
          </button>
          <button
            className="btn btn-secondary"
            onClick={downloadMarkdown}
            disabled={!chunks.length}
          >
            ↓ Download .md
          </button>
        </div>

        {/* Status */}
        {status && (
          <div className={`status-bar ${status.type}`}>
            {status.type === "loading" && <span className="spinner" />}
            {status.type === "success" && <span>✓</span>}
            {status.type === "error" && <span>✕</span>}
            {status.msg}
          </div>
        )}

        {tokenReport && (
          <div
            style={{
              marginTop: "20px",
              padding: "16px",
              borderRadius: "10px",
              background: "#111113",
              border: "1px solid #27272a",
            }}
          >
            <h3 style={{ marginBottom: "10px" }}>📊 Token Optimization</h3>

            <p>Original Tokens: {tokenReport.originalTokens}</p>
            <p>Cleaned Tokens: {tokenReport.cleanedTokens}</p>
            <p>
              Tokens Saved:{" "}
              {tokenReport.tokensSaved ??
                tokenReport.originalTokens - tokenReport.cleanedTokens}
            </p>

            <p style={{ color: "#86efac", fontWeight: "bold" }}>
              Reduction: {tokenReport.reductionPercent}%
            </p>
          </div>
        )}

        {totalPages > 0 && (
          <div
            style={{
              marginTop: "20px",
              padding: "16px",
              borderRadius: "10px",
              background: "#111113",
              border: "1px solid #27272a",
            }}
          >
            <h3 style={{ marginBottom: "10px" }}>📄 Document Info</h3>

            <p>Total Pages: {totalPages}</p>
            <p>Processed Batches: {processedBatches}</p>
            <p>Chunks: {chunks.length}</p>
          </div>
        )}

        {/* Results */}
        {chunks.length > 0 && (
          <>
            <div className="section-divider">
              <div className="section-divider-line" />
              <span className="section-divider-label">Output</span>
              <div className="section-divider-line" />
            </div>
            <div className="chunks-header">
              <span className="chunks-count">
                {chunks.length} chunk{chunks.length !== 1 ? "s" : ""}
              </span>
            </div>
            {chunks.map((chunk, i) => (
              <ChunkCard key={i} chunk={chunk} index={i} />
            ))}

            {markdown && (
              <>
                <div className="section-divider">
                  <div className="section-divider-line" />
                  <span className="section-divider-label">Full Document</span>
                  <div className="section-divider-line" />
                </div>

                <div className="chunk-body">
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>
                    {markdown}
                  </ReactMarkdown>
                </div>
              </>
            )}
          </>
        )}

        {!chunks.length && !loading && (
          <div className="empty-state">
            <div className="empty-state-icon">◌</div>
            <p>No output yet. Upload a document to get started.</p>
          </div>
        )}
      </div>
    </>
  );
}
