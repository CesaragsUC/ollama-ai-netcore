using Microsoft.AspNetCore.Mvc;
using OllamaSharp.Models;
using System.Text;
using UglyToad.PdfPig;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.Diagnostics;
using Serilog;
using System.IO.Pipelines;


namespace AiDemoApi.Controllers;

public class OllamaController : Controller
{
    private readonly IChatStore _store;
    private readonly IOllamaApiClient _ollama;
    private readonly Stopwatch _timer = new();
    private readonly OllamaOptions _opts;

    public OllamaController(
        IChatStore store,
        IOptions<OllamaOptions> options,
        IOllamaApiClient ollama)
    {
        _store = store;
        _opts = options.Value;
        _ollama = ollama;
    }

    [HttpPost("prompt")]
    public async Task<IActionResult> Send([FromBody] SendPromptDto dto, CancellationToken ct)
    {
        _timer.Restart();

        Log.Information("Initializing process");

        var history = _store.Get(dto.ConversationId);
        _store.Append(dto.ConversationId, dto);

        var req = new GenerateRequest
        {
            Model = _opts.ChatModelPhi3,
            Prompt = dto.Prompt,
            Stream = true
        };

        var sb = new StringBuilder();

        // GenerateAsync → Ideal para “uma pergunta → uma resposta”, sem imagens.

        await foreach (var chunk in _ollama.GenerateAsync(req, ct))
        {
            if (!string.IsNullOrEmpty(chunk.Response))
                sb.Append(chunk.Response);
        }

        var answer = sb.ToString();

        dto = dto with { Assistent = answer };

        _store.Append(dto.ConversationId, dto);

        _timer.Stop();

        Log.Warning(@"Process took {Elapsed:hh\:mm\:ss\.fff}", _timer.Elapsed);

        return Ok(new { message = answer });

    }

    [HttpPost("prompt-stream")]
    public async Task SendStream([FromBody] SendPromptDto dto, CancellationToken ct)
    {
        _timer.Restart();

        Log.Information("Initializing process");

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Add("X-Accel-Buffering", "no");

        var req = new GenerateRequest
        {
            Model = _opts.ChatModelPhi3,
            Prompt = dto.Prompt,
            Stream = true
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _ollama.GenerateAsync(req, ct))
        {
            var piece = chunk.Response;
            if (!string.IsNullOrEmpty(piece))
            {
                sb.Append(piece);
                await Response.WriteAsync("data: ", ct);
                await Response.WriteAsync(piece, ct);
                await Response.WriteAsync("\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            if (chunk.Done) break;
        }

        _timer.Stop();

        //var finalAnswer = sb.ToString();

        Log.Warning(@"Process took {Elapsed:hh\:mm\:ss\.fff}", _timer.Elapsed);

    }

    [HttpPost("prompt-files")]
    [DisableRequestSizeLimit] // remova/ajuste se quiser um limite
    public async Task<IActionResult> SendPromptFiles(
            [FromForm] SendPromptDto dto,
            [FromForm] List<IFormFile> files,
            CancellationToken ct)
    {
        _timer.Restart();

        Log.Information("Initializing process");

        var imagesBase64 = new List<string>();
        var pdfTextSb = new StringBuilder();

        foreach (var file in files ?? Enumerable.Empty<IFormFile>())
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                // monta data URI base64 para o chat multimodal
                var b64 = Convert.ToBase64String(bytes, Base64FormattingOptions.None);
                imagesBase64.Add(b64);
            }
            else if (string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                // extrai texto do PDF
                var text = ExtractTextFromPdf(bytes);

                // Se vier vazio, provavelmente é PDF escaneado -> enta poderia usar : https://github.com/tesseract-ocr/tesseract
                // if (string.IsNullOrWhiteSpace(text)) text = await RunOcrAsync(bytes);

                // limite simples (evita estourar contexto)
                const int maxCharsPerPdf = 12000;
                if (text.Length > maxCharsPerPdf)
                    text = text.Substring(0, maxCharsPerPdf) + "\n...[texto truncado]...";

                pdfTextSb.AppendLine($"[PDF: {file.FileName}]");
                pdfTextSb.AppendLine(text);
                pdfTextSb.AppendLine();
            }
            else
            {
                // outros tipos — você pode tratar .docx/.txt etc. aqui
                pdfTextSb.AppendLine($"[Arquivo não suportado no momento: {file.FileName} ({file.ContentType})]");
            }
        }

        // prompt final: prompt do usuário +  conteúdo extraído dos PDFs
        var finalPrompt = new StringBuilder(dto.Prompt);
        if (pdfTextSb.Length > 0)
        {
            finalPrompt.AppendLine("\n\n# Conteúdo dos PDFs (extraído):\n");
            finalPrompt.AppendLine(pdfTextSb.ToString());
        }

        var images = imagesBase64
            .Select(x => x)
            .ToArray();

        // Se tem imagens ->  modelo VISION via Chat
        if (imagesBase64.Count > 0)
        {
            var chatReq = new ChatRequest
            {
                Model = _opts.ChatModelVision,
                Messages = new List<Message> {
                    new Message {
                        Role    = "user",
                        Content = finalPrompt.ToString(),
                        Images  = images
                    }
                }
            };

            // ChatAsync → suporte  imagens (modelos vision).
            var stBuilder = new StringBuilder();
            await foreach (var chunk in _ollama.ChatAsync(chatReq, ct))
            {
                // em stream de chat o texto vem em chunk.Message.Content
                var piece = chunk.Message?.Content;
                if (!string.IsNullOrEmpty(piece))
                    stBuilder.Append(piece);

                if (chunk.Done) break;
            }

            _timer.Stop();

            Log.Warning(@"Process with file took {Elapsed:hh\:mm\:ss\.fff}", _timer.Elapsed);

            var answer = stBuilder.ToString();
            return Ok(new { message = answer });
        }

        // Sem imagens
        var genReq = new GenerateRequest
        {
            Model = _opts.ChatModelPhi3,//"phi3:3.8b" melhor para texto simples
            Prompt = finalPrompt.ToString()
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _ollama.GenerateAsync(genReq, ct))
        {
            if (!string.IsNullOrEmpty(chunk.Response))
                sb.Append(chunk.Response);
            if (chunk.Done) break;
        }

        _timer.Stop();

        Log.Warning(@"Process without file took {Elapsed:hh\:mm\:ss\.fff}", _timer.Elapsed);

        return Ok(new { message = sb.ToString() });
    }

