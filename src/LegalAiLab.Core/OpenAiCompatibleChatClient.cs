using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace LegalAiLab.Core;

public sealed class ModelProviderOptions
{
    public string? ChatBaseUrl { get; init; }
    public string? ChatModel { get; init; }
    public string? ApiKey { get; init; }
    public string? EmbeddingBaseUrl { get; init; }
    public string? EmbeddingModel { get; init; }

    public static ModelProviderOptions FromEnvironment() => new()
    {
        ChatBaseUrl = Environment.GetEnvironmentVariable("LEGALAI_CHAT_BASE_URL"),
        ChatModel = Environment.GetEnvironmentVariable("LEGALAI_CHAT_MODEL") ?? "llama3.2",
        ApiKey = Environment.GetEnvironmentVariable("LEGALAI_API_KEY"),
        EmbeddingBaseUrl = Environment.GetEnvironmentVariable("LEGALAI_EMBEDDING_BASE_URL"),
        EmbeddingModel = Environment.GetEnvironmentVariable("LEGALAI_EMBEDDING_MODEL") ?? "nomic-embed-text"
    };

    public bool HasChat => !string.IsNullOrWhiteSpace(ChatBaseUrl);
}

/// <summary>
/// Calls an OpenAI-compatible /v1/chat/completions endpoint. Ollama can serve this API.
/// </summary>
public sealed class OpenAiCompatibleChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly ModelProviderOptions _options;

    public OpenAiCompatibleChatClient(HttpClient http, ModelProviderOptions options)
    {
        _http = http;
        _options = options;
        if (string.IsNullOrWhiteSpace(options.ChatBaseUrl))
        {
            throw new InvalidOperationException("LEGALAI_CHAT_BASE_URL is required for the live chat client.");
        }
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.ChatModel,
            ["messages"] = messages.Select(message => new { role = message.Role.Value, content = message.Text ?? "" }).ToList()
        };
        if (options?.Tools is { Count: > 0 })
        {
            payload["tools"] = options.Tools.OfType<AIFunction>().Select(tool => new
            {
                type = "function",
                function = new { name = tool.Name, description = tool.Description, parameters = new { type = "object", properties = new { query = new { type = "string" } } } }
            }).ToList();
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.ChatBaseUrl!), "v1/chat/completions"));
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        request.Content = JsonContent.Create(payload);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Chat provider returned {(int)response.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        var text = message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String ? content.GetString() ?? "" : "";
        var chat = new ChatMessage(ChatRole.Assistant, text);
        if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                var function = call.GetProperty("function");
                var arguments = new Dictionary<string, object?>();
                var raw = function.GetProperty("arguments").GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    using var parsed = JsonDocument.Parse(raw);
                    foreach (var property in parsed.RootElement.EnumerateObject())
                    {
                        arguments[property.Name] = property.Value.ToString();
                    }
                }

                chat.Contents.Add(new FunctionCallContent(call.GetProperty("id").GetString() ?? "call", function.GetProperty("name").GetString() ?? "", arguments));
            }
        }

        return new ChatResponse(chat);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.ChatModel,
            ["stream"] = true,
            ["messages"] = messages.Select(message => new { role = message.Role.Value, content = message.Text ?? "" }).ToList()
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_options.ChatBaseUrl!), "v1/chat/completions"));
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        request.Content = JsonContent.Create(payload);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]")
            {
                yield break;
            }

            using var doc = JsonDocument.Parse(data);
            var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
            if (delta.TryGetProperty("content", out var piece) && piece.ValueKind == JsonValueKind.String)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, piece.GetString());
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
