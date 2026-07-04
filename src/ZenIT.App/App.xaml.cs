using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using ZenIT.Core.Configuration;
using ZenIT.Core.Localization;
using ZenIT.Core.Workflows;

namespace ZenIT.App;

public partial class App : Application
{
    private const string MainWindowTitle = "ZenIT | Self-Service IT Assistant";
    private static readonly object LogLock = new();
    private static volatile bool MainWindowShown;
    private Mutex? _singleInstanceMutex;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!TryAcquireSingleInstance())
        {
            TryBringExistingWindowToFront();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
            Environment.Exit(0);
            return;
        }

        base.OnStartup(e);

        MainWindow? mainWindow = null;
        try
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            ValidateStartupPrerequisites();

            mainWindow = MainWindow as MainWindow ?? new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            MainWindowShown = true;
        }
        catch (Exception exception)
        {
            LogStartupException("Startup", exception, mainWindow);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch
        {
            // Best-effort cleanup only.
        }

        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogStartupException("DispatcherUnhandledException", e.Exception, Current?.MainWindow);
        e.Handled = true;

        if (!IsMainWindowRunning())
        {
            Current?.Shutdown(-1);
        }
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogStartupException("AppDomain.CurrentDomain.UnhandledException", exception, Current?.MainWindow);
        }
        else
        {
            WriteStartupCrashLine($"[{DateTimeOffset.Now:O}] AppDomain.CurrentDomain.UnhandledException: {e.ExceptionObject}");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogStartupException("TaskScheduler.UnobservedTaskException", e.Exception, Current?.MainWindow);
        e.SetObserved();
    }

    private static void ValidateStartupPrerequisites()
    {
        var requiredAssets = new[]
        {
            "Assets/logo-display.png",
            "Assets/logo.png",
            "Assets/ZenIT.ico"
        };

        foreach (var asset in requiredAssets)
        {
            if (!PackagedResourceExists(asset))
            {
                throw new FileNotFoundException($"Missing startup asset: {asset}", asset);
            }
        }

        RequireResource("ZenTealBrush", typeof(Brush));
        RequireResource("ZenNavyBrush", typeof(Brush));
        RequireResource("ZenCardBrush", typeof(Brush));
        RequireResource("ZenStrokeBrush", typeof(Brush));
        RequireResource("CardShadow", typeof(System.Windows.Media.Effects.Effect));
        RequireResource("CardHoverShadow", typeof(System.Windows.Media.Effects.Effect));
        RequireResource("ButtonShadow", typeof(System.Windows.Media.Effects.Effect));
        RequireResource("PrimaryButtonStyle", typeof(Style));

        _ = new AppSettingsService().LoadOrCreate();
        _ = LocalizedStrings.Resources;
        var workflowIssues = WorkflowIntegrityValidator.Validate();
        if (workflowIssues.Count > 0)
        {
            throw new InvalidOperationException($"Workflow registry failed startup validation: {string.Join("; ", workflowIssues)}");
        }
    }

    private static void RequireResource(string key, Type expectedType)
    {
        var value = Current.TryFindResource(key);
        if (value is null)
        {
            throw new ResourceReferenceKeyNotFoundException($"Missing startup resource: {key}", key);
        }

        if (!expectedType.IsInstanceOfType(value))
        {
            throw new InvalidOperationException($"Startup resource '{key}' expected {expectedType.FullName} but found {value.GetType().FullName}.");
        }
    }

    private static bool PackagedResourceExists(string relativePath)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
            var streamInfo = GetResourceStream(uri);
            streamInfo?.Stream.Dispose();
            return streamInfo is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void LogStartupException(string source, Exception exception, Window? loadedWindow)
    {
        var builder = new StringBuilder();
        builder.AppendLine("============================================================");
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Exception Source: {source}");
        AppendException(builder, exception, 0);
        AppendXamlDiagnostics(builder, exception);
        AppendApplicationDiagnostics(builder, loadedWindow);
        WriteStartupCrashText(builder.ToString());
    }

    private static void AppendException(StringBuilder builder, Exception exception, int depth)
    {
        var prefix = new string(' ', depth * 2);
        builder.AppendLine($"{prefix}Exception Type: {exception.GetType().FullName}");
        builder.AppendLine($"{prefix}Message: {exception.Message}");
        builder.AppendLine($"{prefix}Source: {exception.Source}");
        builder.AppendLine($"{prefix}TargetSite: {exception.TargetSite}");
        builder.AppendLine($"{prefix}StackTrace: {exception.StackTrace}");

        if (exception is XamlParseException xamlParseException)
        {
            builder.AppendLine($"{prefix}XAML Line Number: {xamlParseException.LineNumber}");
            builder.AppendLine($"{prefix}XAML Line Position: {xamlParseException.LinePosition}");
        }

        if (exception.InnerException is not null)
        {
            builder.AppendLine($"{prefix}Inner Exception:");
            AppendException(builder, exception.InnerException, depth + 1);
        }

        if (exception is AggregateException aggregateException)
        {
            var index = 0;
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                builder.AppendLine($"{prefix}Aggregate Inner [{index++}]:");
                AppendException(builder, innerException, depth + 1);
            }
        }
    }

    private static void AppendXamlDiagnostics(StringBuilder builder, Exception exception)
    {
        var fullText = exception.ToString();
        builder.AppendLine("XAML Diagnostics:");
        builder.AppendLine($"  ResourceDictionary: {DescribeResourceDictionaries()}");
        builder.AppendLine($"  Style Key: {FindKnownToken(fullText, @"Style\s+'?([^'\r\n]+)'?")}");
        builder.AppendLine($"  StaticResource: {FindKnownToken(fullText, @"StaticResource(?:Extension)?\s+'?([^'\r\n]+)'?")}");
        builder.AppendLine($"  DynamicResource: {FindKnownToken(fullText, @"DynamicResource(?:Extension)?\s+'?([^'\r\n]+)'?")}");
        builder.AppendLine($"  Control Name: {FindKnownToken(fullText, @"Name='([^']+)'")}");
        builder.AppendLine($"  DependencyProperty: {FindKnownToken(fullText, @"property '([^']+)'")}");
        builder.AppendLine($"  Binding: {FindKnownToken(fullText, @"BindingExpression:([^\r\n]+)")}");
        builder.AppendLine($"  Converter: {FindKnownToken(fullText, @"converter ([^\r\n]+)")}");
        builder.AppendLine($"  Image URI: {FindKnownToken(fullText, @"(?:Assets|pack://)[^'\""\s]+")}");
        builder.AppendLine($"  Pack URI: {FindKnownToken(fullText, @"pack://[^'\""\s]+")}");
        builder.AppendLine($"  Merged Dictionary: {DescribeMergedDictionaries()}");
    }

    private static void AppendApplicationDiagnostics(StringBuilder builder, Window? loadedWindow)
    {
        builder.AppendLine("Application Diagnostics:");
        builder.AppendLine($"  Loaded Window: {DescribeWindow(loadedWindow)}");
        builder.AppendLine($"  Loaded ResourceDictionary Keys: {DescribeResourceKeys()}");
        builder.AppendLine($"  Loaded Images: {DescribeImages(loadedWindow)}");
        builder.AppendLine($"  Loaded Styles: {DescribeStyles()}");
        builder.AppendLine($"  Loaded Controls: {DescribeControls(loadedWindow)}");
        builder.AppendLine($"  Current Culture: {CultureInfo.CurrentCulture.Name}");
        builder.AppendLine($"  Current UI Culture: {CultureInfo.CurrentUICulture.Name}");
        builder.AppendLine($"  Working Directory: {Environment.CurrentDirectory}");
        builder.AppendLine($"  Executable Path: {Environment.ProcessPath}");
        builder.AppendLine($"  Config Path: {ZenITPaths.SettingsPath}");
        builder.AppendLine($"  Theme: {ReadConfigValue("Theme")}");
        builder.AppendLine($"  Language: {ReadConfigValue("Language")}");
        builder.AppendLine($"  Current Thread: Id={Environment.CurrentManagedThreadId}; Apartment={Thread.CurrentThread.GetApartmentState()}; Name={Thread.CurrentThread.Name ?? "Unnamed"}");
        builder.AppendLine($"  Current Dispatcher: {DescribeDispatcher()}");
        builder.AppendLine($"  Windows Version: {RuntimeInformation.OSDescription}");
        builder.AppendLine($"  .NET Runtime: {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine("  Loaded Assembly Versions:");
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(assembly => assembly.GetName().Name))
        {
            var name = assembly.GetName();
            builder.AppendLine($"    {name.Name}: {name.Version}");
        }
    }

    private static string DescribeWindow(Window? window)
    {
        if (window is null)
        {
            return "None";
        }

        return $"{window.GetType().FullName}; Title={window.Title}; IsLoaded={window.IsLoaded}; IsVisible={window.IsVisible}";
    }

    private static string DescribeDispatcher()
    {
        try
        {
            var dispatcher = Current?.Dispatcher;
            return dispatcher is null
                ? "None"
                : $"ThreadId={dispatcher.Thread.ManagedThreadId}; HasShutdownStarted={dispatcher.HasShutdownStarted}; HasShutdownFinished={dispatcher.HasShutdownFinished}";
        }
        catch (Exception exception)
        {
            return $"Unavailable: {exception.Message}";
        }
    }

    private static string DescribeResourceDictionaries()
    {
        try
        {
            return $"App.Resources Keys={Current.Resources.Keys.Count}; Merged={Current.Resources.MergedDictionaries.Count}";
        }
        catch (Exception exception)
        {
            return $"Unavailable: {exception.Message}";
        }
    }

    private static string DescribeMergedDictionaries()
    {
        try
        {
            if (Current.Resources.MergedDictionaries.Count == 0)
            {
                return "None";
            }

            return string.Join("; ", Current.Resources.MergedDictionaries.Select(dictionary => dictionary.Source?.ToString() ?? "Inline"));
        }
        catch (Exception exception)
        {
            return $"Unavailable: {exception.Message}";
        }
    }

    private static string DescribeResourceKeys()
    {
        try
        {
            return string.Join(", ", Current.Resources.Keys.Cast<object>().Select(key => key.ToString()).Where(key => !string.IsNullOrWhiteSpace(key)).OrderBy(key => key));
        }
        catch (Exception exception)
        {
            return $"Unavailable: {exception.Message}";
        }
    }

    private static string DescribeStyles()
    {
        try
        {
            return string.Join(", ", Current.Resources.Cast<System.Collections.DictionaryEntry>()
                .Where(entry => entry.Value is Style)
                .Select(entry => entry.Key?.ToString() ?? "Unnamed")
                .OrderBy(key => key));
        }
        catch (Exception exception)
        {
            return $"Unavailable: {exception.Message}";
        }
    }

    private static string DescribeImages(Window? window)
    {
        if (window is null)
        {
            return "None";
        }

        try
        {
            var images = FindVisualChildren<Image>(window)
                .Select(image => image.Source?.ToString() ?? "No Source")
                .Distinct()
                .OrderBy(source => source);
            return string.Join("; ", images);
        }
        catch (Exception exception)
        {
            return $"Unavailable: {exception.Message}";
        }
    }

    private static string DescribeControls(Window? window)
    {
        if (window is null)
        {
            return "None";
        }

        try
        {
            var controls = FindVisualChildren<FrameworkElement>(window)
                .Select(element => string.IsNullOrWhiteSpace(element.Name) ? element.GetType().Name : $"{element.GetType().Name}#{element.Name}")
                .Take(250);
            return string.Join(", ", controls);
        }
        catch (Exception exception)
        {
            return $"Unavailable: {exception.Message}";
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static string FindKnownToken(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Value : "Not detected";
    }

    private static string ReadConfigValue(string propertyName)
    {
        try
        {
            if (!File.Exists(ZenITPaths.SettingsPath))
            {
                return "Config file not found";
            }

            var pattern = $"\"{Regex.Escape(propertyName)}\"\\s*:\\s*\"([^\"]*)\"";
            var match = Regex.Match(File.ReadAllText(ZenITPaths.SettingsPath), pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "Not set";
        }
        catch (Exception exception)
        {
            return $"Unavailable: {exception.Message}";
        }
    }

    private static void WriteStartupCrashText(string text)
    {
        if (TryAppendLogText(Path.Combine(ZenITPaths.LogsDirectory, "StartupCrash.log"), text))
        {
            return;
        }

        var fallbackPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZenIT",
            "Logs",
            "StartupCrash.log");
        TryAppendLogText(fallbackPath, text);
    }

    private static void WriteStartupCrashLine(string line)
    {
        WriteStartupCrashText(line + Environment.NewLine);
    }

    private static bool TryAppendLogText(string path, string text)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (LogLock)
            {
                File.AppendAllText(path, text);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMainWindowRunning()
    {
        try
        {
            return MainWindowShown ||
                   Current?.MainWindow is { IsLoaded: true } ||
                   Current?.MainWindow is { IsVisible: true };
        }
        catch
        {
            return MainWindowShown;
        }
    }

    private bool TryAcquireSingleInstance()
    {
        var userId = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var mutexName = $@"Local\ZenIT-{SanitizeMutexName(userId)}";
        _singleInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        return createdNew;
    }

    private static string SanitizeMutexName(string value)
    {
        return string.Concat(value.Select(character => char.IsLetterOrDigit(character) ? character : '-'));
    }

    private static void TryBringExistingWindowToFront()
    {
        try
        {
            var handle = FindWindow(null, MainWindowTitle);
            if (handle == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(handle, 9);
            SetForegroundWindow(handle);
        }
        catch (Exception exception)
        {
            LogStartupException("DuplicateInstanceActivation", exception, Current?.MainWindow);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
