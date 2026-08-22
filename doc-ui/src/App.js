import React, { useState, useEffect, useRef } from "react";
import axios from "axios";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

const API_BASE = process.env.REACT_APP_API_URL || "http://localhost:5256";
const MAX_FILE_MB = 25;

const styles = `
  @import url('https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@400;500&display=swap');

  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

  :root {
    --bg: #0b0f1a;
    --surface: #131826;
    --surface2: #1a2135;
    --border: #232b40;
    --border2: #333e5c;
    --indigo: #4f46e5;
    --indigo-soft: #6366f1;
    --cyan: #22d3ee;
    --gradient: linear-gradient(135deg, var(--indigo), var(--cyan));
    --text: #e8ecf4;
    --muted: #7c88a6;
    --muted2: #a4b0c9;
    --success: #34d399;
    --success-bg: rgba(52, 211, 153, 0.1);
    --warning: #fbbf24;
    --warning-bg: rgba(251, 191, 36, 0.1);
    --danger: #f87171;
    --danger-bg: rgba(248, 113, 113, 0.1);
    --font-body: 'Inter', system-ui, sans-serif;
    --font-display: 'Space Grotesk', 'Inter', system-ui, sans-serif;
    --font-mono: 'JetBrains Mono', 'DM Mono', monospace;
    --ease: cubic-bezier(0.16, 1, 0.3, 1);
  }

  html { background: var(--bg); }

  body {
    background: var(--bg);
    color: var(--text);
    font-family: var(--font-body);
    min-height: 100vh;
  }

  @media (prefers-reduced-motion: reduce) {
    *, *::before, *::after {
      animation-duration: 0.01ms !important;
      transition-duration: 0.01ms !important;
    }
  }

  ::selection { background: var(--indigo); color: #fff; }

  @keyframes fadeUp {
    from { opacity: 0; transform: translateY(14px); }
    to   { opacity: 1; transform: translateY(0); }
  }
  @keyframes spin { to { transform: rotate(360deg); } }
  @keyframes borderRotate {
    to { --angle: 360deg; }
  }
  @property --angle {
    syntax: '<angle>';
    initial-value: 0deg;
    inherits: false;
  }

  .fade-up {
    animation: fadeUp 0.55s var(--ease) forwards;
    animation-delay: calc(var(--i, 0) * 70ms);
    opacity: 0;
  }

  .app {
    max-width: 880px;
    margin: 0 auto;
    padding: 56px 24px 96px;
  }

  @media (max-width: 640px) {
    .app { padding: 32px 16px 64px; }
  }

  /* ── Header ── */
  .header {
    margin-bottom: 48px;
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 20px;
    flex-wrap: wrap;
  }

  .header-eyebrow {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    font-size: 11px;
    letter-spacing: 0.16em;
    text-transform: uppercase;
    color: var(--cyan);
    margin-bottom: 14px;
    font-weight: 600;
  }

  .header-eyebrow-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--gradient);
  }

  .header-title {
    font-family: var(--font-display);
    font-size: clamp(1.9rem, 5vw, 2.9rem);
    font-weight: 700;
    line-height: 1.1;
    letter-spacing: -0.02em;
    color: var(--text);
  }

  .header-title .gradient-text {
    background: var(--gradient);
    -webkit-background-clip: text;
    background-clip: text;
    color: transparent;
  }

  .header-desc {
    margin-top: 14px;
    font-size: 13.5px;
    color: var(--muted2);
    line-height: 1.7;
    max-width: 460px;
  }

  .header-badges {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
  }

  .badge {
    font-size: 10.5px;
    font-family: var(--font-mono);
    letter-spacing: 0.02em;
    color: var(--muted2);
    background: var(--surface);
    border: 1px solid var(--border);
    padding: 5px 10px;
    border-radius: 999px;
    white-space: nowrap;
  }

  /* ── Upload Zone ── */
  .upload-zone {
    --angle: 0deg;
    border-radius: 14px;
    padding: 2px;
    background: linear-gradient(var(--bg), var(--bg)) padding-box,
                var(--border) border-box;
    border: 1px solid transparent;
    transition: background 0.25s var(--ease);
    position: relative;
  }

  .upload-zone.drag-over {
    background: linear-gradient(var(--surface), var(--surface)) padding-box,
                conic-gradient(from var(--angle), var(--indigo), var(--cyan), var(--indigo)) border-box;
    animation: borderRotate 2.5s linear infinite;
  }

  .upload-zone-inner {
    border-radius: 12px;
    padding: 44px 24px;
    text-align: center;
    cursor: pointer;
    background: var(--surface);
    position: relative;
    overflow: hidden;
    transition: background 0.2s var(--ease);
  }

  .upload-zone:hover .upload-zone-inner {
    background: var(--surface2);
  }

  .upload-zone-inner input[type="file"] {
    position: absolute;
    inset: 0;
    opacity: 0;
    cursor: pointer;
    width: 100%;
    height: 100%;
  }

  .upload-icon {
    width: 44px;
    height: 44px;
    margin: 0 auto 16px;
    border-radius: 12px;
    background: var(--surface2);
    border: 1px solid var(--border);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 19px;
  }

  .upload-label {
    font-size: 13.5px;
    color: var(--muted2);
  }

  .upload-label strong {
    color: var(--text);
    font-weight: 600;
  }

  .upload-hint {
    margin-top: 6px;
    font-size: 11px;
    font-family: var(--font-mono);
    color: var(--muted);
    letter-spacing: 0.04em;
  }

  .file-pill {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    margin-top: 18px;
    padding: 6px 14px;
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 999px;
    font-size: 12px;
    font-family: var(--font-mono);
    color: var(--muted2);
  }

  .file-pill-dot {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background: var(--cyan);
    flex-shrink: 0;
  }

  /* ── AI toggle panel ── */
  .ai-panel {
    margin-top: 16px;
    padding: 14px 16px;
    border-radius: 10px;
    background: var(--surface);
    border: 1px solid var(--border);
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
  }

  .ai-panel-label {
    font-size: 12.5px;
    color: var(--text);
    font-weight: 500;
  }

  .ai-panel-hint {
    display: block;
    margin-top: 2px;
    font-size: 10.5px;
    color: var(--muted);
    line-height: 1.5;
  }

  .switch {
    width: 40px;
    height: 22px;
    border-radius: 999px;
    background: var(--surface2);
    border: 1px solid var(--border2);
    position: relative;
    cursor: pointer;
    flex-shrink: 0;
    transition: background 0.25s var(--ease), border-color 0.25s var(--ease);
  }
  .switch.on {
    background: var(--gradient);
    border-color: transparent;
  }
  .switch::after {
    content: '';
    position: absolute;
    top: 2px;
    left: 2px;
    width: 16px;
    height: 16px;
    border-radius: 50%;
    background: #fff;
    transition: transform 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  }
  .switch.on::after { transform: translateX(18px); }

  /* ── Actions ── */
  .actions {
    display: flex;
    gap: 10px;
    margin-top: 20px;
    flex-wrap: wrap;
  }

  .btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 7px;
    padding: 11px 22px;
    border-radius: 9px;
    font-family: var(--font-body);
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    border: none;
    transition: transform 0.15s var(--ease), box-shadow 0.15s var(--ease), opacity 0.2s, background 0.2s;
    letter-spacing: 0.01em;
  }

  .btn:active:not(:disabled) { transform: scale(0.98); }
  .btn:disabled { opacity: 0.35; cursor: not-allowed; }

  .btn-primary {
    background: var(--gradient);
    color: #fff;
    box-shadow: 0 4px 16px -6px rgba(79, 70, 229, 0.55);
  }
  .btn-primary:hover:not(:disabled) {
    transform: translateY(-1px);
    box-shadow: 0 8px 22px -6px rgba(79, 70, 229, 0.7);
  }

  .btn-secondary {
    background: var(--surface2);
    color: var(--muted2);
    border: 1px solid var(--border);
  }
  .btn-secondary:hover:not(:disabled) {
    color: var(--text);
    border-color: var(--border2);
    transform: translateY(-1px);
  }

  @media (max-width: 560px) {
    .actions { flex-direction: column; }
    .btn { width: 100%; }
  }

  /* ── Status bar ── */
  .status-bar {
    margin-top: 20px;
    padding: 12px 16px;
    border-radius: 9px;
    font-size: 12.5px;
    display: flex;
    align-items: center;
    gap: 10px;
    border: 1px solid transparent;
    animation: fadeUp 0.3s var(--ease);
  }

  .status-bar.loading {
    background: var(--surface2);
    border-color: var(--border);
    color: var(--muted2);
  }
  .status-bar.success {
    background: var(--success-bg);
    border-color: rgba(52, 211, 153, 0.3);
    color: var(--success);
  }
  .status-bar.error {
    background: var(--danger-bg);
    border-color: rgba(248, 113, 113, 0.3);
    color: var(--danger);
  }

  .spinner {
    width: 14px;
    height: 14px;
    border: 2px solid var(--border2);
    border-top-color: var(--cyan);
    border-radius: 50%;
    animation: spin 0.7s linear infinite;
    flex-shrink: 0;
  }

  /* ── KPI stat grid ── */
  .stat-grid {
    margin-top: 20px;
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
    gap: 12px;
  }

  .stat-card {
    position: relative;
    padding: 16px 18px;
    border-radius: 12px;
    background: var(--surface);
    border: 1px solid var(--border);
    overflow: hidden;
    transition: border-color 0.2s var(--ease), transform 0.2s var(--ease);
  }
  .stat-card:hover {
    border-color: var(--border2);
    transform: translateY(-2px);
  }
  .stat-card::before {
    content: '';
    position: absolute;
    top: 0; left: 0; right: 0;
    height: 2px;
    background: var(--gradient);
    opacity: 0.8;
  }

  .stat-label {
    font-size: 10.5px;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: var(--muted);
    font-weight: 600;
  }

  .stat-value {
    margin-top: 8px;
    font-family: var(--font-mono);
    font-size: 22px;
    font-weight: 500;
    color: var(--text);
  }

  .stat-value.accent { color: var(--success); }

  .fallback-note {
    grid-column: 1 / -1;
    margin-top: 4px;
    padding: 10px 14px;
    border-radius: 9px;
    background: var(--warning-bg);
    border: 1px solid rgba(251, 191, 36, 0.3);
    color: var(--warning);
    font-size: 11.5px;
    line-height: 1.6;
  }

  .panel-title {
    font-family: var(--font-display);
    font-size: 13px;
    font-weight: 600;
    color: var(--text);
    margin-bottom: 2px;
  }

  /* ── Divider ── */
  .section-divider {
    display: flex;
    align-items: center;
    gap: 16px;
    margin: 48px 0 28px;
  }
  .section-divider-line {
    flex: 1;
    height: 1px;
    background: var(--border);
  }
  .section-divider-label {
    font-size: 11px;
    letter-spacing: 0.14em;
    text-transform: uppercase;
    color: var(--muted);
    font-family: var(--font-mono);
  }

  /* ── Chunk cards ── */
  .chunks-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 18px;
    flex-wrap: wrap;
    gap: 8px;
  }

  .chunks-count {
    font-size: 12px;
    color: var(--muted);
    font-family: var(--font-mono);
  }

  .chunk-card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 11px;
    margin-bottom: 12px;
    overflow: hidden;
    transition: border-color 0.2s var(--ease);
  }
  .chunk-card:hover { border-color: var(--border2); }

  .chunk-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px 16px;
    border-bottom: 1px solid var(--border);
    background: var(--surface2);
    cursor: pointer;
    user-select: none;
    gap: 8px;
    flex-wrap: wrap;
  }

  .chunk-meta {
    display: flex;
    align-items: center;
    gap: 10px;
    flex-wrap: wrap;
  }

  .chunk-index {
    font-size: 10px;
    letter-spacing: 0.08em;
    color: var(--cyan);
    text-transform: uppercase;
    font-family: var(--font-mono);
    font-weight: 600;
  }

  .chunk-chars {
    font-size: 10px;
    color: var(--muted);
    font-family: var(--font-mono);
  }

  .chunk-toggle {
    font-size: 11px;
    color: var(--muted);
    transition: transform 0.25s var(--ease);
  }
  .chunk-toggle.open { transform: rotate(180deg); }

  .copy-btn {
    font-size: 10.5px;
    font-family: var(--font-mono);
    color: var(--muted2);
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 4px 9px;
    cursor: pointer;
    transition: color 0.15s, border-color 0.15s;
  }
  .copy-btn:hover {
    color: var(--cyan);
    border-color: var(--border2);
  }

  .privacy-note {
    margin-top: 12px;
    font-size: 11px;
    color: var(--muted);
    display: flex;
    align-items: center;
    gap: 6px;
  }

  .chunk-body {
    padding: 20px;
    font-size: 13px;
    line-height: 1.75;
    color: var(--muted2);
  }

  .chunk-body h1, .chunk-body h2, .chunk-body h3 {
    font-family: var(--font-display);
    font-weight: 600;
    color: var(--text);
    margin: 1em 0 0.4em;
  }
  .chunk-body h1 { font-size: 1.4em; }
  .chunk-body h2 { font-size: 1.2em; }
  .chunk-body h3 { font-size: 1.08em; }
  .chunk-body p { margin-bottom: 0.8em; }
  .chunk-body code {
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 1px 5px;
    font-size: 12px;
    font-family: var(--font-mono);
    color: var(--cyan);
  }
  .chunk-body pre {
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 8px;
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
  .chunk-body table-wrap { display: block; overflow-x: auto; }
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
  .chunk-body a { color: var(--cyan); }
  .chunk-body ul, .chunk-body ol { padding-left: 1.4em; margin-bottom: 0.8em; }
  .chunk-body li { margin-bottom: 0.3em; }
  .chunk-body blockquote {
    border-left: 2px solid var(--indigo-soft);
    padding-left: 14px;
    color: var(--muted);
    margin: 10px 0;
  }

  .scroll-x { overflow-x: auto; }

  /* ── Empty state ── */
  .empty-state {
    text-align: center;
    padding: 64px 20px;
    color: var(--muted);
  }
  .empty-state-icon {
    font-size: 30px;
    margin-bottom: 12px;
    opacity: 0.5;
  }
  .empty-state p { font-size: 13px; }

  /* ── Footer ── */
  .footer {
    margin-top: 64px;
    padding-top: 24px;
    border-top: 1px solid var(--border);
    font-size: 11px;
    color: var(--muted);
    text-align: center;
    font-family: var(--font-mono);
  }
`;

