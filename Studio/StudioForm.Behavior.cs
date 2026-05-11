using System.Drawing;
using System.Windows.Forms;

namespace My3DApp.Studio;

public sealed partial class StudioForm
{
    private void CreateNewDocument()
    {
        _document = new CadDocument();
        _document.Scene.EnsureReferencePlanes();
        _selectedNode = _document.Scene;
        _chatHistory.Controls.Clear();
        AddChatMessage("system", "Solid CAD Studio ready.");
        AddChatMessage("system", "Try: create cube 20x10x5, add cylinder radius 5 height 20, add sphere radius 8");
        AddChatMessage("system", "For real LLM chat, set MY3DAPP_LLM_API_KEY and optionally MY3DAPP_LLM_BASE_URL / MY3DAPP_LLM_MODEL.");
        Log("Created a clean CAD workspace with base planes and no demo solids.");
        RefreshStudio();
    }

    private string NextBodyName(string prefix)
    {
        var max = 0;
        foreach (var body in _document.Scene.Bodies)
        {
            if (!body.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = body.Name[prefix.Length..];
            if (int.TryParse(suffix, out var value))
            {
                max = Math.Max(max, value);
            }
        }

        return $"{prefix}{max + 1}";
    }

    private CadBody CreateCubeBody()
    {
        var name = NextBodyName("Cube");
        return new CadBody
        {
            Name = name,
            Features = new List<CadFeature>
            {
                new BoxFeature
                {
                    Name = name,
                    Width = 24,
                    Depth = 24,
                    Height = 18
                }
            }
        };
    }

    private CadBody CreateCylinderBody()
    {
        var name = NextBodyName("Cylinder");
        return new CadBody
        {
            Name = name,
            Features = new List<CadFeature>
            {
                new CylinderFeature
                {
                    Name = name,
                    Radius = 12,
                    Height = 16
                }
            }
        };
    }

    private CadBody CreateSphereBody()
    {
        var name = NextBodyName("Sphere");
        return new CadBody
        {
            Name = name,
            Features = new List<CadFeature>
            {
                new SphereFeature { Name = name, Radius = 12 }
            }
        };
    }

    private CadBody CreateConeBody()
    {
        var name = NextBodyName("Cone");
        return new CadBody
        {
            Name = name,
            Features = new List<CadFeature>
            {
                new ConeFeature { Name = name, Radius = 12, Height = 20 }
            }
        };
    }

    private CadBody CreateTorusBody()
    {
        var name = NextBodyName("Torus");
        return new CadBody
        {
            Name = name,
            Features = new List<CadFeature>
            {
                new TorusFeature { Name = name, MajorRadius = 14, MinorRadius = 4 }
            }
        };
    }

    private CadBody CreatePyramidBody()
    {
        var name = NextBodyName("Pyramid");
        return new CadBody
        {
            Name = name,
            Features = new List<CadFeature>
            {
                new PyramidFeature { Name = name, BaseWidth = 20, BaseDepth = 20, Height = 18 }
            }
        };
    }

    private void AddBody(CadBody body)
    {
        _document.Scene.Bodies.Add(body);
        _selectedNode = body;
        Log($"Added body {body.Name}.");
        RefreshStudio();
    }

    private void DeleteSelectedEntity()
    {
        switch (_selectedNode)
        {
            case CadBody body:
            {
                _document.Scene.Bodies.Remove(body);
                _selectedNode = (object?)_document.Scene.Bodies.LastOrDefault() ?? _document.Scene;
                Log($"Deleted body {body.Name}.");
                RefreshStudio();
                return;
            }
            case CadFeature feature:
            {
                var owner = _document.Scene.Bodies.FirstOrDefault(candidate => candidate.Features.Contains(feature));
                if (owner is null)
                {
                    return;
                }

                if (feature is BoxFeature or CylinderFeature or SphereFeature or ConeFeature or TorusFeature or PyramidFeature)
                {
                    _document.Scene.Bodies.Remove(owner);
                    _selectedNode = (object?)_document.Scene.Bodies.LastOrDefault() ?? _document.Scene;
                    Log($"Deleted body {owner.Name}.");
                }
                else
                {
                    owner.Features.Remove(feature);
                    _selectedNode = owner;
                    Log($"Deleted feature {feature.Name} from {owner.Name}.");
                }

                RefreshStudio();
                return;
            }
            case CadReferencePlane plane:
                AddChatMessage("error", $"Reference plane '{plane.Name}' cannot be deleted.");
                return;
            default:
                return;
        }
    }

    private void FocusSelectedEntity()
    {
        _viewport.FocusOnNode(_selectedNode);
        Log("Focused camera on selected entity.");
    }

    private void AddMoveOperation()
    {
        var body = SelectedBody;
        if (body is null)
        {
            AddChatMessage("error", "Select a body before adding a move operation.");
            return;
        }

        var move = new MoveFeature
        {
            Name = $"Move{body.Features.Count(feature => feature is MoveFeature) + 1}",
            X = 10
        };

        var insertIndex = body.Features.FindIndex(feature => feature.Role == CadFeatureRole.Placeholder);
        if (insertIndex >= 0)
        {
            body.Features.Insert(insertIndex, move);
        }
        else
        {
            body.Features.Add(move);
        }

        _selectedNode = move;
        Log($"Added move operation to {body.Name}.");
        RefreshStudio();
    }

    private void OpenProject()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "My3DApp CAD Project (*.my3d.json)|*.my3d.json|JSON Files (*.json)|*.json",
            Title = "Open CAD Project"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _document = _projectSerializer.Load(dialog.FileName);
            _document.Scene.EnsureReferencePlanes();
            _selectedNode = (object?)_document.Scene.Bodies.FirstOrDefault() ?? _document.Scene;
            AddChatMessage("system", $"Opened project: {Path.GetFileName(dialog.FileName)}");
            Log($"Opened project from {dialog.FileName}.");
            RefreshStudio();
        }
        catch (Exception ex)
        {
            AddChatMessage("error", $"Open failed: {ex.Message}");
            Log($"Open failed: {ex}");
        }
    }

    private void SaveProject()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "My3DApp CAD Project (*.my3d.json)|*.my3d.json|JSON Files (*.json)|*.json",
            Title = "Save CAD Project",
            FileName = $"{_document.Name.Replace(' ', '_')}.my3d.json"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _projectSerializer.Save(dialog.FileName, _document);
            AddChatMessage("system", $"Saved project to {Path.GetFileName(dialog.FileName)}");
            Log($"Saved project to {dialog.FileName}.");
        }
        catch (Exception ex)
        {
            AddChatMessage("error", $"Save failed: {ex.Message}");
            Log($"Save failed: {ex}");
        }
    }

    private async Task SubmitChatAsync()
    {
        var input = _chatInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        _chatInput.Clear();
        AddChatMessage("user", input);

        var result = _chatProcessor.Execute(_document, input, SelectedBody);
        if (result.IsSuccess)
        {
            AddChatMessage("system", result.Message);
            Log(result.Message);

            if (result.SelectedFeature is not null)
            {
                _selectedNode = result.SelectedFeature;
            }
            else if (result.SelectedBody is not null)
            {
                _selectedNode = result.SelectedBody;
            }

            RefreshStudio();
            return;
        }

        AddChatMessage("system", "Thinking...");
        var thinkingHost = _chatHistory.Controls.Count > 0 ? _chatHistory.Controls[_chatHistory.Controls.Count - 1] : null;

        var llmResponse = await _llmChatClient.SendAsync(_document, input);
        if (thinkingHost is not null)
        {
            _chatHistory.Controls.Remove(thinkingHost);
            thinkingHost.Dispose();
        }

        AddChatMessage(llmResponse.IsSuccess ? "system" : "error", llmResponse.Reply);
        Log(llmResponse.Reply);

        if (llmResponse.IsSuccess && !string.IsNullOrWhiteSpace(llmResponse.Command))
        {
            var commandResult = _chatProcessor.Execute(_document, llmResponse.Command, SelectedBody);
            AddChatMessage(commandResult.IsSuccess ? "system" : "error", $"Executed command: {llmResponse.Command}");
            Log($"Executed command: {llmResponse.Command}");

            if (commandResult.SelectedFeature is not null)
            {
                _selectedNode = commandResult.SelectedFeature;
            }
            else if (commandResult.SelectedBody is not null)
            {
                _selectedNode = commandResult.SelectedBody;
            }
        }

        RefreshStudio();
    }

    private void HandleViewportEntitySelected(object? node)
    {
        _selectedNode = node ?? _document.Scene;
        RefreshSelectionViews();
        SelectTreeNodeForCurrentSelection();
    }

    private void ConfigureViewportContextMenu()
    {
        _viewportContextMenu.ShowImageMargin = true;
        _viewportContextMenu.ShowCheckMargin = false;
        _viewportContextMenu.BackColor = StudioTheme.SurfaceRaised;
        _viewportContextMenu.ForeColor = StudioTheme.TextPrimary;
        _viewportContextMenu.Font = new Font("Segoe UI", 9f);

        _viewportContextMenu.Items.Clear();
        _viewportContextMenu.Items.Add(CreatePrimitiveContextMenuItem("box", "Add Box", (_, _) => AddBody(CreateCubeBody())));
        _viewportContextMenu.Items.Add(CreatePrimitiveContextMenuItem("sphere", "Add Sphere", (_, _) => AddBody(CreateSphereBody())));
        _viewportContextMenu.Items.Add(CreatePrimitiveContextMenuItem("cylinder", "Add Cylinder", (_, _) => AddBody(CreateCylinderBody())));
        _viewportContextMenu.Items.Add(CreatePrimitiveContextMenuItem("cone", "Add Cone", (_, _) => AddBody(CreateConeBody())));
        _viewportContextMenu.Items.Add(CreatePrimitiveContextMenuItem("torus", "Add Torus", (_, _) => AddBody(CreateTorusBody())));
        _viewportContextMenu.Items.Add(CreatePrimitiveContextMenuItem("pyramid", "Add Pyramid", (_, _) => AddBody(CreatePyramidBody())));
    }

    private void HandleViewportMouseDownForContextMenu(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        _viewportRightDownPoint = e.Location;
        _viewportContextCandidate = true;
    }

    private void HandleViewportMouseMoveForContextMenu(object? sender, MouseEventArgs e)
    {
        if (!_viewportContextCandidate || e.Button != MouseButtons.Right)
        {
            return;
        }

        var movedX = Math.Abs(e.X - _viewportRightDownPoint.X);
        var movedY = Math.Abs(e.Y - _viewportRightDownPoint.Y);
        if (movedX > 4 || movedY > 4)
        {
            _viewportContextCandidate = false;
        }
    }

    private void HandleViewportMouseUpForContextMenu(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var shouldOpenContextMenu = _viewportContextCandidate;
        _viewportContextCandidate = false;
        if (!shouldOpenContextMenu)
        {
            return;
        }

        var hitNode = _viewport.PeekEntityAt(e.Location);
        if (hitNode is not null)
        {
            return;
        }

        _viewportContextMenu.Show(_viewport, e.Location);
    }

    private void HandleStudioKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelectedEntity();
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.F && e.Control)
        {
            FocusSelectedEntity();
            e.Handled = true;
        }
    }

    private void RefreshStudio()
    {
        _document.Scene.EnsureReferencePlanes();
        _projectTitle.Text = $"{_document.Name} / main.kcl";
        _viewport.Document = _document;
        RefreshFeatureTree();
        RefreshSelectionViews();
        _codeText.Text = BuildCodeEditorText();
        RefreshProjectFiles();
        _viewport.Invalidate();
    }

    private string BuildCodeEditorText()
    {
        return "// Solid-first CAD project\r\n" +
               $"// Project: {_document.Name}\r\n" +
               $"// Scene: {_document.Scene.Name}\r\n\r\n" +
               CadDesignTextExporter.Export(_document);
    }

    private void RefreshProjectFiles()
    {
        _projectFilesList.Items.Clear();
        _projectFilesList.Items.Add("blank.kcl");
        _projectFilesList.Items.Add("main.kcl");

        foreach (var body in _document.Scene.Bodies)
        {
            _projectFilesList.Items.Add($"{body.Name.ToLowerInvariant()}.kcl");
        }
    }

    private void RefreshFeatureTree()
    {
        _featureTree.BeginUpdate();
        _featureTree.Nodes.Clear();

        var sceneNode = new TreeNode("Scene") { Tag = _document.Scene };
        var bodiesNode = new TreeNode("Bodies");

        foreach (var body in _document.Scene.Bodies)
        {
            var bodyNode = new TreeNode(body.Name) { Tag = body };

            foreach (var feature in body.Features)
            {
                var node = new TreeNode(feature.Name)
                {
                    Tag = feature
                };

                if (feature.Role == CadFeatureRole.Placeholder)
                {
                    node.ForeColor = Color.FromArgb(122, 130, 141);
                }

                bodyNode.Nodes.Add(node);
            }

            bodiesNode.Nodes.Add(bodyNode);
        }

        sceneNode.Nodes.Add(bodiesNode);

        var planesNode = new TreeNode("Reference Planes");
        foreach (var plane in _document.Scene.ReferencePlanes.OrderBy(plane => plane.Kind))
        {
            planesNode.Nodes.Add(new TreeNode(plane.Name) { Tag = plane });
        }

        sceneNode.Nodes.Add(planesNode);
        sceneNode.ExpandAll();
        _featureTree.Nodes.Add(sceneNode);

        SelectTreeNodeForCurrentSelection();
        _featureTree.EndUpdate();
    }

    private void SelectTreeNodeForCurrentSelection()
    {
        TreeNode? FindNode(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (ReferenceEquals(node.Tag, _selectedNode))
                {
                    return node;
                }

                var found = FindNode(node.Nodes);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        var target = FindNode(_featureTree.Nodes);
        if (target is not null)
        {
            _featureTree.SelectedNode = target;
        }
    }

    private void RefreshSelectionViews()
    {
        _updatingUi = true;

        var body = SelectedBody;
        var feature = SelectedFeature;
        var plane = SelectedPlane;

        _selectionSummary.Text = _selectedNode switch
        {
            CadFeature selectedFeature => $"Selected: {selectedFeature.Name} / {selectedFeature.FeatureType}",
            CadBody selectedBody => $"Selected Body: {selectedBody.Name}",
            CadReferencePlane selectedReferencePlane => $"Selected Plane: {selectedReferencePlane.Name}",
            CadScene => $"Mode: {_document.ActiveMode}",
            _ => $"Mode: {_document.ActiveMode}"
        };

        _nameText.Enabled = _selectedNode is CadBody or CadFeature;
        _typeText.Enabled = false;

        _nameText.Text = feature?.Name
            ?? body?.Name
            ?? plane?.Name
            ?? (_selectedNode as CadScene)?.Name
            ?? string.Empty;

        _typeText.Text = feature?.FeatureType
            ?? (_selectedNode is CadBody ? "Body" :
                _selectedNode is CadReferencePlane selectedPlaneForType ? $"Plane {selectedPlaneForType.Kind}" :
                _selectedNode is CadScene ? "Scene" : string.Empty);

        _widthInput.Enabled = feature is BoxFeature or TorusFeature or PyramidFeature;
        _depthInput.Enabled = feature is BoxFeature or PyramidFeature;
        _heightInput.Enabled = feature is BoxFeature or CylinderFeature or ConeFeature or PyramidFeature;
        _radiusInput.Enabled = feature is CylinderFeature or SphereFeature or ConeFeature or TorusFeature;
        _xInput.Enabled = feature is MoveFeature;
        _yInput.Enabled = feature is MoveFeature;
        _zInput.Enabled = feature is MoveFeature;

        _widthInput.Value = ClampDecimal(feature switch
        {
            BoxFeature box           => box.Width,
            TorusFeature torus       => torus.MinorRadius,
            PyramidFeature pyramid   => pyramid.BaseWidth,
            _ => 0
        });
        _depthInput.Value = ClampDecimal(feature switch
        {
            BoxFeature box         => box.Depth,
            PyramidFeature pyramid => pyramid.BaseDepth,
            _ => 0
        });
        _heightInput.Value = ClampDecimal(feature switch
        {
            BoxFeature box           => box.Height,
            CylinderFeature cylinder => cylinder.Height,
            ConeFeature cone         => cone.Height,
            PyramidFeature pyramid   => pyramid.Height,
            _ => 0
        });
        _radiusInput.Value = ClampDecimal(feature switch
        {
            CylinderFeature cylinder => cylinder.Radius,
            SphereFeature sphere     => sphere.Radius,
            ConeFeature cone         => cone.Radius,
            TorusFeature torus       => torus.MajorRadius,
            _ => 0
        });
        _xInput.Value = ClampDecimal((feature as MoveFeature)?.X ?? 0);
        _yInput.Value = ClampDecimal((feature as MoveFeature)?.Y ?? 0);
        _zInput.Value = ClampDecimal((feature as MoveFeature)?.Z ?? 0);

        _viewport.SelectedNode = _selectedNode;
        _updatingUi = false;
    }

    private void ApplyPropertyChanges()
    {
        if (_updatingUi)
        {
            return;
        }

        if (SelectedFeature is CadFeature feature)
        {
            if (!string.IsNullOrWhiteSpace(_nameText.Text))
            {
                feature.Name = _nameText.Text.Trim();
            }

            switch (feature)
            {
                case BoxFeature box:
                    box.Width = (double)_widthInput.Value;
                    box.Depth = (double)_depthInput.Value;
                    box.Height = (double)_heightInput.Value;
                    break;
                case CylinderFeature cylinder:
                    cylinder.Radius = (double)_radiusInput.Value;
                    cylinder.Height = (double)_heightInput.Value;
                    break;
                case SphereFeature sphere:
                    sphere.Radius = (double)_radiusInput.Value;
                    break;
                case ConeFeature cone:
                    cone.Radius = (double)_radiusInput.Value;
                    cone.Height = (double)_heightInput.Value;
                    break;
                case TorusFeature torus:
                    torus.MajorRadius = (double)_radiusInput.Value;
                    torus.MinorRadius = (double)_widthInput.Value;
                    break;
                case PyramidFeature pyramid:
                    pyramid.BaseWidth = (double)_widthInput.Value;
                    pyramid.BaseDepth = (double)_depthInput.Value;
                    pyramid.Height    = (double)_heightInput.Value;
                    break;
                case MoveFeature move:
                    move.X = (double)_xInput.Value;
                    move.Y = (double)_yInput.Value;
                    move.Z = (double)_zInput.Value;
                    break;
            }
        }
        else if (SelectedBody is CadBody body && !string.IsNullOrWhiteSpace(_nameText.Text))
        {
            body.Name = _nameText.Text.Trim();
        }

        RefreshStudio();
    }

    private void AddChatMessage(string role, string text)
    {
        int panelWidth = Math.Max(280, _chatHistory.ClientSize.Width - 8);
        string prefix = role switch
        {
            "user"  => "»",
            "error" => "!",
            _       => " "
        };

        var line = new Label
        {
            AutoSize = false,
            Width = panelWidth,
            Font = new Font("Consolas", 9f),
            ForeColor = role switch
            {
                "error" => StudioTheme.Error,
                "user"  => StudioTheme.Accent,
                _       => StudioTheme.TextPrimary
            },
            BackColor = StudioTheme.SurfaceRaised,
            Text = $" {prefix}  {text}",
            Margin = new Padding(0, 0, 0, 1),
            Padding = new Padding(6, 3, 6, 3)
        };

        // Rough height calculation based on chars per line
        int charsPerLine = Math.Max(20, (panelWidth - 24) / 7);
        int estimatedLines = Math.Max(1, (text.Length / charsPerLine) + 1);
        line.Height = estimatedLines * 16 + 8;

        _chatHistory.Controls.Add(line);
        _chatHistory.ScrollControlIntoView(line);
    }

    private void Log(string message)
    {
        _logText.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private CadBody? SelectedBody =>
        _selectedNode switch
        {
            CadBody body => body,
            CadFeature feature => _document.Scene.Bodies.FirstOrDefault(body => body.Features.Contains(feature)),
            _ => null
        };

    private CadReferencePlane? SelectedPlane => _selectedNode as CadReferencePlane;

    private CadFeature? SelectedFeature => _selectedNode as CadFeature;
}
