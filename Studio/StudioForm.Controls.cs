using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace My3DApp.Studio;

public sealed partial class StudioForm
{
    private ToolStripMenuItem CreateMenu(string text, params (string label, EventHandler action)[] items)
    {
        var menu = new ToolStripMenuItem(text);
        foreach (var item in items)
        {
            menu.DropDownItems.Add(item.label, null, item.action);
        }

        return menu;
    }

    private static void ApplyMenuTheme(MenuStrip menu)
    {
        foreach (var menuItem in menu.Items.OfType<ToolStripMenuItem>())
        {
            menuItem.ForeColor = Color.FromArgb(232, 236, 244);
            menuItem.BackColor = StudioTheme.MenuBackground;
            menuItem.DropDown.BackColor = StudioTheme.SurfaceRaised;
            menuItem.DropDown.ForeColor = StudioTheme.TextPrimary;
            foreach (ToolStripItem dropdownItem in menuItem.DropDownItems)
            {
                dropdownItem.BackColor = StudioTheme.SurfaceRaised;
                dropdownItem.ForeColor = StudioTheme.TextPrimary;
            }
        }
    }

    private ToolStripMenuItem CreatePrimitiveContextMenuItem(string iconName, string label, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(label)
        {
            Image = SvgIconLoader.Load(IconPath(iconName), 16),
            ImageScaling = ToolStripItemImageScaling.None,
            BackColor = StudioTheme.SurfaceRaised,
            ForeColor = StudioTheme.TextPrimary
        };
        item.Click += onClick;
        return item;
    }

    private void ConfigureTooltips()
    {
        _tooltip.ShowAlways = true;
        _tooltip.AutoPopDelay = 5000;
        _tooltip.InitialDelay = 300;
        _tooltip.ReshowDelay = 80;
        _tooltip.OwnerDraw = true;
        _tooltip.Popup += HandleTooltipPopup;
        _tooltip.Draw += HandleTooltipDraw;
    }

