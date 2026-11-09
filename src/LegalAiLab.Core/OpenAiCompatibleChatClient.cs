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
        var payload = new
        {
            model = _options.ChatModel,
            messages = messages.Select(message => new { role = message.Role.Value, content = message.Text ?? "" }).ToList()
        };
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
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
