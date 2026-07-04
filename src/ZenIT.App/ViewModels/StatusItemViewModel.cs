namespace ZenIT.App.ViewModels;

public sealed class StatusItemViewModel
{
    public StatusItemViewModel(string label, string value, string status, string brush)
    {
        Label = label;
        Value = value;
        Status = status;
        Brush = brush;
    }

    public string Label { get; }
    public string Value { get; }
    public string Status { get; }
    public string Brush { get; }
}