    private void HandleTooltipPopup(object? sender, PopupEventArgs e)
    {
        var text = _tooltip.GetToolTip(e.AssociatedControl);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        var textSize = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding);
        e.ToolTipSize = new Size(textSize.Width + 12, textSize.Height + 8);
    }

    private static void HandleTooltipDraw(object? sender, DrawToolTipEventArgs e)
    {
        using var background = new SolidBrush(Color.FromArgb(38, 42, 48));
        using var borderPen = new Pen(Color.FromArgb(24, 28, 34));
        using var textBrush = new SolidBrush(Color.White);
        using var font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

        e.Graphics.FillRectangle(background, e.Bounds);
        e.Graphics.DrawRectangle(borderPen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);

        var textRect = new Rectangle(6, 4, e.Bounds.Width - 12, e.Bounds.Height - 8);
        TextRenderer.DrawText(
            e.Graphics,
            e.ToolTipText,
            font,
            textRect,
            Color.White,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
    }

    private Button CreateIconButton(string iconName, string tooltipText, EventHandler? onClick = null, bool enabled = true)
    {
        var button = new Button
        {
            Width = 32,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = StudioTheme.SurfaceRaised,
            UseVisualStyleBackColor = false,
            Enabled = enabled,
            Margin = new Padding(0),
            TabStop = false
        };
        button.FlatAppearance.BorderColor = StudioTheme.Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = StudioTheme.AccentSoft;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(195, 225, 248);

        var icon = SvgIconLoader.Load(IconPath(iconName), 18) ?? CreateFallbackIcon(18);
        button.Image = icon;
        button.ImageAlign = ContentAlignment.MiddleCenter;

        _tooltip.SetToolTip(button, tooltipText);
        if (onClick is not null)
        {
            button.Click += onClick;
        }

        return button;
    }

    private Control CreateSplitDropdownTool(
        string mainIconName,
        string mainTooltip,
        EventHandler? mainAction,
        bool mainEnabled,
        params (string iconName, string label, EventHandler? onClick, bool enabled)[] items)
    {
        var host = new Panel
        {
            Width = 48,
            Height = 32,
            Margin = new Padding(0, 0, 0, 0),
            BackColor = Color.Transparent
        };

        var mainButton = CreateIconButton(mainIconName, mainTooltip, mainAction, mainEnabled);
        mainButton.Dock = DockStyle.Left;
        mainButton.Width = 32;
        mainButton.TabStop = true;

        var arrowButton = CreateIconButton("chevron-down", $"More {mainTooltip}", null, true);
        arrowButton.Dock = DockStyle.Fill;
        arrowButton.Width = 16;
        arrowButton.TabStop = true;
        arrowButton.FlatAppearance.BorderColor = StudioTheme.Border;
        arrowButton.FlatAppearance.BorderSize = 1;

        var menu = CreateToolbarDropdownMenu(items);

        void OpenMenu()
        {
            if (menu.Items.Count == 0)
            {
                return;
            }

            menu.Show(host, new Point(0, host.Height));
        }

        arrowButton.Click += (_, _) => OpenMenu();
        arrowButton.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Down or Keys.Enter or Keys.Space)
            {
                OpenMenu();
                e.Handled = true;
            }
        };

        host.Controls.Add(arrowButton);
        host.Controls.Add(mainButton);
        return host;
    }

    private ContextMenuStrip CreateToolbarDropdownMenu(
        params (string iconName, string label, EventHandler? onClick, bool enabled)[] items)
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = true,
            ShowCheckMargin = false,
            BackColor = Color.White,
            ForeColor = StudioTheme.TextPrimary,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Renderer = new ToolStripProfessionalRenderer(new ToolbarDropDownColorTable()),
            MaximumSize = new Size(260, 240)
        };

        foreach (var item in items)
        {
            var menuItem = new ToolStripMenuItem(item.label)
            {
                Image = SvgIconLoader.Load(IconPath(item.iconName), 16),
                ImageScaling = ToolStripItemImageScaling.None,
                Enabled = item.enabled,
                BackColor = Color.White,
                ForeColor = StudioTheme.TextPrimary
            };

            if (item.onClick is not null)
            {
                menuItem.Click += item.onClick;
            }

            menu.Items.Add(menuItem);
        }

        return menu;
    }

    private static Image CreateFallbackIcon(int size)
    {
        var image = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.Transparent);
        using var pen = new Pen(Color.FromArgb(118, 126, 137), 1.6f);
        graphics.DrawRectangle(pen, 2.5f, 2.5f, size - 6f, size - 6f);
        graphics.DrawLine(pen, 4f, size - 4f, size - 4f, 4f);
        return image;
    }

    private static Panel CreateToolbarGroup(params Control[] buttons)
    {
        var totalButtonWidth = 0;
        foreach (var button in buttons)
        {
            totalButtonWidth += button.Width + button.Margin.Horizontal;
        }

        var groupWidth = Math.Max(totalButtonWidth + 2, 32);

        var buttonRow = new FlowLayoutPanel
        {
            Location = new Point(0, 0),
            Width = groupWidth,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        foreach (var button in buttons)
        {
            buttonRow.Controls.Add(button);
        }

        var group = new Panel
        {
            Width = groupWidth,
            Height = 32,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 4, 0)
        };
        group.Controls.Add(buttonRow);
        return group;
    }

    private static Control CreateGroupSeparator()
    {
        return new Panel
        {
            Width = 1,
            Height = 24,
            BackColor = Color.FromArgb(200, 204, 210),
            Margin = new Padding(6, 4, 6, 4)
        };
    }

    private static Control CreateToolbarSeparator()
    {
        return new Panel
        {
            Width = 1,
            Height = 22,
            BackColor = StudioTheme.Border,
            Margin = new Padding(4, 5, 4, 5)
        };
    }

    private static Button CreateHeaderButton(string text, int width, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            BackColor = primary ? StudioTheme.AccentSoft : StudioTheme.SurfaceRaised,
            ForeColor = primary ? StudioTheme.AccentDeep : StudioTheme.TextSecondary,
            Margin = new Padding(0, 0, 6, 0),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? StudioTheme.Accent : StudioTheme.Border;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static Control CreateModeChip(string text, bool active, int width = 112)
    {
        return new Label
        {
            Text = text,
            Width = width,
            Height = 24,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = active ? StudioTheme.Accent : StudioTheme.SurfaceRaised,
            ForeColor = active ? Color.White : StudioTheme.TextMuted,
            Margin = new Padding(0, 0, 4, 0),
            BorderStyle = active ? BorderStyle.None : BorderStyle.FixedSingle,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private static NumericUpDown CreateNumberInput()
    {
        return new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = -10000,
            Maximum = 10000,
            Increment = 1,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 9f),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static void AddRow(TableLayoutPanel layout, string labelText, Control control)
    {
        var rowIndex = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = StudioTheme.TextMuted,
            Margin = new Padding(0, 7, 6, 0)
        };

        control.Margin = new Padding(0, 3, 0, 2);
        layout.Controls.Add(label, 0, rowIndex);
        layout.Controls.Add(control, 1, rowIndex);
    }

    private static decimal ClampDecimal(double value) =>
        (decimal)Math.Clamp(value, -10000, 10000);

    private sealed class ToolbarDropDownColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(204, 208, 216);
        public override Color MenuItemBorder => Color.FromArgb(204, 208, 216);
        public override Color MenuItemSelected => Color.FromArgb(233, 241, 250);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(233, 241, 250);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(233, 241, 250);
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color ImageMarginGradientBegin => Color.White;
        public override Color ImageMarginGradientMiddle => Color.White;
        public override Color ImageMarginGradientEnd => Color.White;
    }
}
