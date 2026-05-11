using System.Drawing;
using System.Windows.Forms;

namespace My3DApp.Studio;

public sealed partial class StudioForm : Form
{
    private readonly CadChatCommandProcessor _chatProcessor = new();
    private readonly LlmChatClient _llmChatClient = new();
    private readonly CadProjectSerializer _projectSerializer = new();
    private readonly StudioViewportControl _viewport = new();
    private readonly TreeView _featureTree = new();
    private readonly ListBox _projectFilesList = new();
    private readonly TextBox _codeText = new();
    private readonly TextBox _logText = new();
    private readonly FlowLayoutPanel _chatHistory = new();
    private readonly TextBox _chatInput = new();
    private readonly Label _selectionSummary = new();
    private readonly Label _projectTitle = new();
    private readonly TextBox _nameText = new();
    private readonly TextBox _typeText = new();
    private readonly NumericUpDown _widthInput = CreateNumberInput();
    private readonly NumericUpDown _depthInput = CreateNumberInput();
    private readonly NumericUpDown _heightInput = CreateNumberInput();
    private readonly NumericUpDown _radiusInput = CreateNumberInput();
    private readonly NumericUpDown _xInput = CreateNumberInput();
    private readonly NumericUpDown _yInput = CreateNumberInput();
    private readonly NumericUpDown _zInput = CreateNumberInput();
    private readonly ToolTip _tooltip = new();
    private readonly ContextMenuStrip _viewportContextMenu = new();
    private CadDocument _document = new();
    private object? _selectedNode;
    private bool _updatingUi;
    private Point _viewportRightDownPoint;
    private bool _viewportContextCandidate;

    public StudioForm()
    {
        Text = "My3DApp Design Studio";
        MinimumSize = new Size(1500, 920);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = StudioTheme.WindowBackground;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        KeyPreview = true;

        ConfigureTooltips();
        BuildLayout();
        _viewport.EntitySelected += HandleViewportEntitySelected;
        _viewport.MouseDown += HandleViewportMouseDownForContextMenu;
        _viewport.MouseMove += HandleViewportMouseMoveForContextMenu;
        _viewport.MouseUp += HandleViewportMouseUpForContextMenu;
        ConfigureViewportContextMenu();
        KeyDown += HandleStudioKeyDown;
        CreateNewDocument();
        RefreshStudio();
    }

    private void BuildLayout()
    {
        var menu = BuildMenu();
        MainMenuStrip = menu;
        Controls.Add(BuildStatusBar());
        Controls.Add(BuildWorkspace());
        Controls.Add(BuildHeader());
        Controls.Add(menu);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top,
            BackColor = StudioTheme.MenuBackground,
            ForeColor = Color.FromArgb(232, 236, 244),
            GripStyle = ToolStripGripStyle.Hidden
        };

        menu.Items.Add(CreateMenu("File",
            ("New Project", (_, _) => CreateNewDocument()),
            ("Open Project...", (_, _) => OpenProject()),
            ("Save Project...", (_, _) => SaveProject())));
        menu.Items.Add(CreateMenu("Edit"));
        menu.Items.Add(CreateMenu("View"));
        menu.Items.Add(CreateMenu("Design",
            ("Add Box",      (_, _) => AddBody(CreateCubeBody())),
            ("Add Sphere",   (_, _) => AddBody(CreateSphereBody())),
            ("Add Cylinder", (_, _) => AddBody(CreateCylinderBody())),
            ("Add Cone",     (_, _) => AddBody(CreateConeBody())),
            ("Add Torus",    (_, _) => AddBody(CreateTorusBody())),
            ("Add Pyramid",  (_, _) => AddBody(CreatePyramidBody())),
            ("Delete Selected", (_, _) => DeleteSelectedEntity()),
            ("Focus Selected",  (_, _) => FocusSelectedEntity()),
            ("Move Operation",  (_, _) => AddMoveOperation())));
        menu.Items.Add(CreateMenu("Help"));
        ApplyMenuTheme(menu);
        return menu;
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = StudioTheme.HeaderBackground,
            Padding = new Padding(10, 7, 10, 7)
        };

        var logoHost = new Panel
        {
            Dock = DockStyle.Left,
            Width = 56,
            BackColor = StudioTheme.HeaderBackground
        };
        logoHost.Controls.Add(new StudioLogoControl { Dock = DockStyle.Fill });

        var projectPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 340,
            BackColor = StudioTheme.HeaderBackground
        };
        _projectTitle.Dock = DockStyle.Fill;
        _projectTitle.Font = new Font("Segoe UI", 11f, FontStyle.Regular);
        _projectTitle.TextAlign = ContentAlignment.MiddleLeft;
        _projectTitle.ForeColor = Color.FromArgb(220, 225, 234);
        _projectTitle.Padding = new Padding(12, 0, 0, 0);
        projectPanel.Controls.Add(_projectTitle);

        var utilButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 200,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 0)
        };

        var rebuildBtn = CreateHeaderButton("Rebuild", 72, false);
        rebuildBtn.Click += (_, _) => { RefreshStudio(); Log("Rebuilt display from solid Design IR."); };
        var redoBtn = CreateHeaderButton("Redo", 52, false);
        redoBtn.Enabled = false;
        var undoBtn = CreateHeaderButton("Undo", 52, false);
        undoBtn.Enabled = false;

        utilButtons.Controls.Add(rebuildBtn);
        utilButtons.Controls.Add(redoBtn);
        utilButtons.Controls.Add(undoBtn);

        header.Controls.Add(utilButtons);
        header.Controls.Add(projectPanel);
        header.Controls.Add(logoHost);
        return header;
    }

    private Control BuildWorkspace()
    {
        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor
        };

        var left = BuildLeftColumn();
        left.Dock = DockStyle.Left;
        left.Width = 280;

        var right = BuildRightColumn();
        right.Dock = DockStyle.Right;
        right.Width = 320;

        var center = BuildCenterColumn();
        center.Dock = DockStyle.Fill;

        workspace.Controls.Add(center);
        workspace.Controls.Add(right);
        workspace.Controls.Add(left);
        return workspace;
    }
}
