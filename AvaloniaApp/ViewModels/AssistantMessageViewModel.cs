namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class AssistantMessageViewModel
{
    public AssistantMessageViewModel(string role, string content, DateTimeOffset timestamp)
    {
        Role = string.IsNullOrWhiteSpace(role) ? "system" : role.Trim().ToLowerInvariant();
        Content = content;
        Timestamp = timestamp;
    }

    public string Role { get; }

    public string Content { get; }

    public DateTimeOffset Timestamp { get; }

    public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm");

    public string RoleBadge => Role switch
    {
        "user" => "YOU",
        "assistant" => "AI",
        _ => "SYS"
    };
}
