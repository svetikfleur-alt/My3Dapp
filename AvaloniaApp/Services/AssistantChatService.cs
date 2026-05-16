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
        sb.AppendLine("You are the CAD assistant inside My3DApp Studio — a parametric, solid-body modeler similar to Onshape/Fusion.");
        sb.AppendLine("Rules:");
        sb.AppendLine("  1. Keep every reply to 1-2 sentences. Never use markdown lists or fences in your reply text.");
        sb.AppendLine("  2. When a local command can execute the user's intent, return ONLY a JSON object: {\"reply\": \"...\", \"command\": \"...\"}");
        sb.AppendLine("  3. If no command applies, return the same JSON with command set to \"\".");
        sb.AppendLine("  4. Never invent command syntax not listed below — prefer clarifying questions over bad commands.");
        sb.AppendLine("  5. Prefer a single recipe over a long step sequence when the user describes a standard part.");
        sb.AppendLine($"  6. Current mode: {ctx.Mode}. Respect the active mode when choosing commands.");
        sb.AppendLine();
        sb.AppendLine("## Typical workflows");
        sb.AppendLine("  Extruded boss:  select plane top; start sketch; rectangle 0 0 40 20; finish sketch; extrude 10");
        sb.AppendLine("  Cut hole:       select plane top; start sketch; circle 20 10 radius 4; finish sketch; extrude cut 10");
        sb.AppendLine("  Revolve shaft:  select plane front; start sketch; rectangle 0 0 5 30; finish sketch; revolve 360 around y");
        sb.AppendLine("  Fillet edges:   fillet 2");
        sb.AppendLine("  Shell part:     shell 1.5");
        sb.AppendLine("  Mirror half:    mirror x");
        sb.AppendLine("  Quick box:      create box 40x20x10");
        sb.AppendLine();
        sb.AppendLine("## Command vocabulary");
        sb.AppendLine("  Sequence:    separate steps with ';'. Steps execute in order.");
        sb.AppendLine("  Primitives:  create box WxHxD | add sphere radius R | add cylinder radius R height H | add cone | add torus | add pyramid | add wedge | add prism | add capsule | add hemisphere | add ellipsoid | add arrow | add icosphere");
        sb.AppendLine("  Transform:   move object +N in x|y|z");
        sb.AppendLine("  Camera:      focus camera");
        sb.AppendLine("  Delete:      delete selected");
        sb.AppendLine("  Planes:      select plane top|front|right");
        sb.AppendLine("  Sketch:      start sketch [on top|front|right] | finish sketch | cancel sketch");
        sb.AppendLine("  Draw:        point X Y | line X1 Y1 X2 Y2 | rectangle X Y W H | circle CX CY radius R | arc X1 Y1 X2 Y2 through MX MY");
        sb.AppendLine("  Tools:       line | rectangle | circle | arc | point | polygon | slot | spline | trim | offset | sketch fillet | mirror | transform | angle dimension");
        sb.AppendLine("  Constraints: horizontal | vertical | coincident | equal | fixed | tangent | parallel | perpendicular | concentric  (append 'constraint')");
        sb.AppendLine("  Extrude:     extrude N | extrude reverse N | extrude symmetric N | extrude join N | extrude cut N");
        sb.AppendLine("  Revolve:     revolve DEG around x|y|z");
        sb.AppendLine("  Fillet:      fillet R");
        sb.AppendLine("  Chamfer:     chamfer D");
        sb.AppendLine("  Shell:       shell T");
        sb.AppendLine("  Mirror:      mirror x|y|z");
        sb.AppendLine("  Patterns:    linear pattern COUNT spacing S along x|y|z | circular pattern COUNT angle A around x|y|z");
        sb.AppendLine("  Holes:       hole depth D");
        sb.AppendLine("  Booleans:    boolean union | boolean subtract | boolean intersect");
        sb.AppendLine("  Color:       color body #hexcode");

        sb.AppendLine();
        sb.AppendLine("## Built-in part recipes  (one-line shortcuts that expand to full sketch+feature sequences)");
        foreach (var recipe in CadRecipeLibrary.All)
        {
            sb.Append("  ");
            sb.Append(recipe.Signature.PadRight(60));
            sb.Append("→ ");
            sb.AppendLine(recipe.Description);
        }
        sb.AppendLine("  Use a recipe when the user describes one of these standard parts; pass numeric args in order.");
        sb.AppendLine("  Examples:  \"a 50x30 mounting plate 4mm thick\" → recipe plate 50 30 4");
        sb.AppendLine("             \"hollow case 60 by 40 by 25, walls 2mm\" → recipe shelled-box 60 40 25 2");
        sb.AppendLine("             \"L bracket 40 wide 30 deep 25 tall 4 thick\" → recipe bracket 40 30 25 4");

        sb.AppendLine();
        sb.AppendLine("## Session state");
        sb.AppendLine($"Document: {ctx.DocumentName} | Studio: {ctx.PartStudioName}");
        var modePart = $"Mode: {ctx.AppMode}";
        var planePart = ctx.SelectedPlane is not null ? $" | Plane: {ctx.SelectedPlane}" : string.Empty;
        var toolPart = ctx.ActiveTool is not null ? $" | Tool: {ctx.ActiveTool}" : string.Empty;
        var entityPart = ctx.AppMode == "Sketch" ? $" | Entities: {ctx.SketchEntityCount}" : string.Empty;
        sb.AppendLine($"{modePart}{planePart}{toolPart}{entityPart} | Bodies: {ctx.BodyCount}");
        var sel = ctx.SelectedFeatures.Count > 0 ? string.Join(", ", ctx.SelectedFeatures) : "none";
        sb.AppendLine($"Selected: {sel}");
        if (ctx.RecentActions.Count > 0)
        {
            sb.Append("Recent: ");
            sb.AppendLine(string.Join(" → ", ctx.RecentActions.TakeLast(4)));
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
    int BodyCount,
    string DocumentName = "Untitled Document",
    string PartStudioName = "Part Studio 1");

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
