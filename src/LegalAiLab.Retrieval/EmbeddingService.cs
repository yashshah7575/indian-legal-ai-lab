using System.Net.Http.Json;
using System.Text.Json;

namespace LegalAiLab.Retrieval;

public sealed record EmbeddingResult(float[] Vector, string Provider, bool IsSemantic);

public sealed class EmbeddingService
{
    private readonly HashEmbeddingGenerator _hash = new();
    private readonly HttpClient? _http;
    private readonly string? _baseUrl;
    private readonly string? _model;
    private readonly string? _apiKey;

    public EmbeddingService(HttpClient? http = null, string? baseUrl = null, string? model = null, string? apiKey = null)
    {
        _http = http;
        _baseUrl = baseUrl ?? Environment.GetEnvironmentVariable("LEGALAI_EMBEDDING_BASE_URL");
        _model = model ?? Environment.GetEnvironmentVariable("LEGALAI_EMBEDDING_MODEL") ?? "nomic-embed-text";
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("LEGALAI_API_KEY");
    }

    public async Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl) || _http is null)
        {
            return new EmbeddingResult(_hash.Embed(text), "hash-offline", false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_baseUrl), "v1/embeddings"));
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }

        request.Content = JsonContent.Create(new { model = _model, input = text });
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Embedding provider returned {(int)response.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var values = doc.RootElement.GetProperty("data")[0].GetProperty("embedding").EnumerateArray().Select(item => item.GetSingle()).ToArray();
        return new EmbeddingResult(values, _model!, true);
    }
}
