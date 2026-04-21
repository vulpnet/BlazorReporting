using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorReporting.Services;

public sealed record ChatMessage(string Role, string Content);

public sealed record OllamaModel(string Name, string ModifiedAt, long Size);

public sealed class ChatbotService(HttpClient http, IConfiguration config)
{
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private string BaseUrl =>
        config["Ollama:BaseUrl"] ?? "http://localhost:11434";

    // ── List available models ─────────────────────────────────
    public async Task<IReadOnlyList<OllamaModel>> GetModelsAsync(
        CancellationToken ct = default)
    {
        try
        {
            var resp = await http.GetAsync($"{BaseUrl}/api/tags", ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var models = new List<OllamaModel>();

            foreach (var m in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                var name  = m.GetProperty("name").GetString() ?? "";
                var modAt = m.TryGetProperty("modified_at", out var ma) ? ma.GetString() ?? "" : "";
                var size  = m.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0L;
                models.Add(new OllamaModel(name, modAt, size));
            }
            return models;
        }
        catch
        {
            return [];
        }
    }

    // ── Send a chat turn ─────────────────────────────────────
    public async Task<string> SendAsync(
        string model,
        IReadOnlyList<ChatMessage> history,
        string userMessage,
        string systemPrompt,
        CancellationToken ct = default)
    {
        // Build messages: system + history + new user turn
        var messages = new List<object>();
        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(new { role = "system", content = systemPrompt });
        foreach (var m in history)
            messages.Add(new { role = m.Role, content = m.Content });
        messages.Add(new { role = "user", content = userMessage });

        var body = new
        {
            model,
            messages,
            stream = false
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        using var resp = await http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama error {(int)resp.StatusCode}: {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
