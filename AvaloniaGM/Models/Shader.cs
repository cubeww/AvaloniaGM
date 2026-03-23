namespace AvaloniaGM.Models;

public class Shader : Resource
{
    public const string SplitMarker = "//######################_==_YOYO_SHADER_MARKER_==_######################@~";
    public const string DefaultProjectType = "GLSLES";
    public const string DefaultVertexSource = """
//
// Simple passthrough vertex shader
//
attribute vec3 in_Position;                  // (x,y,z)
//attribute vec3 in_Normal;                  // (x,y,z)     unused in this shader.
attribute vec4 in_Colour;                    // (r,g,b,a)
attribute vec2 in_TextureCoord;              // (u,v)

varying vec2 v_vTexcoord;
varying vec4 v_vColour;

void main()
{
    vec4 object_space_pos = vec4( in_Position.x, in_Position.y, in_Position.z, 1.0);
    gl_Position = gm_Matrices[MATRIX_WORLD_VIEW_PROJECTION] * object_space_pos;

    v_vColour = in_Colour;
    v_vTexcoord = in_TextureCoord;
}
""";
    public const string DefaultFragmentSource = """
//
// Simple passthrough fragment shader
//
varying vec2 v_vTexcoord;
varying vec4 v_vColour;

void main()
{
    gl_FragColor = v_vColour * texture2D( gm_BaseTexture, v_vTexcoord );
}
""";

    public string ProjectType { get; set; } = DefaultProjectType;

    public string VertexSource { get; set; } = DefaultVertexSource;

    public string FragmentSource { get; set; } = DefaultFragmentSource;

    public string CombinedSource =>
        string.IsNullOrEmpty(FragmentSource)
            ? VertexSource
            : VertexSource + "\n" + SplitMarker + "\n" + FragmentSource;
}
