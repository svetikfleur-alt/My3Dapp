using System.Drawing;
using System.Windows.Forms;

namespace My3DApp.Studio;

public sealed partial class StudioForm
{
    private static string IconPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", $"{name}.svg");

    private Control BuildLeftColumn()
    {
        var host = new Panel { BackColor = StudioTheme.SurfacePrimary };

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 300,
            BackColor = StudioTheme.Border
        };

        split.Panel1.Controls.Add(BuildFeatureTreeSection());
        split.Panel2.Controls.Add(BuildParametersSection());

        host.Controls.Add(split);
        return host;
    }

    private Control BuildCenterColumn()
    {
        var host = new Panel { BackColor = StudioTheme.SurfacePrimary };

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = StudioTheme.ToolbarBackground,
            Padding = new Padding(8, 6, 8, 6)
        };

        var tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        tools.Controls.Add(CreateToolbarGroup(
            CreateIconButton("sketch-plane", "Sketch / 3D Mode")));
        tools.Controls.Add(CreateGroupSeparator());

        tools.Controls.Add(CreateToolbarGroup(
            CreateIconButton("line", "Start Sketch")));
        tools.Controls.Add(CreateGroupSeparator());

        tools.Controls.Add(CreateToolbarGroup(
            CreateSplitDropdownTool(
                "extrude",
                "Extrude",
                null,
                true,
                ("extrude", "Extrude", null, false),
                ("revolve", "Revolve", null, false))));
        tools.Controls.Add(CreateGroupSeparator());

        tools.Controls.Add(CreateToolbarGroup(
            CreateSplitDropdownTool(
                "boolean",
                "Boolean Operations",
                null,
                true,
                ("boolean", "Union", null, false),
                ("boolean", "Subtract", null, false),
                ("boolean", "Intersect", null, false))));
        tools.Controls.Add(CreateGroupSeparator());

        tools.Controls.Add(CreateToolbarGroup(
            CreateSplitDropdownTool(
                "fillet",
                "Fillet",
                null,
                true,
                ("fillet", "Fillet", null, false),
                ("chamfer", "Chamfer", null, false))));
        tools.Controls.Add(CreateGroupSeparator());

        tools.Controls.Add(CreateToolbarGroup(
            CreateSplitDropdownTool(
                "box",
                "Add Box",
                (_, _) => AddBody(CreateCubeBody()),
                true,
                ("box", "Box", (_, _) => AddBody(CreateCubeBody()), true),
                ("sphere", "Sphere", (_, _) => AddBody(CreateSphereBody()), true),
                ("cylinder", "Cylinder", (_, _) => AddBody(CreateCylinderBody()), true),
                ("cone", "Cone", (_, _) => AddBody(CreateConeBody()), true),
                ("torus", "Torus", (_, _) => AddBody(CreateTorusBody()), true),
                ("pyramid", "Pyramid", (_, _) => AddBody(CreatePyramidBody()), true))));
        tools.Controls.Add(CreateGroupSeparator());

        tools.Controls.Add(CreateToolbarGroup(
            CreateSplitDropdownTool(
                "delete",
                "Delete Selected",
                (_, _) => DeleteSelectedEntity(),
                true,
                ("delete", "Delete Selected", (_, _) => DeleteSelectedEntity(), true),
                ("focus", "Focus Selected", (_, _) => FocusSelectedEntity(), true))));

        toolbar.Controls.Add(tools);

        var viewportCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = StudioTheme.SurfaceRaised
        };

        var viewportHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = StudioTheme.SurfaceInset
        };

        var viewportTitle = new Label
        {
            Dock = DockStyle.Left,
            Width = 260,
            Text = "Solid CAD View",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f),
            ForeColor = StudioTheme.TextMuted,
            Padding = new Padding(10, 0, 0, 0)
        };

        _selectionSummary.Dock = DockStyle.Right;
        _selectionSummary.Width = 340;
        _selectionSummary.TextAlign = ContentAlignment.MiddleRight;
        _selectionSummary.Font = new Font("Segoe UI", 9f);
        _selectionSummary.ForeColor = StudioTheme.TextMuted;
        _selectionSummary.Padding = new Padding(0, 0, 10, 0);

        viewportHeader.Controls.Add(_selectionSummary);
        viewportHeader.Controls.Add(viewportTitle);

        _viewport.Dock = DockStyle.Fill;
        viewportCard.Controls.Add(_viewport);
        viewportCard.Controls.Add(viewportHeader);

        host.Controls.Add(viewportCard);
        host.Controls.Add(toolbar);
        return host;
    }

    private Control BuildRightColumn()
    {
        var host = new Panel
        {
            BackColor = StudioTheme.SurfacePrimary,
            Padding = new Padding(5, 0, 0, 0)
        };

        host.Controls.Add(BuildAssistantPanel());
        return host;
    }

    // ── Left sidebar sections ────────────────────────────────────────────────

    private Control BuildFeatureTreeSection()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = StudioTheme.SurfaceRaised
        };

        var header = BuildSectionHeader("Feature Tree");

        _featureTree.Dock = DockStyle.Fill;
        _featureTree.HideSelection = false;
        _featureTree.BorderStyle = BorderStyle.None;
        _featureTree.Font = new Font("Segoe UI", 9f);
        _featureTree.BackColor = StudioTheme.SurfaceRaised;
        _featureTree.ForeColor = StudioTheme.TextPrimary;
        _featureTree.Indent = 14;
        _featureTree.ItemHeight = 20;
        _featureTree.AfterSelect += (_, e) =>
        {
            _selectedNode = e.Node?.Tag;
            RefreshSelectionViews();
        };

        panel.Controls.Add(_featureTree);
        panel.Controls.Add(header);
        return panel;
    }

    private Control BuildParametersSection()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = StudioTheme.SurfaceRaised,
            AutoScroll = true,
            Padding = new Padding(0, 0, 0, 8)
        };

        var header = BuildSectionHeader("Parameters");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(10, 4, 10, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, "Name", _nameText);
        AddRow(layout, "Type", _typeText);
        AddRow(layout, "Width", _widthInput);
        AddRow(layout, "Depth", _depthInput);
        AddRow(layout, "Height", _heightInput);
        AddRow(layout, "Radius", _radiusInput);
        AddRow(layout, "X / dX", _xInput);
        AddRow(layout, "Y / dY", _yInput);
        AddRow(layout, "Z / dZ", _zInput);

        StyleParameterInput(_nameText);
        _typeText.BorderStyle = BorderStyle.None;
        _typeText.Font = new Font("Segoe UI", 9f);
        _typeText.ReadOnly = true;
        _typeText.BackColor = StudioTheme.SurfaceRaised;
        _typeText.ForeColor = StudioTheme.TextMuted;

        _nameText.TextChanged += (_, _) => ApplyPropertyChanges();
        _widthInput.ValueChanged += (_, _) => ApplyPropertyChanges();
        _depthInput.ValueChanged += (_, _) => ApplyPropertyChanges();
        _heightInput.ValueChanged += (_, _) => ApplyPropertyChanges();
        _radiusInput.ValueChanged += (_, _) => ApplyPropertyChanges();
        _xInput.ValueChanged += (_, _) => ApplyPropertyChanges();
        _yInput.ValueChanged += (_, _) => ApplyPropertyChanges();
        _zInput.ValueChanged += (_, _) => ApplyPropertyChanges();

        panel.Controls.Add(layout);
        panel.Controls.Add(header);
        return panel;
    }

    private static void StyleParameterInput(TextBox tb)
    {
        tb.BorderStyle = BorderStyle.FixedSingle;
        tb.Font = new Font("Segoe UI", 9f);
        tb.BackColor = StudioTheme.SurfaceRaised;
        tb.ForeColor = StudioTheme.TextPrimary;
    }

    // ── Right panel: AI assistant ───────────────────────────────────────────

    private Control BuildAssistantPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = StudioTheme.SurfaceRaised
        };

        // Header
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = StudioTheme.SurfaceInset,
            Padding = new Padding(10, 8, 10, 6)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Text = "AI Assistant",
            Font = new Font("Segoe UI Semibold", 9.5f),
            ForeColor = StudioTheme.TextSecondary
        };

        var modeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        modeRow.Controls.Add(CreateModeChip("Auto",     true,  48));
        modeRow.Controls.Add(CreateModeChip("Do",       false, 38));
        modeRow.Controls.Add(CreateModeChip("Think",    false, 50));
        modeRow.Controls.Add(CreateModeChip("Assist",   false, 54));
        modeRow.Controls.Add(CreateModeChip("Think&Do", false, 70));

        header.Controls.Add(modeRow);
        header.Controls.Add(title);

        // Chat output
        _chatHistory.Dock = DockStyle.Fill;
        _chatHistory.FlowDirection = FlowDirection.TopDown;
        _chatHistory.WrapContents = false;
        _chatHistory.AutoScroll = true;
        _chatHistory.Padding = new Padding(0, 4, 0, 4);
        _chatHistory.BackColor = StudioTheme.SurfaceRaised;

        // Composer
        var composer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            Padding = new Padding(8),
            BackColor = StudioTheme.SurfaceInset
        };

        var sendButton = new Button
        {
            Dock = DockStyle.Right,
            Width = 42,
            Text = "↵",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 13f),
            BackColor = StudioTheme.Accent,
            ForeColor = Color.White,
            UseVisualStyleBackColor = false
        };
        sendButton.FlatAppearance.BorderSize = 0;
        sendButton.Click += async (_, _) => await SubmitChatAsync();

        _chatInput.Dock = DockStyle.Fill;
        _chatInput.Multiline = true;
        _chatInput.Font = new Font("Consolas", 9.5f);
        _chatInput.BorderStyle = BorderStyle.FixedSingle;
        _chatInput.BackColor = StudioTheme.SurfaceRaised;
        _chatInput.ForeColor = StudioTheme.TextPrimary;
        _chatInput.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                _ = SubmitChatAsync();
            }
        };

        composer.Controls.Add(sendButton);
        composer.Controls.Add(_chatInput);

        panel.Controls.Add(_chatHistory);
        panel.Controls.Add(composer);
        panel.Controls.Add(header);
        return panel;
    }

    // ── Status bar ──────────────────────────────────────────────────────────

    private Control BuildStatusBar()
    {
        var status = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            BackColor = StudioTheme.SurfaceInset,
            SizingGrip = false
        };

        status.Items.Add(new ToolStripStatusLabel("v1.2.22") { ForeColor = StudioTheme.TextMuted });
        status.Items.Add(new ToolStripStatusLabel("·") { ForeColor = StudioTheme.TextMuted });
        status.Items.Add(new ToolStripStatusLabel("local solid IR") { ForeColor = StudioTheme.TextMuted });
        status.Items.Add(new ToolStripStatusLabel { Spring = true });
        status.Items.Add(new ToolStripStatusLabel("mm") { ForeColor = StudioTheme.TextMuted });
        status.Items.Add(new ToolStripStatusLabel("ready") { ForeColor = StudioTheme.TextMuted });
        return status;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Panel BuildSectionHeader(string title)
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = StudioTheme.SurfaceInset
        };

        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title.ToUpperInvariant(),
            Font = new Font("Segoe UI Semibold", 8f),
            ForeColor = StudioTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        });

        return header;
    }
}
