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
    private const int    MaxTokens  = 2048; // enough for geometry scripts + explanations

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
        You are an expert 3D CAD design assistant embedded in My3DApp, an AI-native Onshape-style CAD tool.
        You help users design 3D parts using parametric features: sketches, extrudes, revolves, lofts, shells,
        and boolean operations.

        When the user asks you to create or modify geometry, always include a JavaScript script block
        that executes in the 3D viewport. ALWAYS call viewer.fitView() at the end of the script.

        <geometry-script>
        // Viewer API reference:
        // viewer.addBox(name, w, h, d)               — box mesh (width, height, depth in mm)
        // viewer.addCylinder(name, r, h, segments)   — cylinder mesh
        // viewer.addSphere(name, r, segments)         — sphere mesh
        // viewer.addSketchPlane(name, w, d)           — flat semi-transparent sketch plane
        // viewer.removeObject(name)                   — remove a named mesh
        // viewer.setObjectVisible(name, true/false)   — show/hide a named mesh
        // viewer.clearScene()                         — clear all geometry
        // viewer.fitView()                            — fit camera to scene (ALWAYS call last)
        // viewer.setView('front'|'top'|'right'|'iso') — camera preset
        // viewer.setWireframe(true/false)             — toggle wireframe mode
        // viewer.loadSTL(base64, name)                — load a binary STL from base64 data
        //
        // IMPORTANT: Always end geometry scripts with viewer.fitView()
        // Example — create a bracket:
        viewer.clearScene();
        viewer.addBox('Base', 100, 10, 60);
        viewer.addBox('Wall', 10, 50, 60);
        viewer.fitView();
        </geometry-script>

        Keep explanations concise. Focus on parametric thinking and design intent.
        Use realistic dimensions in millimetres.
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
