using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace My3DApp.AvaloniaApp.Services;

public sealed class AssistantChatService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    private readonly IReadOnlyDictionary<AssistantProvider, IAssistantChatProvider> _providers;

    public AssistantChatService()
    {
        _providers = new Dictionary<AssistantProvider, IAssistantChatProvider>
        {
            [AssistantProvider.DeepSeek] = new OpenAiCompatibleAssistantProvider(AssistantProvider.DeepSeek, SharedHttpClient),
            [AssistantProvider.OpenAI] = new OpenAiCompatibleAssistantProvider(AssistantProvider.OpenAI, SharedHttpClient)
        };
    }

    public AssistantRuntimeConfiguration ReadConfiguration(
        string provider,
        string selectedModel,
        string? baseUrlOverride = null,
        string? keyOverride = null) =>
        AssistantRuntimeConfiguration.FromSelection(provider, selectedModel, baseUrlOverride, keyOverride);

    public async Task<AssistantChatResult> SendAsync(
        AssistantRuntimeConfiguration configuration,
        IReadOnlyList<AssistantPromptMessage> conversation,
        CadAssistantContext context,
        CancellationToken cancellationToken = default)
    {
        if (configuration.IsLocalOnly)
        {
            return AssistantChatResult.Error("Local CAD only mode does not send remote AI requests.");
        }

        if (!configuration.IsConfigured)
        {
            return AssistantChatResult.Error(
                configuration.ValidationError ??
                $"AI provider not configured. {configuration.MissingKeyHint}");
        }

        if (!_providers.TryGetValue(configuration.Provider, out var provider))
        {
            return AssistantChatResult.Error($"No AI provider client is registered for {configuration.ProviderDisplayName}.");
        }

        var providerResponse = await provider.SendAsync(configuration, conversation, context, cancellationToken);
        if (!providerResponse.IsSuccess)
        {
            return AssistantChatResult.Error(providerResponse.ErrorMessage);
        }

        var candidate = AssistantCandidateParser.Parse(providerResponse.Content, out var parseError);
        if (candidate is null)
        {
            return AssistantChatResult.Error(
                $"AI response was not valid ACL or CAD Recipe JSON: {parseError ?? "unknown parse error"}");
        }

        var validatedCandidate = AssistantCandidateParser.Validate(candidate, out var validationError);
        if (validatedCandidate is null)
        {
            return AssistantChatResult.Error(
                $"AI response was not safe to hand off: {validationError ?? "unknown validation error"}");
        }

        return AssistantChatResult.Success(validatedCandidate);
    }

    public static string GetDefaultModelName(string provider)
    {
        return AssistantRuntimeConfiguration.NormalizeProvider(provider) switch
        {
            AssistantProvider.LocalCadOnly => "local-cad",
            AssistantProvider.OpenAI => "gpt-4o-mini",
            _ => "deepseek-chat"
        };
    }

    public static string GetDefaultBaseUrl(string provider)
    {
        return AssistantRuntimeConfiguration.NormalizeProvider(provider) switch
        {
            AssistantProvider.OpenAI => "https://api.openai.com/v1",
            AssistantProvider.DeepSeek => "https://api.deepseek.com/v1",
            _ => string.Empty
        };
    }
}

public interface IAssistantChatProvider
{
    AssistantProvider Provider { get; }

    Task<AssistantProviderResponse> SendAsync(
        AssistantRuntimeConfiguration configuration,
        IReadOnlyList<AssistantPromptMessage> conversation,
        CadAssistantContext context,
        CancellationToken cancellationToken);
}

public sealed class OpenAiCompatibleAssistantProvider : IAssistantChatProvider
{
    private readonly HttpClient _httpClient;

    public OpenAiCompatibleAssistantProvider(AssistantProvider provider, HttpClient httpClient)
    {
        Provider = provider;
        _httpClient = httpClient;
    }

    public AssistantProvider Provider { get; }

