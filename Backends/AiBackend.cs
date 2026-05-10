using System.Net.Http;
using System.Text;
using System.Text.Json;
using My3DApp.Core;

namespace My3DApp.Backends;

/// <summary>
/// Talks to the Claude API (claude-sonnet-4-6) for design assistance.
/// Parses optional <geometry-script> blocks from the reply for live viewport updates.
/// </summary>
public class AiBackend : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly List<ChatMessage> _history = new();
    private const string ApiUrl     = "https://api.anthropic.com/v1/messages";
    private const string Model      = "claude-sonnet-4-6";
    private const int    MaxTurns   = 20;   // keep last N user+assistant pairs to avoid token overflow
    private const int    MaxTokens  = 3000; // room for explanation + multi-component geometry scripts

    public string? LastGeometryScript { get; private set; }

    private CancellationTokenSource? _cts;

    public void CancelCurrentRequest()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void ClearHistory() => _history.Clear();

    private static readonly string SystemPrompt = """
        You are an expert 3D CAD design assistant embedded in My3DApp, an AI-native parametric CAD tool.
        Your primary job is to help users design practical, real-world 3D parts by generating geometry
        scripts that run live in the Three.js viewport.

        ## Rules for geometry scripts
        - ALWAYS wrap scripts in <geometry-script>…</geometry-script> tags when creating or modifying geometry.
        - ALWAYS start with viewer.clearScene() unless the user asks you to ADD to the existing model.
        - ALWAYS end with viewer.fitView().
        - Use descriptive, single-word-or-hyphenated names for each part (e.g. 'Base-Plate', 'Shaft', 'Flange').
        - Dimensions must be realistic and in millimetres.
        - Decompose complex shapes into named sub-components (each gets its own addBox/addCylinder call).

        ## Viewer API
        viewer.addBox('Name', width, height, depth)          — rectangular solid
        viewer.addCylinder('Name', radius, height, segments) — cylinder / rod / tube
        viewer.addSphere('Name', radius, segments)           — sphere / ball
        viewer.addSketchPlane('Name', width, depth)          — flat reference plane
        viewer.removeObject('Name')                          — remove one object
        viewer.setObjectVisible('Name', true|false)          — show/hide
        viewer.clearScene()                                  — remove everything
        viewer.fitView()                                     — zoom to fit  ← ALWAYS LAST
        viewer.setView('front'|'top'|'right'|'iso')         — camera preset
        viewer.setWireframe(true|false)                      — wireframe toggle

        ## Example — L-bracket
        <geometry-script>
        viewer.clearScene();
        viewer.addBox('Base-Plate', 80, 8, 50);
        viewer.addBox('Vertical-Wall', 8, 60, 50);
        viewer.fitView();
        </geometry-script>

        ## Example — Shaft with flange
        <geometry-script>
        viewer.clearScene();
        viewer.addCylinder('Shaft', 10, 120, 32);
        viewer.addCylinder('Flange', 25, 8, 32);
        viewer.fitView();
        </geometry-script>

        Keep explanations brief. After the geometry script, summarise the key dimensions and how the
        user could modify them parametrically. Do not apologise or repeat the question.
        """;

    public AiBackend()
    {
        _http.DefaultRequestHeaders.Add("x-api-key", GetApiKey());
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Sends a chat message to the AI.
    /// <paramref name="userMessage"/> is shown in chat history.
    /// If <paramref name="contextPrefix"/> is provided it is prepended to the API request
    /// only (not stored in history), keeping context fresh without inflating past turns.
    /// </summary>
    public async Task<string> ChatAsync(string userMessage, string? contextPrefix = null)
    {
        CancelCurrentRequest(); // cancel any prior in-flight request
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        LastGeometryScript = null;

        var apiKey = GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
            return "API key not configured. Set ANTHROPIC_API_KEY environment variable.";

        _history.Add(new ChatMessage("user", userMessage)); // plain message in history

        try
        {
            // Trim to the most recent MaxTurns pairs to stay within context limits
            var window = _history.Count > MaxTurns * 2
                ? _history.TakeLast(MaxTurns * 2).ToList()
                : _history.ToList();

            // Inject context prefix into the last user message for this request only
            var apiMessages = window.Select((m, i) =>
            {
                if (i == window.Count - 1 && m.Role == "user" && contextPrefix != null)
                    return new { role = m.Role, content = contextPrefix + "\n\n" + m.Content };
                return new { role = m.Role, content = m.Content };
            }).ToArray();

            var request = new
            {
                model = Model,
                max_tokens = MaxTokens,
                system = SystemPrompt,
                messages = apiMessages
            };

            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(ApiUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                RuntimeLog.Error("AI", $"API error {response.StatusCode}: {err}");
                return $"AI API error ({response.StatusCode}). Check logs.";
            }

            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            // Extract optional geometry script
            const string scriptOpen  = "<geometry-script>";
            const string scriptClose = "</geometry-script>";
            var start = text.IndexOf(scriptOpen, StringComparison.Ordinal);
            var end   = text.IndexOf(scriptClose, StringComparison.Ordinal);
            if (start >= 0 && end > start)
            {
                LastGeometryScript = text[(start + scriptOpen.Length)..end].Trim();
                // Remove the script block from the displayed text
                text = (text[..start] + text[(end + scriptClose.Length)..]).Trim();
            }

            _history.Add(new ChatMessage("assistant", text));
            return text;
        }
        catch (OperationCanceledException)
        {
            _history.RemoveAt(_history.Count - 1); // remove the unsent user message
            return "(Request cancelled.)";
        }
        catch (Exception ex)
        {
            RuntimeLog.Error("AI", "Chat request failed.", ex);
            return $"Error: {ex.Message}";
        }
    }

    private static string GetApiKey()
        => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;

    public void Dispose() => _http.Dispose();

    private record ChatMessage(string Role, string Content);
}
