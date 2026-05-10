using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ANumericUpDown = Avalonia.Controls.NumericUpDown;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class ParameterEditDialog : AWindow
{
    private readonly List<ParameterRow> _rows = [];

    public ParameterEditDialog()
    {
        InitializeComponent();
    }

    public ParameterEditDialog(
        string title,
        string subtitle,
        IReadOnlyList<(string Name, string Key, string Value)> parameters)
        : this()
    {
        Title = title;
        TitleTextBlock.Text = title;
        SubtitleTextBlock.Text = subtitle;

        foreach (var parameter in parameters)
        {
            if (!TryParseDecimal(parameter.Value, out var value))
            {
                continue;
            }

            AddParameterRow(parameter.Name, parameter.Key, value);
        }

        if (_rows.Count == 0)
        {
            ErrorLabel.Text = "No numeric editable parameters are available for this selection.";
            ErrorLabel.IsVisible = true;
            OkButton.IsEnabled = false;
        }
    }

    private void AddParameterRow(string name, string key, decimal value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("140,*"),
            RowDefinitions = new RowDefinitions("Auto")
        };

        var label = new TextBlock
        {
            Text = name,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var input = new ANumericUpDown
        {
            Value = value,
            Minimum = -1000000m,
            Maximum = 1000000m,
            Increment = 1m,
            FormatString = "0.###",
            Margin = new Avalonia.Thickness(12, 0, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };

        Grid.SetColumn(input, 1);
        grid.Children.Add(label);
        grid.Children.Add(input);
        RowsPanel.Children.Add(grid);
        _rows.Add(new ParameterRow(key, input));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
        {
            Close(null);
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows)
        {
            var value = row.Input.Value ?? 0m;
            values[row.Key] = value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        Close(new ParameterEditDialogResult(values));
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private sealed record ParameterRow(string Key, ANumericUpDown Input);
}

public sealed record ParameterEditDialogResult(IReadOnlyDictionary<string, string> ValuesByKey);
