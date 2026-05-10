using Avalonia.Media;

namespace My3DApp.AvaloniaApp.ViewModels;

public class AiMessage : ViewModelBase
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public IBrush Background => Role == "You"
        ? new SolidColorBrush(Color.Parse("#1A2A3A"))
        : new SolidColorBrush(Color.Parse("#0D1B2A"));

    public IBrush RoleColor => Role == "You"
        ? new SolidColorBrush(Color.Parse("#4A9EFF"))
        : new SolidColorBrush(Color.Parse("#A78BFA"));
}
