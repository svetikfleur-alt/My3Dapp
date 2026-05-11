using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace My3DApp.AvaloniaApp.Services;

public sealed class AssistantChatService
{
    private const string AnthropicApiVersion = "2023-06-01";
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public AssistantRuntimeConfiguration ReadConfiguration(string provider, string selectedModel, string? keyOverride = null) =>
        AssistantRuntimeConfiguration.FromSelection(provider, selectedModel, keyOverride);

    public async Task<AssistantChatResult> SendAsync(
        AssistantRuntimeConfiguration configuration,
        IReadOnlyList<AssistantPromptMessage> conversation,
        CadAssistantContext context,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.IsConfigured)
        {
            return AssistantChatResult.Error(
                configuration.ValidationError ??
                "Assistant is not configured. Add a provider key to enable remote AI.");
        }

        if (!Uri.TryCreate(configuration.RequestUrl, UriKind.Absolute, out var requestUri))
        {
            return AssistantChatResult.Error(
                $"Assistant configuration is invalid. Endpoint is not a valid absolute URL: {configuration.RequestUrl}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = configuration.Provider switch
        {
            AssistantProvider.Anthropic => BuildAnthropicRequest(request, configuration, conversation, context),
            _ => BuildOpenAiCompatibleRequest(request, configuration, conversation, context)
        };

        HttpResponseMessage response;
        try
        {
            response = await SharedHttpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("Assistant", "Assistant request failed before response.", ex);
            return AssistantChatResult.Error($"Assistant request failed before response: {ex.Message}");
        }

        using (response)
        {
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            RuntimeLog.Write(
                "Assistant",
                $"Assistant request failed: {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{responseText}");
            return AssistantChatResult.Error(
                $"Assistant request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        try
        {
            using var json = JsonDocument.Parse(responseText);
            var content = configuration.Provider switch
            {
                AssistantProvider.Anthropic => ExtractAnthropicContent(json.RootElement),
                _ => ExtractOpenAiCompatibleContent(json.RootElement)
            };
            if (string.IsNullOrWhiteSpace(content))
            {
                return AssistantChatResult.Error("Assistant returned an empty response.");
            }

            var envelope = ParseEnvelope(content);
            return envelope is not null
                ? AssistantChatResult.Success(envelope.Value.reply, envelope.Value.command)
                : AssistantChatResult.Success(content.Trim(), null);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("Assistant", "Could not parse assistant response.", ex);
            return AssistantChatResult.Error($"Could not parse assistant response: {ex.Message}");
        }
        }
    }

    private static string BuildSystemPrompt(CadAssistantContext ctx)
    {
        var sb = new StringBuilder();
        sb.Append("You are the assistant inside My3DApp CAD Studio. ");
        sb.Append("Keep answers practical and concise. ");
        sb.Append("This is a solid-first, feature-driven CAD workflow (not mesh-first). ");
        sb.Append($"Current assistant mode: {ctx.Mode}. ");
        sb.AppendLine("When a direct local action is possible, return ONLY a JSON object with keys \"reply\" (string) and \"command\" (string). ");
        sb.AppendLine("Do not use markdown fences around that JSON. ");
        sb.AppendLine("If no command should run, set command to \"\". ");
        sb.AppendLine("Full command vocabulary:");
        sb.AppendLine("  Sequences:   separate steps with ';' or 'then'. Example: select plane top; start sketch; rectangle 0 0 20 10; finish sketch; extrude 8");
        sb.AppendLine("  Primitives:  create box 20x10x5 | add sphere radius 5 | add cylinder radius 5 height 20 | add cone | add torus | add pyramid | add wedge | add prism | add capsule | add hemisphere | add ellipsoid | add arrow | add icosphere");
        sb.AppendLine("  Transform:   move object +10 in x  (axis: x|y|z)");
        sb.AppendLine("  Selection:   delete selected | focus camera");
        sb.AppendLine("  Planes:      select plane top|front|right");
        sb.AppendLine("  Sketch flow: select plane top|front|right | start sketch | start sketch on top|front|right | finish sketch | cancel sketch");
        sb.AppendLine("  Sketch coordinates: point 0 0 | line 0 0 20 0 | rectangle 0 0 20 10 | circle 0 0 radius 5 | arc 0 0 10 0 through 5 4");
        sb.AppendLine("  Sketch tools: line | rectangle | circle | arc | point | polygon | slot | spline | trim | offset | sketch fillet | mirror | transform | angle dimension");
        sb.AppendLine("  Constraints: horizontal constraint | vertical constraint | coincident constraint | equal constraint | fixed constraint | tangent constraint | parallel constraint | perpendicular constraint | concentric constraint");
        sb.AppendLine("  Features:    extrude 6 | extrude reverse 6 | extrude symmetric 10 | extrude join 6 | extrude cut 6 | revolve 270 around y | fillet 2 | chamfer 1 | shell 1.5 | mirror x|y|z");
        sb.AppendLine("  Patterns:    linear pattern 3 spacing 10 along x | circular pattern 6 angle 360 around y");
        sb.AppendLine("  Holes:       hole depth 10");
        sb.AppendLine("  Booleans:    boolean union | boolean subtract | boolean intersect");

        sb.AppendLine();
        sb.AppendLine("## Session context");
        var modePart = $"App mode: {ctx.AppMode}";
        var planePart = ctx.SelectedPlane is not null ? $" | Plane: {ctx.SelectedPlane}" : string.Empty;
        var toolPart = ctx.ActiveTool is not null ? $" | Active tool: {ctx.ActiveTool}" : string.Empty;
        var entityPart = ctx.AppMode == "Sketch" ? $" | Sketch entities: {ctx.SketchEntityCount}" : string.Empty;
        sb.AppendLine($"{modePart}{planePart}{toolPart}{entityPart} | Bodies: {ctx.BodyCount}");
        var sel = ctx.SelectedFeatures.Count > 0 ? string.Join(", ", ctx.SelectedFeatures) : "(none)";
        sb.AppendLine($"Selected: {sel}");
        if (ctx.RecentActions.Count > 0)
        {
            sb.AppendLine("Recent actions:");
            foreach (var entry in ctx.RecentActions)
            {
                sb.AppendLine($"  {entry}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static HttpContent BuildOpenAiCompatibleRequest(
        HttpRequestMessage request,
        AssistantRuntimeConfiguration configuration,
        IReadOnlyList<AssistantPromptMessage> conversation,
        CadAssistantContext context)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiKey);

        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = BuildSystemPrompt(context)
            }
        };

        foreach (var message in conversation.TakeLast(16))
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            messages.Add(new
            {
                role = message.Role,
                content = message.Content
            });
        }

        var payload = new
        {
            model = configuration.Model,
            temperature = 0.2,
            messages = messages.ToArray()
        };

        return new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
    }

