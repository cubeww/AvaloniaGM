using System;
using AvaloniaGM.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaGM.ViewModels;

public partial class ScriptEditorViewModel : ObservableObject, IGmlCodeDocument
{
    private readonly Script _script;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineCount))]
    [NotifyPropertyChangedFor(nameof(CharacterCount))]
    private string sourceCode;

    public string Name => _script.Name;

    public int LineCount => string.IsNullOrEmpty(SourceCode)
        ? 1
        : SourceCode.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length;

    public int CharacterCount => SourceCode.Length;

    public ScriptEditorViewModel(Script script)
    {
        _script = script;
        sourceCode = script.SourceCode;
    }

    partial void OnSourceCodeChanged(string value)
    {
        _script.SourceCode = value;
    }
}
