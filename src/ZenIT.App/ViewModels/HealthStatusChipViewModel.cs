namespace ZenIT.App.ViewModels;

public sealed class HealthStatusChipViewModel
{
    public HealthStatusChipViewModel(string label, string status, string brush)
    {
        Label = label;
        Status = status;
        Brush = brush;
    }

    public string Label { get; }
    public string Status { get; }
    public string Brush { get; }
}