function useCountUp(target, duration = 900) {
  const [value, setValue] = useState(0);
  const reduced = useRef(
    typeof window !== "undefined" &&
      window.matchMedia &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches,
  );

  useEffect(() => {
    if (target == null || Number.isNaN(target)) return;
    if (reduced.current) {
      setValue(target);
      return;
    }
    let raf;
    const startTime = performance.now();
    function step(now) {
      const progress = Math.min((now - startTime) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      setValue(Math.round(target * eased));
      if (progress < 1) raf = requestAnimationFrame(step);
    }
    raf = requestAnimationFrame(step);
    return () => cancelAnimationFrame(raf);
  }, [target, duration]);

  return value;
}

function StatCard({ label, value, accent, index, suffix = "" }) {
  const animated = useCountUp(typeof value === "number" ? value : null);
  const display = typeof value === "number" ? animated.toLocaleString() : value;
  return (
    <div className="stat-card fade-up" style={{ "--i": index }}>
      <div className="stat-label">{label}</div>
      <div className={`stat-value ${accent ? "accent" : ""}`}>
        {display}
        {suffix}
      </div>
    </div>
  );
}

function CopyButton({ text }) {
  const [copied, setCopied] = useState(false);
  return (
    <button
      className="copy-btn"
      onClick={(e) => {
        e.stopPropagation();
        navigator.clipboard.writeText(text);
        setCopied(true);
        setTimeout(() => setCopied(false), 1500);
      }}
    >
      {copied ? "Copied" : "Copy"}
    </button>
  );
}

function ChunkCard({ chunk, index }) {
  const [open, setOpen] = useState(index === 0);
  return (
    <div className="chunk-card fade-up" style={{ "--i": Math.min(index, 6) }}>
      <div className="chunk-header" onClick={() => setOpen(!open)}>
        <div className="chunk-meta">
          <span className="chunk-index">Chunk #{index + 1}</span>
          <span className="chunk-chars">{chunk.length} chars</span>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
          <CopyButton text={chunk} />
          <span className={`chunk-toggle ${open ? "open" : ""}`}>▾</span>
        </div>
      </div>
      {open && (
        <div className="chunk-body">
          <div className="scroll-x">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{chunk}</ReactMarkdown>
          </div>
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
  const [aiFullyApplied, setAiFullyApplied] = useState(null);

  const handleFile = (f) => {
    if (!f) return;
    if (f.size > MAX_FILE_MB * 1024 * 1024) {
      setStatus({
        type: "error",
        msg: `File is over the ${MAX_FILE_MB}MB limit on this free instance.`,
      });
      return;
    }
    setFile(f);
    setStatus(null);
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
      setAiFullyApplied(null);
      const isLarge = file.size > 5 * 1024 * 1024;
      setStatus({
        type: "loading",
        msg: isLarge
          ? "Converting document — large files with AI cleanup can take up to a minute…"
          : "Converting document…",
      });
      const res = await axios.post(`${API_BASE}/api/convert/convert`, formData);
      setChunks(res.data.chunks);
      setMarkdown(res.data.markdownContent);
      setTokenReport(res.data.tokenReport);
      setTotalPages(res.data.totalPages);
      setProcessedBatches(res.data.processedBatches);
      setAiFullyApplied(res.data.aiFullyApplied);

      // Report what actually happened, not what was requested — if Groq
      // was rate-limited/unavailable the backend already fell back to
      // basic cleaning, and the UI should say so instead of claiming AI ran.
      let aiNote = "Normal Mode";
      if (aiMode) {
        aiNote = res.data.aiFullyApplied
          ? "AI cleanup applied (Groq)"
          : "AI unavailable — used basic cleanup instead";
      }
      setStatus({
        type: "success",
        msg: `Done — ${res.data.chunks.length} chunks • ${res.data.totalPages} pages • ${aiNote}`,
      });
    } catch (err) {
      console.error(err);
      setStatus({
        type: "error",
        msg: err?.response?.data?.message || "Conversion failed. Check console.",
      });
    } finally {
      setLoading(false);
    }
  };

  const downloadMarkdown = () => {
    if (!chunks.length) return;
    const blob = new Blob([markdown || chunks.join("\n\n---\n\n")], {
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

  const downloadJson = () => {
    if (!chunks.length) return;
    const payload = {
      fileName: file?.name || "document",
      chunkCount: chunks.length,
      chunks: chunks.map((text, i) => ({ index: i, text })),
      tokenReport,
      totalPages,
      processedBatches,
      aiFullyApplied,
    };
    const blob = new Blob([JSON.stringify(payload, null, 2)], {
      type: "application/json",
    });
    const url = URL.createObjectURL(blob);
    const a = Object.assign(document.createElement("a"), {
      href: url,
      download: "chunks.json",
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
          <div className="fade-up" style={{ "--i": 0 }}>
            <p className="header-eyebrow">
              <span className="header-eyebrow-dot" />
              Document Conversion
            </p>
            <h1 className="header-title">
              Doc → <span className="gradient-text">Markdown</span>
            </h1>
            <p className="header-desc">
              Upload a PDF, DOCX, or XLSX file. Get back clean, chunked
              Markdown — with real LLM cleanup and RAG-ready exports.
            </p>
          </div>
          <div className="header-badges fade-up" style={{ "--i": 1 }}>
            <span className="badge">Groq LLM</span>
            <span className="badge">RAG-ready</span>
            <span className="badge">Free</span>
          </div>
        </header>

        {/* Upload zone */}
        <div
          className={`upload-zone fade-up ${dragOver ? "drag-over" : ""}`}
          style={{ "--i": 2 }}
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
          <div className="upload-zone-inner">
            <input
              type="file"
              accept=".pdf,.docx,.xlsx"
              onChange={(e) => handleFile(e.target.files[0])}
            />
            <div className="upload-icon">📂</div>
            <p className="upload-label">
              <strong>Click to browse</strong> or drag & drop
            </p>
            <p className="upload-hint">PDF · DOCX · XLSX · max {MAX_FILE_MB}MB</p>
            {file && (
              <div className="file-pill">
                <div className="file-pill-dot" />
                {file.name}
              </div>
            )}
          </div>
        </div>
        <p className="privacy-note">
          🔒 Files are processed in memory and deleted immediately after
          conversion — never stored.
        </p>

        {/* AI toggle */}
        <div className="ai-panel fade-up" style={{ "--i": 3 }}>
          <div>
            <span className="ai-panel-label">AI Cleanup (Groq)</span>
            <span className="ai-panel-hint">
              Real LLM cleanup, free tier — may briefly fall back to basic
              cleaning if rate-limited
            </span>
          </div>
          <div
            className={`switch ${aiMode ? "on" : ""}`}
            role="switch"
            aria-checked={aiMode}
            tabIndex={0}
            onClick={() => setAiMode(!aiMode)}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                setAiMode(!aiMode);
              }
            }}
          />
        </div>

        {/* Actions */}
        <div className="actions fade-up" style={{ "--i": 4 }}>
          <button className="btn btn-primary" onClick={uploadFile} disabled={loading || !file}>
            {loading ? (
              <>
                <span className="spinner" /> Converting…
              </>
            ) : (
              "↑ Convert"
            )}
          </button>
          <button className="btn btn-secondary" onClick={downloadMarkdown} disabled={!chunks.length}>
            ↓ Download .md
          </button>
          <button className="btn btn-secondary" onClick={downloadJson} disabled={!chunks.length}>
            ↓ Download chunks.json
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
          <>
            <p className="panel-title" style={{ marginTop: "24px" }}>
              Token Optimization
            </p>
            <div className="stat-grid">
              <StatCard label="Original (approx.)" value={tokenReport.originalTokens} index={0} />
              <StatCard label="Cleaned (approx.)" value={tokenReport.cleanedTokens} index={1} />
              <StatCard label="Tokens Saved" value={tokenReport.tokensSaved} index={2} />
              <StatCard
                label="Reduction"
                value={tokenReport.reductionPercent}
                suffix="%"
                accent
                index={3}
              />
              {aiFullyApplied === false && (
                <div className="fallback-note">
                  AI cleanup was unavailable for this run (rate limit or
                  outage) — numbers above reflect basic fallback cleaning only.
                </div>
              )}
            </div>
          </>
        )}

        {totalPages > 0 && (
          <>
            <p className="panel-title" style={{ marginTop: "24px" }}>
              Document Info
            </p>
            <div className="stat-grid">
              <StatCard label="Total Pages" value={totalPages} index={0} />
              <StatCard label="Batches" value={processedBatches} index={1} />
              <StatCard label="Chunks" value={chunks.length} index={2} />
            </div>
          </>
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
                <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: "8px" }}>
                  <CopyButton text={markdown} />
                </div>
                <div className="chunk-body">
                  <div className="scroll-x">
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>{markdown}</ReactMarkdown>
                  </div>
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

        <footer className="footer">Doc → Markdown · runs on a free instance, limits apply</footer>
      </div>
    </>
  );
}
