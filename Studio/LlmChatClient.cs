using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace My3DApp.Studio;

public sealed class LlmChatClient
{
    private readonly HttpClient _httpClient = new();

    public async Task<LlmChatResponse> SendAsync(CadDocument document, string userInput, CancellationToken cancellationToken = default)
    {
        var config = LlmChatConfiguration.FromEnvironment();
        if (!config.IsConfigured)
        {
            return LlmChatResponse.Error(
                "LLM is not configured. Set MY3DAPP_LLM_API_KEY and optionally MY3DAPP_LLM_BASE_URL / MY3DAPP_LLM_MODEL.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, config.ChatCompletionsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var prompt = BuildSystemPrompt(document);
        var payload = new
        {
            model = config.Model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = prompt },
                new { role = "user", content = userInput }
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return LlmChatResponse.Error($"LLM request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        try
        {
            using var json = JsonDocument.Parse(responseText);
            var content = json.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return LlmChatResponse.Error("LLM returned an empty response.");
            }

            var envelope = ParseEnvelope(content);
            return envelope is not null
                ? LlmChatResponse.Success(envelope.Value.reply, envelope.Value.command)
                : LlmChatResponse.Success(content.Trim(), null);
        }
        catch (Exception ex)
        {
            return LlmChatResponse.Error($"Could not parse LLM response: {ex.Message}");
        }
    }

    private static string BuildSystemPrompt(CadDocument document)
    {
        return
            "You are the CAD assistant inside My3DApp. " +
            "The application is solid-first and feature-driven. " +
            "You may answer questions normally, but when a simple action can be expressed as a supported local CAD command, " +
            "return strict JSON with keys reply and command. " +
            "Supported commands are: " +
            "'create box WIDTHxDEPTHxHEIGHT', " +
            "'add cylinder radius R height H', " +
            "'move object DELTA in x|y|z'. " +
            "If no command should be executed, set command to an empty string. " +
            "Do not emit mesh language. Use the current CAD document context below.\n\n" +
            CadDesignTextExporter.Export(document);
    }

    private static (string reply, string? command)? ParseEnvelope(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        using var json = JsonDocument.Parse(trimmed);
        var root = json.RootElement;

        if (!root.TryGetProperty("reply", out var replyElement))
        {
            return null;
        }

        var reply = replyElement.GetString() ?? string.Empty;
        var command = root.TryGetProperty("command", out var commandElement)
            ? commandElement.GetString()
            : null;

        return (reply, string.IsNullOrWhiteSpace(command) ? null : command.Trim());
    }
}

public sealed class LlmChatConfiguration
{
    public string ApiKey { get; private init; } = string.Empty;

    public string BaseUrl { get; private init; } = "https://api.openai.com/v1";

    public string Model { get; private init; } = "gpt-4.1-mini";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public string ChatCompletionsUrl => $"{BaseUrl.TrimEnd('/')}/chat/completions";

    public static LlmChatConfiguration FromEnvironment()
    {
        return new LlmChatConfiguration
        {
            ApiKey = Environment.GetEnvironmentVariable("MY3DAPP_LLM_API_KEY") ?? string.Empty,
            BaseUrl = Environment.GetEnvironmentVariable("MY3DAPP_LLM_BASE_URL") ?? "https://api.openai.com/v1",
            Model = Environment.GetEnvironmentVariable("MY3DAPP_LLM_MODEL") ?? "gpt-4.1-mini"
        };
    }
}

public sealed class LlmChatResponse
{
    public bool IsSuccess { get; private init; }

    public string Reply { get; private init; } = string.Empty;

    public string? Command { get; private init; }

    public static LlmChatResponse Success(string reply, string? command) =>
        new()
        {
            IsSuccess = true,
            Reply = reply,
            Command = command
        };

    public static LlmChatResponse Error(string reply) =>
        new()
        {
            IsSuccess = false,
            Reply = reply
        };
}
