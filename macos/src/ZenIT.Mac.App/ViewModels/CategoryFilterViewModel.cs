namespace ZenIT.Mac.App.ViewModels;

public sealed class CategoryFilterViewModel : ObservableObject
{
    private bool _isSelected;
    private string _name;

    public CategoryFilterViewModel(string name, bool isSelected = false)
    {
        _name = name;
        _isSelected = isSelected;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
