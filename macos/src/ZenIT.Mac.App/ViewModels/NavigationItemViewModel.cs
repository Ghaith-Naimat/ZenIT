namespace ZenIT.Mac.App.ViewModels;

public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _title;

    public NavigationItemViewModel(string key, string title, string iconText)
    {
        Key = key;
        _title = title;
        IconText = iconText;
    }

    public string Key { get; }
    public string IconText { get; }
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
