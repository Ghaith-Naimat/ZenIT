namespace ZenIT.Mac.App.ViewModels;

public sealed class HealthMetricViewModel
{
    public HealthMetricViewModel(string label, string value, string helperText)
    {
        Label = label;
        Value = value;
        HelperText = helperText;
    }

    public string Label { get; }
    public string Value { get; }
    public string HelperText { get; }
}