    private static HttpContent BuildAnthropicRequest(
        HttpRequestMessage request,
        AssistantRuntimeConfiguration configuration,
        IReadOnlyList<AssistantPromptMessage> conversation,
        CadAssistantContext context)
    {
        request.Headers.Add("x-api-key", configuration.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicApiVersion);

        var messages = new List<object>();

        foreach (var message in conversation.TakeLast(16))
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            var role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? "assistant"
                : "user";

            messages.Add(new
            {
                role,
                content = message.Content
            });
        }

        if (messages.Count == 0)
        {
            messages.Add(new
            {
                role = "user",
                content = "Hello."
            });
        }

        var payload = new
        {
            model = configuration.Model,
            max_tokens = 1200,
            system = BuildSystemPrompt(context),
            messages = messages.ToArray()
        };

        return new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
    }

    private static string ExtractOpenAiCompatibleContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message))
        {
            return string.Empty;
        }

        if (!message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value);
                }

                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }
        }

        return string.Join("\n", parts);
    }

    private static string ExtractAnthropicContent(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (item.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String &&
                !string.Equals(typeElement.GetString(), "text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }
        }

        return string.Join("\n", parts);
    }

    private static (string reply, string? command)? ParseEnvelope(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var fenceStart = trimmed.IndexOf('{');
            var fenceEnd = trimmed.LastIndexOf('}');
            if (fenceStart >= 0 && fenceEnd > fenceStart)
            {
                trimmed = trimmed[fenceStart..(fenceEnd + 1)];
            }
        }

        if (!trimmed.StartsWith("{", StringComparison.Ordinal) ||
            !trimmed.EndsWith("}", StringComparison.Ordinal))
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
        var command = root.TryGetProperty("command", out var commandElement) &&
                      commandElement.ValueKind == JsonValueKind.String
            ? commandElement.GetString()
            : null;

        return (reply, string.IsNullOrWhiteSpace(command) ? null : command.Trim());
    }
}

public sealed record AssistantPromptMessage(string Role, string Content);

public sealed class AssistantRuntimeConfiguration
{
    public AssistantProvider Provider { get; private init; } = AssistantProvider.OpenAI;

    public string ApiKey { get; private init; } = string.Empty;