    public async Task<AssistantProviderResponse> SendAsync(
        AssistantRuntimeConfiguration configuration,
        IReadOnlyList<AssistantPromptMessage> conversation,
        CadAssistantContext context,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(configuration.RequestUrl, UriKind.Absolute, out var requestUri))
        {
            return AssistantProviderResponse.Error(
                $"{configuration.ProviderDisplayName} configuration is invalid. Endpoint is not a valid absolute URL: {configuration.RequestUrl}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
        request.Content = BuildRequestContent(configuration, conversation, context);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("Assistant", $"{configuration.ProviderDisplayName} request failed before response.", ex);
            return AssistantProviderResponse.Error(
                $"{configuration.ProviderDisplayName} request failed before response: {ex.Message}");
        }

        using (response)
        {
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                RuntimeLog.Write(
                    "Assistant",
                    $"{configuration.ProviderDisplayName} request failed: {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{TrimForLog(responseText)}");
                return AssistantProviderResponse.Error(
                    $"{configuration.ProviderDisplayName} request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {ExtractProviderError(responseText)}".Trim());
            }

            try
            {
                using var json = JsonDocument.Parse(responseText);
                var content = ExtractOpenAiCompatibleContent(json.RootElement);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return AssistantProviderResponse.Error($"{configuration.ProviderDisplayName} returned an empty response.");
                }

                return AssistantProviderResponse.Success(content);
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("Assistant", $"Could not parse {configuration.ProviderDisplayName} response.", ex);
                return AssistantProviderResponse.Error(
                    $"Could not parse {configuration.ProviderDisplayName} response: {ex.Message}");
            }
        }
    }

    private static HttpContent BuildRequestContent(
        AssistantRuntimeConfiguration configuration,
        IReadOnlyList<AssistantPromptMessage> conversation,
        CadAssistantContext context)
    {
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
                role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? "assistant"
                    : "user",
                content = message.Content
            });
        }

        var payload = new
        {
            model = configuration.Model,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = messages.ToArray()
        };

        return new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
    }

    private static string BuildSystemPrompt(CadAssistantContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the provider-backed CAD assistant inside My3DApp Studio.");
        sb.AppendLine("Return exactly one valid JSON object and no prose, markdown, or code fences.");
        sb.AppendLine("The JSON must be one of these two shapes:");
        sb.AppendLine("ACL candidate: {\"kind\":\"acl\",\"title\":\"short name\",\"description\":\"what it builds\",\"acl\":\"supported ACL/M3 Script commands\"}");
        sb.AppendLine("Recipe candidate: {\"kind\":\"recipe\",\"title\":\"short name\",\"description\":\"what it builds\",\"recipe\":{\"units\":\"mm\",\"steps\":[\"supported command 1\",\"supported command 2\"]}}");
        sb.AppendLine("Use ACL for compact scripts and recipe JSON for multi-step CAD plans. If the request is unclear, return recipe JSON with an empty steps array and put one concise clarification question in description.");
        sb.AppendLine("Do not invent commands. Every ACL or recipe step must use the supported vocabulary below.");
        sb.AppendLine($"Current assistant mode: {ctx.Mode}. Remote output is always reviewed by the app before execution.");
        sb.AppendLine();
        sb.AppendLine("Supported command vocabulary:");
        sb.AppendLine("Sequence: separate steps with semicolons or recipe.steps array items.");
        sb.AppendLine("Primitives: create box WxHxD | add sphere radius R | add cylinder radius R height H | add cone | add torus | add pyramid | add wedge | add prism | add capsule | add hemisphere | add ellipsoid | add arrow | add icosphere | add tetrahedron | add octahedron | add icosahedron");
        sb.AppendLine("Transform: move object +N in x|y|z");
        sb.AppendLine("Camera/Delete: focus camera | delete selected");
        sb.AppendLine("Planes: select plane top|front|right");
        sb.AppendLine("Sketch: start sketch [on top|front|right] | finish sketch | cancel sketch");
        sb.AppendLine("Draw: point X Y | line X1 Y1 X2 Y2 | rectangle X Y W H | circle CX CY radius R | arc X1 Y1 X2 Y2 through MX MY");
        sb.AppendLine("Tools: line | rectangle | circle | arc | point | polygon | slot | spline | trim | offset | sketch fillet | mirror | transform | angle dimension");
        sb.AppendLine("Constraints: horizontal | vertical | coincident | equal | fixed | tangent | parallel | perpendicular | concentric");
        sb.AppendLine("Features: extrude N | extrude reverse N | extrude symmetric N | extrude join N | extrude cut N | revolve DEG around x|y | fillet R | chamfer D | shell T | mirror x|y|z | hole depth D | boolean union | boolean subtract | boolean intersect");
        sb.AppendLine("Patterns: linear pattern COUNT spacing S along x|y|z | circular pattern COUNT angle A around x|y|z");
        sb.AppendLine();
        sb.AppendLine("Built-in CAD recipe shortcuts:");
        foreach (var recipe in CadRecipeLibrary.All)
        {
            sb.Append("  ");
            sb.Append(recipe.Signature.PadRight(60));
            sb.Append(" - ");
            sb.AppendLine(recipe.Description);
        }
        sb.AppendLine("Use a recipe shortcut when it directly matches the user's requested part.");
        sb.AppendLine("Examples: recipe plate 50 30 4; recipe shelled-box 60 40 25 2; recipe bracket 40 30 25 4");
        sb.AppendLine();
        sb.AppendLine("Session state:");
        sb.AppendLine($"Document: {ctx.DocumentName} | Studio: {ctx.PartStudioName}");
        var planePart = ctx.SelectedPlane is not null ? $" | Plane: {ctx.SelectedPlane}" : string.Empty;
        var toolPart = ctx.ActiveTool is not null ? $" | Tool: {ctx.ActiveTool}" : string.Empty;
        var entityPart = ctx.AppMode == "Sketch" ? $" | Sketch entities: {ctx.SketchEntityCount}" : string.Empty;
        sb.AppendLine($"Mode: {ctx.AppMode}{planePart}{toolPart}{entityPart} | Bodies: {ctx.BodyCount}");
        var selection = ctx.SelectedFeatures.Count > 0 ? string.Join(", ", ctx.SelectedFeatures) : "none";
        sb.AppendLine($"Selected: {selection}");
        if (ctx.RecentActions.Count > 0)
        {
            sb.Append("Recent: ");
            sb.AppendLine(string.Join(" -> ", ctx.RecentActions.TakeLast(4)));
        }

        return sb.ToString().TrimEnd();
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
        if (!firstChoice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
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

    private static string ExtractProviderError(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        try
        {
            using var json = JsonDocument.Parse(responseText);
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return TrimForDisplay(error.GetString() ?? string.Empty);
                }

                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String)
                {
                    return TrimForDisplay(message.GetString() ?? string.Empty);
                }
            }
        }
        catch (JsonException)
        {
            // Fall back to a compact body excerpt below.
        }

        return TrimForDisplay(responseText);
    }

    private static string TrimForDisplay(string text)
    {
        var normalized = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 320 ? normalized : normalized[..320] + "...";
    }

    private static string TrimForLog(string text)
    {
        var normalized = text.Trim();
        return normalized.Length <= 1200 ? normalized : normalized[..1200] + "...";
    }
}

