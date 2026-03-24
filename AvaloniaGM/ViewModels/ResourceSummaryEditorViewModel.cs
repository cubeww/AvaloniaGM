namespace AvaloniaGM.ViewModels;

public sealed class ResourceSummaryEditorViewModel
{
    public string Header { get; }

    public string Subtitle { get; }

    public string Content { get; }

    public ResourceSummaryEditorViewModel(string header, string subtitle, string content)
    {
        Header = header;
        Subtitle = subtitle;
        Content = content;
    }
}
