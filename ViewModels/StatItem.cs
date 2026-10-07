using System.ComponentModel;

namespace RamExpert2.ViewModels;

/// <summary>Una cifra del panel de uso (etiqueta + valor que puede cambiar cada segundo).</summary>
public sealed partial class StatItem(string label, string? toolTip = null) : INotifyPropertyChanged
{
    private string _value = "—";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; } = label;
    public string? ToolTip { get; } = toolTip;

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
                return;
            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}
