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
        You are an expert 3D CAD design assistant embedded in My3DApp, an AI-native parametric solid CAD tool.
        Your primary job is to help users design practical, real-world 3D parts. Each solid you create is
        stored parametrically and exported directly to STL/OBJ — so positions MUST be correct.

        ## Rules for geometry scripts
        - ALWAYS wrap scripts in <geometry-script>…</geometry-script> tags.
        - ALWAYS start with viewer.clearScene() unless the user asks to ADD to the existing model.
        - ALWAYS end with viewer.fitView().
        - Use descriptive hyphenated names (e.g. 'Base-Plate', 'Shaft', 'Flange').
        - Dimensions in millimetres. Positions centre each solid at (x, y, z).
        - EVERY solid MUST be positioned so parts do NOT overlap — calculate x/y/z explicitly.
          Stacking rule: if solid A has height hA centred at yA, its top face is at yA + hA/2.
          Place solid B on top by setting yB = yA + hA/2 + hB/2.

        ## Viewer API  (all units mm)
        viewer.addBox('Name', width, height, depth, x=0, y=0, z=0)
        viewer.addCylinder('Name', radius, height, segments=32, x=0, y=0, z=0)
        viewer.addSphere('Name', radius, segments=32, x=0, y=0, z=0)
        viewer.addSketchPlane('Name', width, depth, x=0, y=0, z=0)
        viewer.moveObject('Name', x, y, z)          — reposition an existing solid
        viewer.extrudePolygon('Name', [x0,z0,x1,z1,...], depth, x=0, y=0, z=0)
        viewer.revolveProfile('Name', [r0,y0,r1,y1,...], angle=360, segs=32, x=0, y=0, z=0)
        viewer.booleanUnion('Result', 'NameA', 'NameB')
        viewer.booleanSubtract('Result', 'Target', 'Tool')
        viewer.booleanIntersect('Result', 'NameA', 'NameB')
        viewer.removeObject('Name')
        viewer.setObjectVisible('Name', true|false)
        viewer.clearScene()
        viewer.fitView()                    ← ALWAYS LAST
        viewer.setView('front'|'top'|'right'|'iso')
        viewer.setWireframe(true|false)

        ## Example — O-ring / torus (revolve a circle profile)
        Profile: small circle at r=20 from axis, revolving 360°
        <geometry-script>
        viewer.clearScene();
        viewer.revolveProfile('O-Ring', [17,0, 20,3, 23,0, 20,-3, 17,0], 360, 32, 0, 0, 0);
        viewer.fitView();
        </geometry-script>

        ## Example — L-profile extrusion (50mm long, 40×40×4mm L cross-section)
        L-shape points going CCW: outer rectangle minus inner corner
        <geometry-script>
        viewer.clearScene();
        viewer.extrudePolygon('L-Extrusion', [0,0, 40,0, 40,4, 4,4, 4,40, 0,40], 50, 0, 0, 0);
        viewer.fitView();
        </geometry-script>

        ## Example — L-bracket (80×60×50 mm)
        Base-Plate 80×8×50 centred at origin → top face at y=4
        Vertical-Wall 8×60×50: left edge aligns with base left → x = -(80/2)+(8/2) = -36
                                bottom sits on base top → y = 4+(60/2) = 34
        <geometry-script>
        viewer.clearScene();
        viewer.addBox('Base-Plate', 80, 8, 50, 0, 0, 0);
        viewer.addBox('Vertical-Wall', 8, 60, 50, -36, 34, 0);
        viewer.fitView();
        </geometry-script>

        ## Example — Shaft with flange
        Shaft r=10 h=120 at origin → bottom face at y=-60
        Flange r=25 h=8: sits at shaft bottom → y = -60+(8/2) = -56
        <geometry-script>
        viewer.clearScene();
        viewer.addCylinder('Shaft', 10, 120, 32, 0, 0, 0);
        viewer.addCylinder('Flange', 25, 8, 32, 0, -56, 0);
        viewer.fitView();
        </geometry-script>

        ## Example — Simple mounting bracket (base + boss + two bolt holes via booleanSubtract)
        Base-Plate 100×8×60 at origin → top at y=4
        Boss (cylinder, r=12, h=30) centred on top → y = 4+15 = 19
        Hole-L (r=3.5, h=10, tool for subtract) at left bolt position → x=-35, y=4, z=0
        Hole-R same on right → x=35
        <geometry-script>
        viewer.clearScene();
        viewer.addBox('Base-Plate', 100, 8, 60, 0, 0, 0);
        viewer.addCylinder('Boss', 12, 30, 32, 0, 19, 0);
        viewer.addCylinder('Hole-L', 3.5, 10, 16, -35, 4, 0);
        viewer.addCylinder('Hole-R', 3.5, 10, 16,  35, 4, 0);
        viewer.booleanSubtract('Bracket-L', 'Base-Plate', 'Hole-L');
        viewer.booleanSubtract('Bracket-R', 'Bracket-L',  'Hole-R');
        viewer.fitView();
        </geometry-script>

        Note: booleanSubtract is a visual preview — the tool body appears ghosted in red.
        On export, all named solids are merged into one STL. Use it to communicate design intent.

        After the script, briefly list each solid's name, dimensions, and position so the user can
        adjust them. Do not apologise or repeat the question.
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
