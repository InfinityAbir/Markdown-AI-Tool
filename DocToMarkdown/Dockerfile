# Build context: DocToMarkdown/ (Render "Root Directory" = DocToMarkdown)

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY DocToMarkdown.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish DocToMarkdown.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Pandoc (DOCX/XLSX), Poppler (pdfinfo/pdftotext/pdftoppm for PDF), and
# Tesseract (OCR fallback for scanned/image-only PDFs) — the whole
# reason this can't run on plain shared/IIS hosting.
RUN apt-get update \
    && apt-get install -y --no-install-recommends poppler-utils pandoc tesseract-ocr tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Uploads are deleted immediately after each request (see
# ConversionService), but the process still needs somewhere to write
# them transiently, and the base image's non-root "app" user needs
# permission on it.
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app
USER app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Render injects $PORT at runtime; Kestrel must bind to it.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet DocToMarkdown.dll"]
