using DocToMarkdown.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ======================
// 🔥 SERVICES
// ======================

builder.Services.AddControllers();

// CORS (for React frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DocToMarkdown API",
        Version = "v1"
    });

    // Fix file upload in Swagger
    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

// Dependency Injection
builder.Services.AddScoped<IConversionService, ConversionService>();

var app = builder.Build();

// ======================
// 🔥 MIDDLEWARE
// ======================

// Enable CORS
app.UseCors("AllowReact");

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocToMarkdown API v1");
    c.RoutePrefix = string.Empty; // Swagger at root
});

// Static files (uploads)
app.UseStaticFiles();

// Routing
app.MapControllers();

app.Run();