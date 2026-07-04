using System.Windows;
using System.Windows.Controls;
using ZenIT.App.ViewModels;

namespace ZenIT.App;

public partial class MainWindow : Window
{
    private bool _isSynchronizingPasswordFields;

    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PasswordFieldsCleared += OnPasswordFieldsCleared;
        }

        PreviewMouseMove += OnUserActivity;
        PreviewMouseDown += OnUserActivity;
        PreviewKeyDown += OnUserActivity;
    }

    private void ItPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingPasswordFields)
        {
            return;
        }

        if (DataContext is not MainViewModel viewModel ||
            sender is not PasswordBox passwordBox ||
            passwordBox.Tag is not string fieldName)
        {
            return;
        }

        viewModel.SetItModePasswordValue(fieldName, passwordBox.Password);
        SyncVisiblePassword(passwordBox.Password);
    }

    private void OnUserActivity(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.NotifyUserActivity();
        }
    }

    private void ItVisiblePasswordBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizingPasswordFields)
        {
            return;
        }

        if (DataContext is not MainViewModel viewModel ||
            sender is not TextBox textBox ||
            textBox.Tag is not string fieldName)
        {
            return;
        }

        viewModel.SetItModePasswordValue(fieldName, textBox.Text);
        SyncHiddenPassword(textBox.Text);
    }

    private void ShowUnlockPasswordCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        SyncVisiblePassword(UnlockPasswordBox.Password);
        UnlockPasswordBox.Visibility = Visibility.Collapsed;
        UnlockPasswordVisibleBox.Visibility = Visibility.Visible;
        UnlockPasswordVisibleBox.Focus();
        UnlockPasswordVisibleBox.CaretIndex = UnlockPasswordVisibleBox.Text.Length;
    }

    private void ShowUnlockPasswordCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        SyncHiddenPassword(UnlockPasswordVisibleBox.Text);
        UnlockPasswordVisibleBox.Visibility = Visibility.Collapsed;
        UnlockPasswordBox.Visibility = Visibility.Visible;
        UnlockPasswordBox.Focus();
    }

    private void OnPasswordFieldsCleared(object? sender, EventArgs e)
    {
        _isSynchronizingPasswordFields = true;
        try
        {
            UnlockPasswordBox.Clear();
            UnlockPasswordVisibleBox.Clear();
            ShowUnlockPasswordCheckBox.IsChecked = false;
            UnlockPasswordVisibleBox.Visibility = Visibility.Collapsed;
            UnlockPasswordBox.Visibility = Visibility.Visible;
        }
        finally
        {
            _isSynchronizingPasswordFields = false;
        }
    }

    private void SyncVisiblePassword(string password)
    {
        _isSynchronizingPasswordFields = true;
        try
        {
            UnlockPasswordVisibleBox.Text = password;
        }
        finally
        {
            _isSynchronizingPasswordFields = false;
        }
    }

    private void SyncHiddenPassword(string password)
    {
        _isSynchronizingPasswordFields = true;
        try
        {
            UnlockPasswordBox.Password = password;
        }
        finally
        {
            _isSynchronizingPasswordFields = false;
        }
    }
}
