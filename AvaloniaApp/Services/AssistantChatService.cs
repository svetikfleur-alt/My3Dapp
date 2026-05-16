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
                ? AssistantChatResult.Success(envelope.Value.reply, envelope.Value.command,
                      envelope.Value.solidSculpt, envelope.Value.formaScript)
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
        sb.AppendLine("  1. Always return a JSON object with a \"reply\" key. Never return plain text.");
        sb.AppendLine("     Use ONE of the three output modes:");
        sb.AppendLine("       CAD command:   {\"reply\": \"...\", \"command\": \"...\"}");
        sb.AppendLine("       FormaScript:   {\"reply\": \"...\", \"formascript\": \"...\"}");
        sb.AppendLine("       SolidSculpt:   {\"reply\": \"...\", \"solidsculpt\": \"...\"}");
        sb.AppendLine("     command, formascript, and solidsculpt are mutually exclusive — only set the one you need.");
        sb.AppendLine("  2. \"reply\" is shown in chat — 1-3 sentences, no markdown, no bullet lists.");
        sb.AppendLine("  3. \"command\" is executed by the CAD engine — use exact syntax from vocabulary below.");
        sb.AppendLine("  4. \"formascript\" contains a complete FormaScript program (multi-line string). Use when the user needs parametric generation, loops, or repeating patterns.");
        sb.AppendLine("  5. \"solidsculpt\" contains a complete SolidSculpt program (multi-line string). Use when the user asks for organic, sculptural, or noise-based geometry.");
        sb.AppendLine("  6. Never invent command syntax not listed below — prefer clarifying questions over bad commands.");
        sb.AppendLine("  7. Prefer a single recipe over a long step sequence when the user describes a standard part.");
        var modeInstruction = ctx.Mode switch
        {
            "Do" =>
                "  8. Mode=Do: Always include a command, formascript, or solidsculpt. Never reply with all three empty.",
            "Think" =>
                "  8. Mode=Think: Reason through the geometry in your reply (2-3 sentences), then provide the best output.",
            "Assist" =>
                "  8. Mode=Assist: Guidance only — explain what the user should do step by step but leave command/formascript/solidsculpt empty.",
            "Think&Do" =>
                "  8. Mode=Think&Do: First reason through the problem (up to 4 sentences), then provide a complete, confident output.",
            _ =>
                "  8. Mode=Auto: Provide a command, formascript, or solidsculpt when you are confident it matches the intent."
        };
        sb.AppendLine(modeInstruction);
        sb.AppendLine();
        sb.AppendLine("## Typical workflows");
        sb.AppendLine("  Extruded boss:      command  → select plane top; start sketch; rectangle 0 0 40 20; finish sketch; extrude 10");
        sb.AppendLine("  Cut through-hole:   command  → select plane top; start sketch; circle 20 10 radius 4; finish sketch; extrude cut 30");
        sb.AppendLine("  Revolve shaft:      command  → select plane front; start sketch; rectangle 0 0 5 30; finish sketch; revolve 360 around y");
        sb.AppendLine("  Hollow enclosure:   command  → recipe shelled-box 60 40 30 2");
        sb.AppendLine("  Mounting plate:     command  → recipe plate-with-hole 80 50 6 5");
        sb.AppendLine("  L-bracket:          command  → recipe bracket 40 30 25 4");
        sb.AppendLine("  Flanged shaft:      command  → recipe flange 18 4 6 25");
        sb.AppendLine("  Tube/standoff:      command  → recipe hollow-cylinder 12 9 25");
        sb.AppendLine("  Fillet all edges:   command  → fillet 2");
        sb.AppendLine("  Shell after solid:  command  → shell 1.5");
        sb.AppendLine("  Mirror half:        command  → mirror x");
        sb.AppendLine("  Bolt pattern:       command  → circular pattern 6 angle 360 around z");
        sb.AppendLine("  Slot array:         command  → linear pattern 4 spacing 20 along x");
        sb.AppendLine("  Merge bodies:       command  → boolean union");
        sb.AppendLine("  Subtract cutout:    command  → boolean subtract");
        sb.AppendLine("  Grid of cylinders:  formascript → var n=5 var gap=30 / repeat n / add cylinder / linear pattern n spacing gap along x / end / linear pattern n spacing gap along y");
        sb.AppendLine("  Gear with N teeth:  formascript → var teeth=12 / add cylinder / circular pattern teeth angle 360 around y / fillet 2");
        sb.AppendLine("  Rocky asteroid:     solidsculpt → base sphere 40 / subdivide 2 / noise 0.08 12 31 / smooth 3 0.4");
        sb.AppendLine("  Organic blob:       solidsculpt → base sphere 30 / subdivide 3 / smooth 2 0.6 / inflate 5 / noise 0.1 8 42");
        sb.AppendLine("  Twisted column:     solidsculpt → base cylinder 15 80 / subdivide 2 / smooth 2 0.5 / twist 120");
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
        sb.AppendLine("## FormaScript — parametric CAD scripting language");
        sb.AppendLine("  Use when: user wants looping geometry, parametric grids, variable-driven shapes, or complex multi-step patterns.");
        sb.AppendLine("  Return as:  {\"reply\": \"...\", \"formascript\": \"<full multiline script>\"}");
        sb.AppendLine("  Syntax (each on its own line, comments start with //):");
        sb.AppendLine("    var name = expr                   — variable (supports +,-,*,/,%,^ and sqrt/abs/sin/cos/tan/pi)");
        sb.AppendLine("    name = expr                       — reassignment");
        sb.AppendLine("    repeat N  ...  end                — loop N times");
        sb.AppendLine("    for i in range(a, b)  ...  end    — range loop");
        sb.AppendLine("    for i in range(a, b, step)  ...  end");
        sb.AppendLine("    if expr op expr  ...  end         — conditional (==,!=,<,>,<=,>=)");
        sb.AppendLine("    def name(p1, p2)  ...  end        — function definition");
        sb.AppendLine("    name(arg1, arg2)                  — function call");
        sb.AppendLine("    Any CAD command (add box, extrude, fillet, etc.)  — executed with variable substitution");
        sb.AppendLine("  Examples:");
        sb.AppendLine("    Bolt hole circle:");
        sb.AppendLine("      var n = 6  var r = 40");
        sb.AppendLine("      repeat n");
        sb.AppendLine("        add cylinder");
        sb.AppendLine("        circular pattern n angle 360 around y");
        sb.AppendLine("      end");
        sb.AppendLine("    Grid of boxes:");
        sb.AppendLine("      var cols = 4  var rows = 3  var size = 20  var gap = 5");
        sb.AppendLine("      repeat rows");
        sb.AppendLine("        repeat cols");
        sb.AppendLine("          add box");
        sb.AppendLine("          linear pattern cols spacing (size + gap) along x");
        sb.AppendLine("        end");
        sb.AppendLine("        linear pattern rows spacing (size + gap) along y");
        sb.AppendLine("      end");

        sb.AppendLine();
        sb.AppendLine("## SolidSculpt — organic mesh sculpting language");
        sb.AppendLine("  Use when: user wants organic, artistic, bumpy, twisted, bent, tapered, or noise-deformed geometry.");
        sb.AppendLine("  Return as:  {\"reply\": \"...\", \"solidsculpt\": \"<full multiline script>\"}");
        sb.AppendLine("  Syntax (each on its own line):");
        sb.AppendLine("    base sphere [radius]              — start from a sphere (default 30)");
        sb.AppendLine("    base box [w] [h] [d]              — start from a box");
        sb.AppendLine("    base cylinder [radius] [height]   — start from a cylinder");
        sb.AppendLine("    subdivide [n]                     — loop subdivide (1–5, each ×4 triangles)");
        sb.AppendLine("    smooth [iters] [strength 0-1]     — Laplacian smoothing");
        sb.AppendLine("    inflate [amount]                  — push vertices along normals");
        sb.AppendLine("    noise [scale] [strength] [seed]   — procedural noise displacement");
        sb.AppendLine("    pinch [strength] [falloff]        — pull toward centroid");
        sb.AppendLine("    twist [degrees]                   — twist around Z axis");
        sb.AppendLine("    bend [degrees]                    — bend along X axis");
        sb.AppendLine("    taper [topScale] [botScale]       — scale XY by height fraction");
        sb.AppendLine("    spherize [strength] [radius]      — blend toward sphere");
        sb.AppendLine("    var name = expr  |  repeat N ... end  (same as FormaScript)");
        sb.AppendLine("  Examples:");
        sb.AppendLine("    Rocky asteroid:  base sphere 40 → subdivide 2 → noise 0.08 12 31 → smooth 3 0.4");
        sb.AppendLine("    Twisted pillar:  base cylinder 15 80 → subdivide 2 → smooth 2 0.5 → twist 120");
        sb.AppendLine("    Organic blob:    base sphere 30 → subdivide 3 → smooth 2 0.6 → inflate 5 → noise 0.1 8 42 → spherize 0.4");

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
            max_tokens = 2048,
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
            max_tokens = 2048,
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

    private static (string reply, string? command, string? solidSculpt, string? formaScript)?
        ParseEnvelope(string content)
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
        var solidSculpt = root.TryGetProperty("solidsculpt", out var ssElement) &&
                          ssElement.ValueKind == JsonValueKind.String
            ? ssElement.GetString()
            : null;
        var formaScript = root.TryGetProperty("formascript", out var fsElement) &&
                          fsElement.ValueKind == JsonValueKind.String
            ? fsElement.GetString()
            : null;

        return (reply,
            string.IsNullOrWhiteSpace(command) ? null : command.Trim(),
            string.IsNullOrWhiteSpace(solidSculpt) ? null : solidSculpt.Trim(),
            string.IsNullOrWhiteSpace(formaScript) ? null : formaScript.Trim());
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

    /// <summary>A SolidSculpt script block if the assistant returned one, otherwise null.</summary>
    public string? SolidSculptScript { get; private init; }

    /// <summary>A FormaScript block if the assistant returned one, otherwise null.</summary>
    public string? FormaScript { get; private init; }

    public static AssistantChatResult Success(string reply, string? command,
        string? solidSculptScript = null, string? formaScript = null)
    {
        return new AssistantChatResult
        {
            IsSuccess = true,
            Reply = reply,
            Command = command,
            SolidSculptScript = string.IsNullOrWhiteSpace(solidSculptScript) ? null : solidSculptScript.Trim(),
            FormaScript = string.IsNullOrWhiteSpace(formaScript) ? null : formaScript.Trim()
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
