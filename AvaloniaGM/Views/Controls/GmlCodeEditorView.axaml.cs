using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaGM.ViewModels;

namespace AvaloniaGM.Views.Controls
{
    public partial class GmlCodeEditorView : UserControl
    {
        private IGmlCodeDocument? _document;
        private bool _isSynchronizing;

        public GmlCodeEditorView()
        {
            InitializeComponent();

            DataContextChanged += GmlCodeEditorView_OnDataContextChanged;
            Editor.TextChanged += Editor_OnTextChanged;
            Editor.TextArea.TextView.LineTransformers.Add(new GmlSyntaxColorizer());
        }

        private void GmlCodeEditorView_OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_document is not null)
            {
                _document.PropertyChanged -= Document_OnPropertyChanged;
            }

            _document = DataContext as IGmlCodeDocument;

            if (_document is not null)
            {
                _document.PropertyChanged += Document_OnPropertyChanged;
                SynchronizeEditorFromDocument();
            }
            else
            {
                Editor.Text = string.Empty;
                Editor.IsReadOnly = true;
            }
        }

        private void Document_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IGmlCodeDocument.SourceCode))
            {
                SynchronizeEditorFromDocument();
            }
        }

        private void SynchronizeEditorFromDocument()
        {
            if (_document is null || _isSynchronizing)
            {
                return;
            }

            Editor.IsReadOnly = false;

            if (Editor.Text == _document.SourceCode)
            {
                return;
            }

            _isSynchronizing = true;
            Editor.Text = _document.SourceCode;
            _isSynchronizing = false;
        }

        private void Editor_OnTextChanged(object? sender, System.EventArgs e)
        {
            if (_document is null || _isSynchronizing)
            {
                return;
            }

            if (_document.SourceCode == Editor.Text)
            {
                return;
            }

            _isSynchronizing = true;
            _document.SourceCode = Editor.Text ?? string.Empty;
            _isSynchronizing = false;
        }
    }
}
