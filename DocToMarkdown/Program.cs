using System.Threading.RateLimiting;
using DocToMarkdown.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ======================
// SERVICES
// ======================

builder.Services.AddControllers();

// CORS — locked to configured origins (appsettings "AllowedOrigins" /
// env var AllowedOrigins__0 etc.), not wide open.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Rate limiting — free-tier Groq + shared hosting can't take unlimited load
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("convert", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("chat", opt =>
    {
        opt.PermitLimit = 20;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

builder.Services.AddResponseCompression();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DocToMarkdown API",
        Version = "v1"
    });

    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

builder.Services.AddHttpClient<GroqService>(client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
});

builder.Services.AddScoped<IConversionService, ConversionService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddSingleton<ConversionJobStore>();

var app = builder.Build();

// ======================
// MIDDLEWARE
// ======================

app.UseResponseCompression();
app.UseCors("AllowFrontend");
app.UseRateLimiter();

// Swagger only in Development — don't expose API internals in production
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocToMarkdown API v1");
        c.RoutePrefix = string.Empty;
    });
}

// Render's default health check hits "/" — Swagger no longer owns that
// route in production, so give it something to check.
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "DocToMarkdown API" }));

app.MapControllers();

app.Run();