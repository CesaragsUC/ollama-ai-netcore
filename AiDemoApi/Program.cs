using AiDemoApi;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OllamaSharp;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Start Serilog configuration
var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration) 
    .Enrich.FromLogContext()
    .WriteTo.Console() // Write logs to console
    .WriteTo.Seq(builder.Configuration["Serilog:SeqServerUrl"] ?? "http://localhost:5341") // Write to Seq server
    .CreateLogger();

Log.Logger = logger;

builder.Host.UseSerilog();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

builder.Services.AddOpenApi();


builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));

builder.Services.AddSingleton<IOllamaApiClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;

    var http = new HttpClient(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15)
    })
    {
        BaseAddress = new Uri(opts.Endpoint),
        Timeout = Timeout.InfiniteTimeSpan 
    };
    return new OllamaApiClient(http);
});


builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ollama API", Version = "v1" }));

// store de histórico em memória
builder.Services.AddSingleton<IChatStore, InMemoryChatStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