    public string BaseUrl { get; private init; } = "https://api.openai.com/v1";

    public string Model { get; private init; } = "gpt-5-mini";

    public string? ValidationError { get; private init; }

    public bool IsConfigured => string.IsNullOrWhiteSpace(ValidationError) && !string.IsNullOrWhiteSpace(ApiKey);

    public string RequestUrl =>
        Provider == AssistantProvider.Anthropic
            ? $"{BaseUrl.TrimEnd('/')}/messages"
            : $"{BaseUrl.TrimEnd('/')}/chat/completions";

    public string MissingKeyHint =>
        Provider == AssistantProvider.Anthropic
            ? "Set ANTHROPIC_API_KEY to enable remote AI."
            : "Set OPENAI_API_KEY or MY3DAPP_LLM_API_KEY to enable remote AI.";

    public static AssistantRuntimeConfiguration FromSelection(string provider, string selectedModel, string? keyOverride = null)
    {
        var normalizedProvider = string.Equals(provider?.Trim(), "Anthropic", StringComparison.OrdinalIgnoreCase)
            ? AssistantProvider.Anthropic
            : AssistantProvider.OpenAI;

        var trimmedOverride = (keyOverride ?? string.Empty).Trim();
        var apiKey = !string.IsNullOrWhiteSpace(trimmedOverride)
            ? trimmedOverride
            : normalizedProvider == AssistantProvider.Anthropic
                ? ReadEnvironmentValue("ANTHROPIC_API_KEY", "MY3DAPP_LLM_API_KEY")
                : ReadEnvironmentValue("OPENAI_API_KEY", "MY3DAPP_LLM_API_KEY");

        var defaultBaseUrl = normalizedProvider == AssistantProvider.Anthropic
            ? "https://api.anthropic.com/v1"
            : "https://api.openai.com/v1";
        var baseUrl = ReadEnvironmentValue("MY3DAPP_LLM_BASE_URL");
        var model = ReadEnvironmentValue("MY3DAPP_LLM_MODEL");

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = defaultBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            model = MapDisplayModelToApiModel(normalizedProvider, selectedModel);
        }

        string? validationError = null;
        if (!string.IsNullOrWhiteSpace(baseUrl) && !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            validationError = $"Assistant configuration error: MY3DAPP_LLM_BASE_URL is not a valid absolute URL ({baseUrl}).";
        }
        else if (string.IsNullOrWhiteSpace(apiKey))
        {
            validationError = normalizedProvider == AssistantProvider.Anthropic
                ? "Anthropic key not found."
                : "OpenAI key not found.";
        }

        return new AssistantRuntimeConfiguration
        {
            Provider = normalizedProvider,
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            Model = model,
            ValidationError = validationError
        };
    }

    private static string ReadEnvironmentValue(params string[] names)
    {
        foreach (var name in names)
        {
            var processValue = (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(processValue))
            {
                return processValue;
            }

            var userValue = (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(userValue))
            {
                return userValue;
            }

            var machineValue = (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(machineValue))
            {
                return machineValue;
            }
        }

        return string.Empty;
    }

    private static string MapDisplayModelToApiModel(AssistantProvider provider, string selectedModel)
    {
        var normalized = (selectedModel ?? string.Empty).Trim().ToLowerInvariant();

        if (provider == AssistantProvider.Anthropic)
        {
            return "claude-sonnet-4-6";
        }

        return normalized switch
        {
            "gpt-5" => "gpt-5",
            "gpt-5 mini" => "gpt-5-mini",
            "gpt-5.4" => "gpt-5",
            "gpt-5.4 mini" => "gpt-5-mini",
            _ => "gpt-5-mini"
        };
    }
}

public enum AssistantProvider
{
    OpenAI,
    Anthropic
}

public sealed record CadAssistantContext(
    string Mode,
    string AppMode,
    string? ActiveTool,
    string? SelectedPlane,
    IReadOnlyList<string> SelectedFeatures,
    IReadOnlyList<string> RecentActions,
    int SketchEntityCount,
    int BodyCount);

public sealed class AssistantChatResult
{
    public bool IsSuccess { get; private init; }

    public string Reply { get; private init; } = string.Empty;

    public string? Command { get; private init; }

    public static AssistantChatResult Success(string reply, string? command)
    {
        return new AssistantChatResult
        {
            IsSuccess = true,
            Reply = reply,
            Command = command
        };
    }

    public static AssistantChatResult Error(string reply)
    {
        return new AssistantChatResult
        {
            IsSuccess = false,
            Reply = reply
        };
    }
}
