using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ZenIT.Core.Configuration;
using ZenIT.Core.Localization;
using ZenIT.Core.Workflows;

namespace ZenIT.Mac.App;

public partial class App : Application
{
    private static readonly object LogLock = new();
    private FileStream? _singleInstanceLock;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!TryAcquireSingleInstance())
            {
                desktop.Shutdown(0);
                return;
            }

            try
            {
                ValidateStartupPrerequisites();
                desktop.MainWindow = new MainWindow();
                desktop.Exit += (_, _) => ReleaseSingleInstance();
            }
            catch (Exception exception)
            {
                LogStartupException("Startup", exception);
                ReleaseSingleInstance();
                desktop.Shutdown(-1);
                return;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ValidateStartupPrerequisites()
    {
        _ = new AppSettingsService().LoadOrCreate();
        _ = LocalizedStrings.Resources;
        var workflowIssues = WorkflowIntegrityValidator.Validate();
        if (workflowIssues.Count > 0)
        {
            throw new InvalidOperationException($"Workflow registry failed startup validation: {string.Join("; ", workflowIssues)}");
        }
    }

    private bool TryAcquireSingleInstance()
    {
        try
        {
            Directory.CreateDirectory(ZenITPaths.Root);
            var lockPath = Path.Combine(ZenITPaths.Root, "zenit.lock");
            _singleInstanceLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (Exception exception)
        {
            // If the lock cannot be created for any other reason, prefer starting the app over blocking it.
            LogStartupException("SingleInstance", exception);
            return true;
        }
    }

    private void ReleaseSingleInstance()
    {
        try
        {
            _singleInstanceLock?.Dispose();
            _singleInstanceLock = null;
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogStartupException("AppDomain.CurrentDomain.UnhandledException", exception);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogStartupException("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void LogStartupException(string source, Exception exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine("============================================================");
        builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Exception Source: {source}");
        AppendException(builder, exception, 0);
        builder.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
        builder.AppendLine($"Executable Path: {Environment.ProcessPath}");
        builder.AppendLine($"Config Path: {ZenITPaths.SettingsPath}");
        WriteStartupCrashText(builder.ToString());
    }

    private static void AppendException(StringBuilder builder, Exception exception, int depth)
    {
        var prefix = new string(' ', depth * 2);
        builder.AppendLine($"{prefix}Exception Type: {exception.GetType().FullName}");
        builder.AppendLine($"{prefix}Message: {exception.Message}");
        builder.AppendLine($"{prefix}StackTrace: {exception.StackTrace}");

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

    private static void WriteStartupCrashText(string text)
    {
        try
        {
            Directory.CreateDirectory(ZenITPaths.LogsDirectory);
            lock (LogLock)
            {
                File.AppendAllText(Path.Combine(ZenITPaths.LogsDirectory, "StartupCrash.log"), text);
            }
        }
        catch
        {
            // Crash logging must never crash the app.
        }
    }
}
