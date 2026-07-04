using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ZenIT.Mac.App.ViewModels;

namespace ZenIT.Mac.App;

public partial class MainWindow : Window
{
    private bool _isSynchronizingPasswordFields;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        viewModel.PasswordFieldsCleared += OnPasswordFieldsCleared;
        viewModel.ClipboardSetter = text => Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;

        UnlockPasswordBox.TextChanged += OnUnlockPasswordChanged;
        AddHandler(PointerMovedEvent, OnUserActivity, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnUserActivity, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnUserActivity, RoutingStrategies.Tunnel);
    }

    private void OnUnlockPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isSynchronizingPasswordFields)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetItModePasswordValue("Unlock", UnlockPasswordBox.Text ?? string.Empty);
        }
    }

    private void OnUserActivity(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.NotifyUserActivity();
        }
    }

    private void OnPasswordFieldsCleared(object? sender, EventArgs e)
    {
        _isSynchronizingPasswordFields = true;
        try
        {
            UnlockPasswordBox.Text = string.Empty;
            ShowUnlockPasswordCheckBox.IsChecked = false;
        }
        finally
        {
            _isSynchronizingPasswordFields = false;
        }
    }
}