public sealed class AssistantCandidateParser
{
    private static readonly JsonSerializerOptions CandidateJsonOptions = new() { WriteIndented = true };
    private static readonly CadCommandParser CommandParser = new();

    private AssistantCandidateParser()
    {
    }

    public static AssistantCandidate? Parse(string content, out string? error)
    {
        error = null;
        var jsonText = ExtractJsonObject(content);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            error = "No JSON object was found in the provider response.";
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(jsonText);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The provider response root must be a JSON object.";
                return null;
            }

            var kind = ResolveKind(root);
            if (kind is null)
            {
                error = "Expected kind/type to be 'acl' or 'recipe'.";
                return null;
            }

            var sourceText = kind == AssistantCandidateKind.Acl
                ? ReadAclText(root)
                : ReadRecipeText(root, out _) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                error = kind == AssistantCandidateKind.Acl
                    ? "ACL candidate did not include an acl/script string."
                    : "Recipe candidate did not include executable recipe.steps.";
                return null;
            }

            _ = ReadRecipeText(root, out var recipeUnits);
            var units = !string.IsNullOrWhiteSpace(recipeUnits)
                ? recipeUnits
                : GetString(root, "units") ?? "mm";

            var title = GetString(root, "title", "name") ??
                        (kind == AssistantCandidateKind.Acl ? "AI ACL Candidate" : "AI Recipe Candidate");
            var description = GetString(root, "description", "summary") ?? "Provider generated CAD candidate.";
            var sourceJson = JsonSerializer.Serialize(root, CandidateJsonOptions);
            return new AssistantCandidate(
                kind.Value,
                title.Trim(),
                description.Trim(),
                string.IsNullOrWhiteSpace(units) ? "mm" : units.Trim(),
                sourceText.Trim(),
                string.Empty,
                sourceJson);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static AssistantCandidate? Validate(AssistantCandidate candidate, out string? error)
    {
        error = null;
        if (!CadScriptLibrary.TryExpandSequence(candidate.SourceText, out var expanded, out var expandError))
        {
            error = $"ACL expansion failed: {expandError}";
            return null;
        }

        if (string.IsNullOrWhiteSpace(expanded))
        {
            error = "Candidate produced no executable CAD steps.";
            return null;
        }

        var steps = CommandParser.ParseSequence(expanded);
        var failedStep = steps.FirstOrDefault(step => !step.Result.IsSuccess || step.Result.Commands.Count == 0);
        if (failedStep is not null)
        {
            error = $"Unsupported step {failedStep.Index}: {failedStep.Text}. {failedStep.Result.Message}";
            return null;
        }

        return candidate with { CommandText = expanded.Trim() };
    }

    private static AssistantCandidateKind? ResolveKind(JsonElement root)
    {
        var raw = GetString(root, "kind", "type", "format")?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (raw.Contains("acl", StringComparison.Ordinal))
            {
                return AssistantCandidateKind.Acl;
            }

            if (raw.Contains("recipe", StringComparison.Ordinal))
            {
                return AssistantCandidateKind.Recipe;
            }
        }

        if (TryGetProperty(root, out _, "acl", "script"))
        {
            return AssistantCandidateKind.Acl;
        }

        if (TryGetProperty(root, out _, "recipe", "steps", "commands"))
        {
            return AssistantCandidateKind.Recipe;
        }

        return null;
    }

    private static string ReadAclText(JsonElement root)
    {
        var text = GetString(root, "acl", "script", "m3Script", "command");
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (TryGetProperty(root, out var commands, "commands"))
        {
            return ReadStepList(commands);
        }

        return string.Empty;
    }

    private static string? ReadRecipeText(JsonElement root, out string? units)
    {
        units = GetString(root, "units");

        if (TryGetProperty(root, out var recipe, "recipe"))
        {
            if (recipe.ValueKind == JsonValueKind.String)
            {
                return recipe.GetString() ?? string.Empty;
            }

            if (recipe.ValueKind == JsonValueKind.Object)
            {
                units = GetString(recipe, "units") ?? units;

                var invocation = ReadRecipeInvocation(recipe);
                if (!string.IsNullOrWhiteSpace(invocation))
                {
                    return invocation;
                }

                var text = GetString(recipe, "acl", "script", "m3Script", "command");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (TryGetProperty(recipe, out var recipeSteps, "steps", "commands"))
                {
                    return ReadStepList(recipeSteps);
                }
            }

            if (recipe.ValueKind == JsonValueKind.Array)
            {
                return ReadStepList(recipe);
            }
        }

        var rootText = GetString(root, "acl", "script", "m3Script", "command");
        if (!string.IsNullOrWhiteSpace(rootText))
        {
            return rootText;
        }

        if (TryGetProperty(root, out var steps, "steps", "commands"))
        {
            return ReadStepList(steps);
        }

        return string.Empty;
    }

    private static string ReadRecipeInvocation(JsonElement recipe)
    {
        var name = GetString(recipe, "name", "id", "recipeName");
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var args = new List<string>();
        if (TryGetProperty(recipe, out var arguments, "arguments", "args", "parameters") &&
            arguments.ValueKind == JsonValueKind.Array)
        {
            foreach (var argument in arguments.EnumerateArray())
            {
                switch (argument.ValueKind)
                {
                    case JsonValueKind.Number:
                        args.Add(argument.GetRawText());
                        break;
                    case JsonValueKind.String:
                        var value = argument.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            args.Add(value.Trim());
                        }
                        break;
                }
            }
        }

        return $"recipe {name.Trim()} {string.Join(' ', args)}".TrimEnd();
    }

    private static string ReadStepList(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var steps = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            var text = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object => GetString(item, "acl", "script", "m3Script", "command", "text", "action"),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                steps.Add(text.Trim());
            }
        }

        return string.Join("; ", steps);
    }

    private static string ExtractJsonObject(string content)
    {
        var trimmed = (content ?? string.Empty).Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var fenceStart = trimmed.IndexOf('{');
            var fenceEnd = trimmed.LastIndexOf('}');
            return fenceStart >= 0 && fenceEnd > fenceStart
                ? trimmed[fenceStart..(fenceEnd + 1)]
                : string.Empty;
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : string.Empty;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, out var property, name) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] names)
    {
        foreach (var item in element.EnumerateObject())
        {
            if (names.Any(name => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                property = item.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}

public sealed record AssistantPromptMessage(string Role, string Content);

public sealed record AssistantProviderResponse(bool IsSuccess, string Content, string ErrorMessage)
{
    public static AssistantProviderResponse Success(string content) => new(true, content, string.Empty);

    public static AssistantProviderResponse Error(string message) => new(false, string.Empty, message);
}

public sealed record AssistantCandidate(
    AssistantCandidateKind Kind,
    string Title,
    string Description,
    string Units,
    string SourceText,
    string CommandText,
    string SourceJson)
{
    public string KindLabel => Kind == AssistantCandidateKind.Acl ? "ACL" : "CAD Recipe JSON";

    public string ToDisplayText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{KindLabel} candidate: {Title}");
        sb.AppendLine(Description);
        sb.AppendLine("Validated by My3DApp. Review the generated recipe steps before running.");
        sb.AppendLine();
        sb.AppendLine("Model JSON:");
        sb.AppendLine(SourceJson);
        return sb.ToString().TrimEnd();
    }
}

public enum AssistantCandidateKind
{
    Acl,
    Recipe
}

public sealed class AssistantRuntimeConfiguration
{
    public AssistantProvider Provider { get; private init; } = AssistantProvider.DeepSeek;

    public string ApiKey { get; private init; } = string.Empty;

    public string ApiKeySource { get; private init; } = string.Empty;

    public string BaseUrl { get; private init; } = "https://api.deepseek.com/v1";

    public string BaseUrlSource { get; private init; } = "default";

    public string Model { get; private init; } = "deepseek-chat";

    public string? ValidationError { get; private init; }

    public bool IsLocalOnly => Provider == AssistantProvider.LocalCadOnly;

    public bool IsConfigured => !IsLocalOnly && string.IsNullOrWhiteSpace(ValidationError) && !string.IsNullOrWhiteSpace(ApiKey);

    public string ProviderDisplayName => Provider switch
    {
        AssistantProvider.LocalCadOnly => "Local CAD Only",
        AssistantProvider.OpenAI => "OpenAI",
        _ => "DeepSeek"
    };

    public string RequestUrl => $"{BaseUrl.TrimEnd('/')}/chat/completions";

    public string ApiKeyStatus => IsLocalOnly
        ? "API key not required"
        : string.IsNullOrWhiteSpace(ApiKey)
            ? "API key not configured"
            : $"API key configured from {ApiKeySource}";

    public string MissingKeyHint => Provider switch
    {
        AssistantProvider.DeepSeek => "Set DEEPSEEK_API_KEY, paste a session key, or switch to Local CAD Only.",
        AssistantProvider.OpenAI => "Set OPENAI_API_KEY, paste a session key, or switch to Local CAD Only.",
        _ => "Switch to DeepSeek or OpenAI and configure a provider key for remote AI."
    };

    public static AssistantRuntimeConfiguration FromSelection(
        string provider,
        string selectedModel,
        string? baseUrlOverride = null,
        string? keyOverride = null)
    {
        var normalizedProvider = NormalizeProvider(provider);
        if (normalizedProvider == AssistantProvider.LocalCadOnly)
        {
            return new AssistantRuntimeConfiguration
            {
                Provider = normalizedProvider,
                BaseUrl = string.Empty,
                BaseUrlSource = "local",
                Model = "local-cad"
            };
        }

        var defaultBaseUrl = normalizedProvider switch
        {
            AssistantProvider.OpenAI => "https://api.openai.com/v1",
            _ => "https://api.deepseek.com/v1"
        };

        var trimmedOverride = (keyOverride ?? string.Empty).Trim();
        string apiKey;
        string keySource;
        if (!string.IsNullOrWhiteSpace(trimmedOverride))
        {
            apiKey = trimmedOverride;
            keySource = "session input";
        }
        else
        {
            var keyEnvironment = normalizedProvider == AssistantProvider.OpenAI
                ? ReadEnvironmentValue("OPENAI_API_KEY")
                : ReadEnvironmentValue("DEEPSEEK_API_KEY");
            apiKey = keyEnvironment.Value;
            keySource = string.IsNullOrWhiteSpace(keyEnvironment.Source)
                ? string.Empty
                : $"environment variable {keyEnvironment.Source}";
        }

        var baseUrl = (baseUrlOverride ?? string.Empty).Trim();
        var baseUrlSource = "local settings";
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var envBaseUrl = ReadEnvironmentValue("MY3DAPP_LLM_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envBaseUrl.Value))
            {
                baseUrl = envBaseUrl.Value;
                baseUrlSource = $"environment variable {envBaseUrl.Source}";
            }
            else
            {
                baseUrl = defaultBaseUrl;
                baseUrlSource = "provider default";
            }
        }

        var envModel = ReadEnvironmentValue("MY3DAPP_LLM_MODEL");
        var model = !string.IsNullOrWhiteSpace(envModel.Value)
            ? envModel.Value
            : MapDisplayModelToApiModel(normalizedProvider, selectedModel);

        string? validationError = null;
        if (string.IsNullOrWhiteSpace(model))
        {
            validationError = "AI provider model is not configured.";
        }
        else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            validationError = $"AI provider base URL is not a valid absolute URL: {baseUrl}";
        }
        else if (string.IsNullOrWhiteSpace(apiKey))
        {
            validationError = normalizedProvider == AssistantProvider.OpenAI
                ? "OpenAI API key not found."
                : "DeepSeek API key not found.";
        }

        return new AssistantRuntimeConfiguration
        {
            Provider = normalizedProvider,
            ApiKey = apiKey,
            ApiKeySource = keySource,
            BaseUrl = baseUrl,
            BaseUrlSource = baseUrlSource,
            Model = model,
            ValidationError = validationError
        };
    }

    public static AssistantProvider NormalizeProvider(string? provider)
    {
        return provider?.Trim().ToLowerInvariant() switch
        {
            "local" or "local cad" or "local cad only" or "localcadonly" => AssistantProvider.LocalCadOnly,
            "openai" or "open ai" => AssistantProvider.OpenAI,
            "deepseek" or "deep seek" => AssistantProvider.DeepSeek,
            _ => AssistantProvider.DeepSeek
        };
    }

    private static (string Value, string Source) ReadEnvironmentValue(params string[] names)
    {
        foreach (var name in names)
        {
            var processValue = (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(processValue))
            {
                return (processValue, name);
            }

            var userValue = (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(userValue))
            {
                return (userValue, name);
            }

            var machineValue = (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(machineValue))
            {
                return (machineValue, name);
            }
        }

        return (string.Empty, string.Empty);
    }

    private static string MapDisplayModelToApiModel(AssistantProvider provider, string selectedModel)
    {
        var trimmed = (selectedModel ?? string.Empty).Trim();
        var normalized = trimmed.ToLowerInvariant();
        if (provider == AssistantProvider.DeepSeek)
        {
            return normalized switch
            {
                "" => "deepseek-chat",
                "deepseek chat" => "deepseek-chat",
                "deepseek reasoner" => "deepseek-reasoner",
                _ => trimmed
            };
        }

        return normalized switch
        {
            "" => "gpt-4o-mini",
            "gpt-5" => "gpt-5",
            "gpt-5 mini" => "gpt-5-mini",
            "gpt-5.4" => "gpt-5",
            "gpt-5.4 mini" => "gpt-5-mini",
            _ => trimmed
        };
    }
}

public enum AssistantProvider
{
    LocalCadOnly,
    DeepSeek,
    OpenAI
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

    public AssistantCandidate? Candidate { get; private init; }

    public static AssistantChatResult Success(AssistantCandidate candidate)
    {
        return new AssistantChatResult
        {
            IsSuccess = true,
            Reply = candidate.ToDisplayText(),
            Candidate = candidate
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