    [HttpPost("prompt-stream-files")]
    [DisableRequestSizeLimit] // remova/ajuste se quiser um limite
    public async Task SendStreamFiles(
           [FromForm] SendPromptDto dto,
           [FromForm] List<IFormFile> files,
           CancellationToken ct)
    {
        _timer.Restart();

        Log.Information("Initializing process");

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Add("X-Accel-Buffering", "no");

        var imagesBase64 = new List<string>();
        var pdfTextSb = new StringBuilder();

        foreach (var file in files ?? Enumerable.Empty<IFormFile>())
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                // monta data URI base64 para o chat multimodal
                var b64 = Convert.ToBase64String(bytes, Base64FormattingOptions.None);
                imagesBase64.Add(b64);
            }
            else if (string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                // extrai texto do PDF
                var text = ExtractTextFromPdf(bytes);

                // Se vier vazio, provavelmente é PDF escaneado -> enta poderia usar : https://github.com/tesseract-ocr/tesseract
                // if (string.IsNullOrWhiteSpace(text)) text = await RunOcrAsync(bytes);

                // limite simples (evita estourar contexto)
                const int maxCharsPerPdf = 12000;
                if (text.Length > maxCharsPerPdf)
                    text = text.Substring(0, maxCharsPerPdf) + "\n...[texto truncado]...";

                pdfTextSb.AppendLine($"[PDF: {file.FileName}]");
                pdfTextSb.AppendLine(text);
                pdfTextSb.AppendLine();
            }
            else
            {
                // outros tipos — você pode tratar .docx/.txt etc. aqui
                pdfTextSb.AppendLine($"[Arquivo não suportado no momento: {file.FileName} ({file.ContentType})]");
            }
        }

        // prompt final: prompt do usuário +  conteúdo extraído dos PDFs
        var finalPrompt = new StringBuilder(dto.Prompt);
        if (pdfTextSb.Length > 0)
        {
            finalPrompt.AppendLine("\n\n# Conteúdo dos PDFs (extraído):\n");
            finalPrompt.AppendLine(pdfTextSb.ToString());
        }

        var images = imagesBase64
            .Select(x => x)
            .ToArray();

        // Se tem imagens ->  modelo VISION via Chat
        if (imagesBase64.Count > 0)
        {
            var chatReq = new ChatRequest
            {
                Model = _opts.ChatModelVision,
                Messages = new List<Message> {
                    new Message {
                        Role    = "user",
                        Content = finalPrompt.ToString(),
                        Images  = images
                    }
                }
            };

            // ChatAsync → suporte  imagens (modelos vision).
            var stBuilder = new StringBuilder();
            await foreach (var chunk in _ollama.ChatAsync(chatReq, ct))
            {
                // em stream de chat o texto vem em chunk.Message.Content
                var piece = chunk?.Message?.Content;
                if (!string.IsNullOrEmpty(piece))
                {
                    stBuilder.Append(piece);
                    await Response.WriteAsync("data: ", ct);
                    await Response.WriteAsync(piece, ct);
                    await Response.WriteAsync("\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }

                if (chunk.Done) break;
            }

            _timer.Stop();

            Log.Warning(@"Process with file took {Elapsed:hh\:mm\:ss\.fff}", _timer.Elapsed);

            var answer = stBuilder.ToString();

        }

        // Sem imagens
        var genReq = new GenerateRequest
        {
            Model = _opts.ChatModelPhi3,//"phi3:3.8b" melhor para texto simples
            Prompt = finalPrompt.ToString()
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _ollama.GenerateAsync(genReq, ct))
        {
            var piece = chunk?.Response;
            if (!string.IsNullOrEmpty(piece))
            {
                sb.Append(piece);
                await Response.WriteAsync("data: ", ct);
                await Response.WriteAsync(piece, ct);
                await Response.WriteAsync("\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            if (chunk.Done) break;
        }

        _timer.Stop();
        //var finalAnswer = sb.ToString();
        Log.Warning(@"Process without file took {Elapsed:hh\:mm\:ss\.fff}", _timer.Elapsed);

    }

    private static string ExtractTextFromPdf(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var pdf = PdfDocument.Open(ms);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }
}
