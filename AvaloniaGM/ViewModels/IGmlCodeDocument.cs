using System.ComponentModel;

namespace AvaloniaGM.ViewModels;

public interface IGmlCodeDocument : INotifyPropertyChanged
{
    string SourceCode { get; set; }
}
