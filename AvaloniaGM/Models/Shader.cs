namespace AvaloniaGM.Models;

public class Shader : Resource
{
    public const string SplitMarker = "//######################_==_YOYO_SHADER_MARKER_==_######################@~";

    public string ProjectType { get; set; } = string.Empty;

    public string VertexSource { get; set; } = string.Empty;

    public string FragmentSource { get; set; } = string.Empty;

    public string CombinedSource =>
        string.IsNullOrEmpty(FragmentSource)
            ? VertexSource
            : VertexSource + "\n" + SplitMarker + "\n" + FragmentSource;
}
