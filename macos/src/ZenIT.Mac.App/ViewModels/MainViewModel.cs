using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ZenIT.Core.Configuration;
using ZenIT.Core.Execution;
using ZenIT.Core.Localization;
using ZenIT.Core.Logging;
using ZenIT.Core.Maintenance;
using ZenIT.Core.Models;
using ZenIT.Core.Security;
using ZenIT.Core.Services;
using ZenIT.Core.Workflows;

namespace ZenIT.Mac.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly DeviceHealthService _deviceHealthService;
    private readonly LogService _logService;
    private readonly ProcessRunner _processRunner;
    private readonly AppSettingsService _settingsService;
    private readonly HealthGuardianService _healthGuardianService;
    private readonly DispatcherTimer _itModeSessionTimer;
    private AppSettings _settings;
    private readonly ITPolicy _itPolicy;
    private DeviceStatus _deviceStatus;
    private NavigationItemViewModel _selectedNavigationItem;
    private string _healthCheckStatus = string.Empty;
    private string _healthCheckStatusBrush = "#6B7A8A";
    private int _deviceHealthScore;
    private string _deviceHealthScoreStatus = "Not checked";
    private string _deviceHealthScoreBrush = "#6B7A8A";
    private string _logsStatus = string.Empty;
    private string _fallbackLogPathText = string.Empty;
    private string _quickFixSearchText = string.Empty;
    private string _selectedWorkflowCategory = "All";
    private string _selectedITWorkflowCategory = "All";
    private string _logsSearchText = string.Empty;
    private string _selectedLogFilter = "All";
    private string _selectedLogSort = "Newest First";
    private string _lastHealthCheckText = string.Empty;
    private string _lastSupportPackageText = string.Empty;
    private string _supportStatus = string.Empty;
    private string _toastMessage = string.Empty;
    private string _toastBrush = "#20B486";
    private bool _isItModeUnlocked;
    private bool _isConfirmationVisible;
    private TaskCompletionSource<bool>? _confirmationCompletion;
    private WorkflowCardViewModel? _pendingConfirmationWorkflow;
    private string _itModeStatus = string.Empty;
    private string _itModeStatusBrush = "#6B7A8A";
    private string _itModeUsernameAttempt = string.Empty;
    private string _signedInITUsername = string.Empty;
    private string _unlockPassword = string.Empty;
    private bool _isAuthenticatingITMode;
    private string _language = "en";

    public event EventHandler? PasswordFieldsCleared;

    /// <summary>
    /// Set by the view so the view model can copy text without referencing a window directly.
    /// </summary>
    public Func<string, Task>? ClipboardSetter { get; set; }

    public MainViewModel()
        : this(new DeviceHealthService(), new LogService(), new ProcessRunner(), new AppSettingsService())
    {
    }

    public MainViewModel(
        DeviceHealthService deviceHealthService,
        LogService logService,
        ProcessRunner processRunner,
        AppSettingsService settingsService)
        : this(deviceHealthService, logService, processRunner, settingsService, settingsService.LoadOrCreate())
    {
    }

    public MainViewModel(
        DeviceHealthService deviceHealthService,
        LogService logService,
        ProcessRunner processRunner,
        AppSettingsService settingsService,
        AppSettings settings)
        : this(
            deviceHealthService,
            new LocalWorkflowExecutor(processRunner, deviceHealthService, logService, settings, new ITPolicyService().Load()),
            logService,
            processRunner,
            settings,
            settingsService)
    {
    }

    public MainViewModel(
        DeviceHealthService deviceHealthService,
        IWorkflowExecutor workflowExecutor,
        LogService logService,
        ProcessRunner? processRunner = null,
        AppSettings? settings = null,
        AppSettingsService? settingsService = null)
    {
        _deviceHealthService = deviceHealthService;
        _logService = logService;
        _processRunner = processRunner ?? new ProcessRunner();
        _settingsService = settingsService ?? new AppSettingsService();
        _healthGuardianService = new HealthGuardianService(_logService);
        _settings = settings ?? new AppSettings();
        _itPolicy = new ITPolicyService().Load();
        _itModeSessionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(Math.Clamp(_itPolicy.ITModeSessionTimeoutMinutes, 5, 240))
        };
        _itModeSessionTimer.Tick += OnITModeSessionTimeout;
        _language = _settings.Language;
        HealthMetrics = [];
        HealthStatusChips = [];
        ConnectivityHealthItems = [];
        SecurityHealthItems = [];
        PerformanceHealthItems = [];
        UpdatesHealthItems = [];
        ItNetworkDiagnostics = [];
        ItSystemDiagnostics = [];
        ItEnvironmentDiagnostics = [];
        ItSummaryItems = [];
        LogRows = [];
        FilteredLogRows = [];
        RecentActivity = [];

        StartBackgroundStartupMaintenance();
        _healthGuardianService.Start();

        var health = _deviceHealthService.GetCurrentHealth();
        _deviceStatus = CreateDeviceStatus(health);
        UpdateHealthMetrics(health);
        RefreshLogs();

        Greeting = "ZenIT";
        Username = Environment.UserName;
        NavigationItems =
        [
            new("home", "Home", "HM"),
            new("quick-fixes", "Quick Fixes", "QF"),
            new("device-health", "My Device", "DV"),
            new("it-mode", "IT Mode 🔒", "🔒"),
            new("about", "About", "AB")
        ];

        _selectedNavigationItem = NavigationItems[0];
        _selectedNavigationItem.IsSelected = true;
        SelectNavigationCommand = new RelayCommand(SelectNavigation);
        RunHealthCheckCommand = new AsyncRelayCommand(RunHealthCheckAsync);
        CollectITReportCommand = new AsyncRelayCommand(CollectITReportAsync);
        ExportReportFormatCommand = new AsyncRelayCommand(ExportReportFormatAsync);
        OpenLogFolderCommand = new AsyncRelayCommand(OpenLogFolderAsync);
        OpenReportsFolderCommand = new AsyncRelayCommand(OpenReportsFolderAsync);
        RefreshLogsCommand = new RelayCommand(_ => RefreshLogs());
        CopyLatestSummaryCommand = new RelayCommand(_ => CopyLatestSummary());
        ExportLogCommand = new RelayCommand(_ => ExportLog());
        SelectWorkflowCategoryCommand = new RelayCommand(SelectWorkflowCategory);
        SelectITWorkflowCategoryCommand = new RelayCommand(SelectITWorkflowCategory);
        SelectLogFilterCommand = new RelayCommand(SelectLogFilter);
        SelectLogSortCommand = new RelayCommand(SelectLogSort);
        UnlockITModeCommand = new AsyncRelayCommand(UnlockITModeAsync, () => IsITModePolicyAvailable && !IsAuthenticatingITMode);
        LockITModeCommand = new RelayCommand(_ => LockITMode("Manual"));
        ContactITCommand = new AsyncRelayCommand(ContactITAsync);
        ConfirmRunCommand = new RelayCommand(_ => ResolveConfirmation(true));
        CancelRunCommand = new RelayCommand(_ => ResolveConfirmation(false));
        SwitchLanguageCommand = new RelayCommand(SwitchLanguage);

        var workflowExecutorInstance = workflowExecutor;
        Actions = new ObservableCollection<WorkflowCardViewModel>(
            WorkflowRegistry.EmployeeWorkflows.Select(workflow => new WorkflowCardViewModel(workflow, workflowExecutorInstance, logService, _deviceStatus, ConfirmWorkflowAsync, ShowToast)));
        ITActions = new ObservableCollection<WorkflowCardViewModel>(
            WorkflowRegistry.ITWorkflows.Select(workflow => new WorkflowCardViewModel(workflow, workflowExecutorInstance, logService, _deviceStatus, ConfirmWorkflowAsync, ShowToast)));
        HomeActions = new ObservableCollection<WorkflowCardViewModel>();
        FilteredActions = new ObservableCollection<WorkflowCardViewModel>(Actions);
        FilteredITActions = new ObservableCollection<WorkflowCardViewModel>(ITActions);
        WorkflowCategories =
        [
            new("All", true),
            new("Recommended"),
            new("Connectivity"),
            new("Performance"),
            new("Meetings"),
            new("Productivity"),
            new("Security"),
            new("My Device"),
            new("Support")
        ];
        ITWorkflowCategories =
        [
            new("All", true),
            new("Diagnostics"),
            new("macOS Repair"),
            new("Network Repair"),
            new("Services"),
            new("Performance"),
            new("Updates"),
            new("Reports")
        ];
        LogFilters =
        [
            new("All", true),
            new("Success"),
            new("Warning"),
            new("Failed"),
            new("IT Mode")
        ];
        LogSortOptions =
        [
            new("Newest First", true),
            new("Oldest First")
        ];
        ApplyLanguage();
        if (!IsITModePolicyAvailable)
        {
            SetItModeMessage(T("IT.PolicyUnavailableInstall"), "#E5484D");
        }
        UpdateHomeSummary();
    }

    public string Greeting { get; }
    public string Username { get; }

    public DeviceStatus DeviceStatus
    {
        get => _deviceStatus;
        private set => SetProperty(ref _deviceStatus, value);
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }
    public ObservableCollection<WorkflowCardViewModel> Actions { get; }
    public ObservableCollection<WorkflowCardViewModel> ITActions { get; }
    public ObservableCollection<WorkflowCardViewModel> HomeActions { get; }
    public ObservableCollection<WorkflowCardViewModel> FilteredActions { get; }
    public ObservableCollection<WorkflowCardViewModel> FilteredITActions { get; }
    public ObservableCollection<CategoryFilterViewModel> WorkflowCategories { get; }
    public ObservableCollection<CategoryFilterViewModel> ITWorkflowCategories { get; }
    public ObservableCollection<HealthMetricViewModel> HealthMetrics { get; }
    public ObservableCollection<HealthStatusChipViewModel> HealthStatusChips { get; }
    public ObservableCollection<StatusItemViewModel> ConnectivityHealthItems { get; }
    public ObservableCollection<StatusItemViewModel> SecurityHealthItems { get; }
    public ObservableCollection<StatusItemViewModel> PerformanceHealthItems { get; }
    public ObservableCollection<StatusItemViewModel> UpdatesHealthItems { get; }
    public ObservableCollection<StatusItemViewModel> ItNetworkDiagnostics { get; }
    public ObservableCollection<StatusItemViewModel> ItSystemDiagnostics { get; }
    public ObservableCollection<StatusItemViewModel> ItEnvironmentDiagnostics { get; }
    public ObservableCollection<StatusItemViewModel> ItSummaryItems { get; }
    public ObservableCollection<LogRowViewModel> LogRows { get; }
    public ObservableCollection<LogRowViewModel> FilteredLogRows { get; }
    public ObservableCollection<RecentActivityViewModel> RecentActivity { get; }
    public ObservableCollection<CategoryFilterViewModel> LogFilters { get; }
    public ObservableCollection<CategoryFilterViewModel> LogSortOptions { get; }
    public ICommand SelectNavigationCommand { get; }
    public ICommand RunHealthCheckCommand { get; }
    public ICommand CollectITReportCommand { get; }
    public ICommand ExportReportFormatCommand { get; }
    public ICommand OpenLogFolderCommand { get; }
    public ICommand OpenReportsFolderCommand { get; }
    public ICommand RefreshLogsCommand { get; }
    public ICommand CopyLatestSummaryCommand { get; }
    public ICommand ExportLogCommand { get; }
    public ICommand SelectWorkflowCategoryCommand { get; }
    public ICommand SelectITWorkflowCategoryCommand { get; }
    public ICommand SelectLogFilterCommand { get; }
    public ICommand SelectLogSortCommand { get; }
    public ICommand UnlockITModeCommand { get; }
    public ICommand LockITModeCommand { get; }
    public ICommand ContactITCommand { get; }
    public ICommand ConfirmRunCommand { get; }
    public ICommand CancelRunCommand { get; }
    public ICommand SwitchLanguageCommand { get; }
    public string AppVersion => "1.0.0";
    public string BuildDate => "2026.07.04";
    public string RuntimeVersion => RuntimeInformation.FrameworkDescription;
    public string ConfigurationMode => _settings.AppMode;
    public string UpdateChannel => _settings.UpdateChannel;
    public string AppMode => _settings.AppMode;
    public bool IsTestMode => _settings.EnableTestMode;
    public string CompanyName => _settings.CompanyName;
    public string ITSupportEmail => _settings.ITSupportEmail;
    public string SupportFooterText => T("Support.Footer");
    public string ReadyToHelpText => T("Status.Available");
    public string HeroBadgeText => T("Home.HeroBadge");
    public string HeroTitle => T("Home.HeroTitle");
    public string HeroText => T("Home.HeroText");
    public string QuickActionsTitle => T("Home.QuickActions");
    public string QuickFixQuestion => T("QuickFixes.Question");
    public string RunningText => T("Status.Running");
    public string EnglishText => T("Language.English");
    public string ArabicText => T("Language.Arabic");
    public string LanguageTooltip => T("Language.Tooltip");
    public string HomeMainActionTitle => T("Home.MainAction");
    public string HomeSupportTitle => T("Home.SupportTitle");
    public string HomeSupportText => T("Home.SupportText");
    public string RecentActivityTitle => T("Home.RecentActivity");
    public string NoRecentActivityText => T("Home.NoRecentActivity");
    public string DeviceStatusTitleText => T("Home.DeviceStatus");
    public string DeviceLabelText => T("Label.Device");
    public string UserLabelText => T("Label.User");
    public string InternetLabelText => T("Label.Internet");
    public string LastCheckLabelText => T("Label.LastCheck");
    public string LastHealthCheckLabelText => T("Label.LastHealthCheck");
    public string LastSupportPackageLabelText => T("Label.LastSupportPackage");
    public string QuickSupportLabelText => T("Label.QuickSupport");
    public string OpenITSupportInSlackText => T("Label.OpenITSupportInSlack");
    public string StatusLabelText => T("Label.Status");
    public string OpenReportsFolderText => T("Button.OpenReports");
    public string ContactITText => T("Button.ContactIT");
    public string RunHealthCheckText => T("Button.RunHealthCheck");
    public string CreateSupportPackageText => T("Button.CreateSupportPackage");
    public string RefreshText => T("Button.Refresh");
    public string OpenLogFolderText => T("Button.OpenLogFolder");
    public string CopyLatestSummaryText => T("Button.CopySummary");
    public string ExportLogText => T("Button.ExportLog");
    public string LogsIntroText => T("Logs.Intro");
    public string LogsSearchTextLabel => T("Logs.Search");
    public string LogsTimeText => T("Logs.Time");
    public string LogsWorkflowText => T("Logs.Workflow");
    public string LogsResultText => T("Logs.Result");
    public string LogsDurationText => T("Logs.Duration");
    public string LogsTimelineText => T("Logs.Timeline");
    public string DeviceTitleText => T("Device.Title");
    public string DeviceSubtitleText => T("Device.Subtitle");
    public string DeviceHealthScoreText => T("Device.HealthScore");
    public string HealthScoreTooltipText => T("Device.HealthScoreTooltip");
    public string ConnectivityText => T("Device.Connectivity");
    public string SecurityText => T("Device.Security");
    public string PerformanceText => T("Device.Performance");
    public string UpdatesText => T("Device.Updates");
    public string ITSecureAccessText => T("IT.SecureAccess");
    public string ITUnlockIntroText => T("IT.UnlockIntro");
    public string ITUnlockHelpText => T("IT.UnlockHelp");
    public string ITAdministratorAccessText => T("IT.AdministratorAccess");
    public string ITAdministratorAccessSubtitleText => T("IT.AdministratorAccessSubtitle");
    public string ITSecurityNoteText => T("IT.SecurityNote");
    public string ITUnlockTitleText => T("IT.UnlockTitle");
    public string ITUsernameText => T("IT.Username");
    public string ITPasswordText => T("IT.Password");
    public string ITShowPasswordText => T("IT.ShowPassword");
    public string ITCredentialsProtectedText => T("IT.CredentialsProtected");
    public string ITNeedAccessText => T("IT.NeedAccess");
    public string ITAuthenticatingButtonText => IsAuthenticatingITMode ? T("IT.Authenticating") : T("IT.UnlockTitle");
    public string ITAdministratorVerifiedText => T("IT.AdministratorVerified");
    public string ITWhatUnlocksText => T("IT.WhatUnlocks");
    public string ITSecurityHeadingText => T("IT.SecurityHeading");
    public string ITCapabilityAdvancedRepairsText => T("IT.Capability.AdvancedRepairs");
    public string ITCapabilityDiagnosticsText => T("IT.Capability.Diagnostics");
    public string ITCapabilityNetworkToolsText => T("IT.Capability.NetworkTools");
    public string ITCapabilityMacRepairText => T("IT.Capability.WindowsRepair");
    public string ITCapabilitySecurityChecksText => T("IT.Capability.SecurityChecks");
    public string ITCapabilitySystemLogsText => T("IT.Capability.SystemLogs");
    public string ITCapabilityReportsText => T("IT.Capability.Reports");
    public string ITCapabilityDeploymentToolsText => T("IT.Capability.DeploymentTools");
    public string ITSecurityAdminAuthText => T("IT.Security.AdminAuth");
    public string ITSecurityLoggedText => T("IT.Security.Logged");
    public string ITSecurityNoPasswordsText => T("IT.Security.NoPasswords");
    public string ITSecurityAuditableText => T("IT.Security.Auditable");
    public string ITUnlockedText => T("IT.Unlocked");
    public string ITUnlockedBodyText => T("IT.UnlockedText");
    public string ITSummaryText => T("IT.Dashboard");
    public string ITLogsText => T("IT.Logs");
    public string ITReportsText => T("IT.Reports");
    public string ITAdvancedRepairsText => T("IT.AdvancedRepairs");
    public string ITAdminToolsText => T("IT.AdminTools");
    public string ITNetworkText => T("IT.Network");
    public string ITSystemText => T("IT.System");
    public string ITNetworkDescriptionText => T("IT.NetworkDescription");
    public string ITSystemDescriptionText => T("IT.SystemDescription");
    public string ITZenHREnvironmentText => T("IT.ZenHREnvironment");
    public string ITZenHREnvironmentDescriptionText => T("IT.ZenHREnvironmentDescription");
    public string ITCredentialsManagedText => T("IT.CredentialsManaged");
    public string ITCredentialChangesDisabledText => T("IT.CredentialChangesDisabled");
    public string ITReportsDescriptionText => T("IT.ReportsDescription");
    public string ITWorkflowNoticeText => T("IT.WorkflowNotice");
    public string PlaceholderText => T("Placeholder.NextPhase");
    public string LockITModeText => T("Button.LockITMode");
    public string UnlockITModeText => T("Button.Unlock");
    public string ConfirmITWorkflowText => T("IT.ConfirmTitle");
    public string RiskLevelText => T("IT.RiskLevel");
    public string PermissionsText => T("IT.Permissions");
    public string LoggedNoticeText => T("IT.LoggedNotice");
    public string CancelText => T("Button.Cancel");
    public string RunText => T("Button.Run");
    public string AboutTitleText => T("About.Title");
    public string AboutSubtitleText => T("About.Subtitle");
    public string AboutBodyText => T("About.Body");
    public string AboutPrivacyTitleText => T("About.PrivacyTitle");
    public string AboutPrivacyText => T("About.Privacy");
    public string AboutOwnerText => T("About.Owner");
    public string AboutVersionText => T("About.Version");
    public string AboutBuildDateText => T("About.BuildDate");
    public string AboutRuntimeText => T("About.Runtime");
    public string AboutConfigurationModeText => T("About.ConfigurationMode");
    public string AboutUpdateChannelText => T("About.UpdateChannel");
    public string AboutSecurityTitleText => T("About.SecurityTitle");
    public string AboutPrivacyNeverCollectsText => T("About.PrivacyNeverCollects");
    public string AboutPasswordsText => T("About.Passwords");
    public string AboutCookiesText => T("About.Cookies");
    public string AboutBrowserHistoryText => T("About.BrowserHistory");
    public string AboutEmailsText => T("About.Emails");
    public string AboutPersonalFilesText => T("About.PersonalFiles");
    public string AboutITModeProtectedText => T("About.ITModeProtected");
    public string AboutLocalExecutionOnlyText => T("About.LocalExecutionOnly");
    public string AboutSupportPackagesExcludePersonalDataText => T("About.SupportPackagesExcludePersonalData");
    public string AboutTestDiagnosticsText => T("About.TestDiagnostics");
    public string CurrentLanguage => _language;
    public bool IsArabic => _language.Equals("ar", StringComparison.OrdinalIgnoreCase);
    public bool IsEnglish => _language.Equals("en", StringComparison.OrdinalIgnoreCase);
    public string EnglishLanguageBackground => IsEnglish ? "#00A7A5" : "#FFFFFF";
    public string EnglishLanguageForeground => IsEnglish ? "#FFFFFF" : "#006D73";
    public string EnglishLanguageBorder => IsEnglish ? "#00A7A5" : "#DCEAF4";
    public string ArabicLanguageBackground => IsArabic ? "#00A7A5" : "#FFFFFF";
    public string ArabicLanguageForeground => IsArabic ? "#FFFFFF" : "#006D73";
    public string ArabicLanguageBorder => IsArabic ? "#00A7A5" : "#DCEAF4";
    public FlowDirection AppFlowDirection => IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public string ConfigPath => ZenITPaths.SettingsPath;
    public string ReportsPath => ZenITPaths.ReportsDirectory;
    public string RuntimeInfo => $"{RuntimeInformation.FrameworkDescription} on {RuntimeInformation.OSDescription}";
    public string BuildInfo => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Not available";

    public string SupportStatus
    {
        get => _supportStatus;
        private set => SetProperty(ref _supportStatus, value);
    }

    public string ToastMessage
    {
        get => _toastMessage;
        private set
        {
            if (SetProperty(ref _toastMessage, value))
            {
                OnPropertyChanged(nameof(IsToastVisible));
            }
        }
    }

    public string ToastBrush
    {
        get => _toastBrush;
        private set => SetProperty(ref _toastBrush, value);
    }

    public bool IsToastVisible => !string.IsNullOrWhiteSpace(ToastMessage);

    public bool IsConfirmationVisible
    {
        get => _isConfirmationVisible;
        private set => SetProperty(ref _isConfirmationVisible, value);
    }

    public string ConfirmationWorkflowName => _pendingConfirmationWorkflow?.Title ?? "Workflow";
    public string ConfirmationRiskLevel => _pendingConfirmationWorkflow?.RiskText ?? "Low";
    public string ConfirmationDescription => _pendingConfirmationWorkflow?.Description ?? string.Empty;
    public string ConfirmationContinueText => T("Button.Run");
    public string ConfirmationAdminText => _pendingConfirmationWorkflow?.RequiresAdmin == true ? T("IT.AdminRequired") : T("IT.NoAdminRequired");
    public string LastHealthCheckText
    {
        get => _lastHealthCheckText;
        private set => SetProperty(ref _lastHealthCheckText, value);
    }

    public string LastSupportPackageText
    {
        get => _lastSupportPackageText;
        private set => SetProperty(ref _lastSupportPackageText, value);
    }

    public string QuickFixSearchText
    {
        get => _quickFixSearchText;
        set
        {
            if (SetProperty(ref _quickFixSearchText, value))
            {
                ApplyWorkflowFilters();
            }
        }
    }

    public string LogsSearchText
    {
        get => _logsSearchText;
        set
        {
            if (SetProperty(ref _logsSearchText, value))
            {
                ApplyLogFilters();
            }
        }
    }
    public string VersionBadgeText => "v1.0.0";
    public string SidebarVersionText => VersionBadgeText;
    public string FooterText => T("Footer.Text");
    public string PrimaryLogPath => _logService.PrimaryLogPath;
    public string FallbackLogPathText
    {
        get => _fallbackLogPathText;
        private set => SetProperty(ref _fallbackLogPathText, value);
    }
    public bool IsFallbackLogVisible => !string.IsNullOrWhiteSpace(FallbackLogPathText);
    public bool IsITModePolicyAvailable => _itPolicy.EnableITMode &&
                                           !string.IsNullOrWhiteSpace(_itPolicy.ITModeUsername) &&
                                           !string.IsNullOrWhiteSpace(_itPolicy.ITModePasswordHash);
    public bool IsITModePasswordConfigured => IsITModePolicyAvailable;
    public bool IsAuthenticatingITMode
    {
        get => _isAuthenticatingITMode;
        private set
        {
            if (SetProperty(ref _isAuthenticatingITMode, value) &&
                UnlockITModeCommand is AsyncRelayCommand command)
            {
                OnPropertyChanged(nameof(ITAuthenticatingButtonText));
                command.RaiseCanExecuteChanged();
            }
        }
    }

    public string SignedInITUserText => string.IsNullOrWhiteSpace(_signedInITUsername)
        ? string.Empty
        : string.Format(T("IT.SignedInAs"), _signedInITUsername);
    public bool IsItModeUnlocked
    {
        get => _isItModeUnlocked;
        private set
        {
            if (SetProperty(ref _isItModeUnlocked, value))
            {
                OnPropertyChanged(nameof(IsItModeUnlockVisible));
                OnPropertyChanged(nameof(IsItModeDashboardVisible));
                OnPropertyChanged(nameof(IsITModePasswordConfigured));
                OnPropertyChanged(nameof(SignedInITUserText));
            }
        }
    }

    public bool IsItModeUnlockVisible => IsItModeSelected && !IsItModeUnlocked;
    public bool IsItModeDashboardVisible => IsItModeSelected && IsItModeUnlocked;

    public string ItModeStatus
    {
        get => _itModeStatus;
        private set => SetProperty(ref _itModeStatus, value);
    }

    public string ItModeStatusBrush
    {
        get => _itModeStatusBrush;
        private set => SetProperty(ref _itModeStatusBrush, value);
    }

    public string ITModeUsernameAttempt
    {
        get => _itModeUsernameAttempt;
        set => SetProperty(ref _itModeUsernameAttempt, value);
    }

    public NavigationItemViewModel SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        private set
        {
            if (SetProperty(ref _selectedNavigationItem, value))
            {
                OnPropertyChanged(nameof(IsHomeSelected));
                OnPropertyChanged(nameof(IsQuickFixesSelected));
                OnPropertyChanged(nameof(IsWorkflowGridSelected));
                OnPropertyChanged(nameof(IsDeviceHealthSelected));
                OnPropertyChanged(nameof(IsItModeSelected));
                OnPropertyChanged(nameof(IsItModeUnlockVisible));
                OnPropertyChanged(nameof(IsItModeDashboardVisible));
                OnPropertyChanged(nameof(IsAboutSelected));
                OnPropertyChanged(nameof(CurrentPageTitle));
                OnPropertyChanged(nameof(CurrentPageSubtitle));
            }
        }
    }

    public bool IsHomeSelected => SelectedNavigationItem.Key == "home";
    public bool IsQuickFixesSelected => SelectedNavigationItem.Key == "quick-fixes";
    public bool IsWorkflowGridSelected => IsHomeSelected || IsQuickFixesSelected;
    public bool IsDeviceHealthSelected => SelectedNavigationItem.Key == "device-health";
    public bool IsItModeSelected => SelectedNavigationItem.Key == "it-mode";
    public bool IsAboutSelected => SelectedNavigationItem.Key == "about";
    public string CurrentPageTitle => IsHomeSelected ? T("Header.HomeTitle") : SelectedNavigationItem.Title;

    public string CurrentPageSubtitle => SelectedNavigationItem.Key switch
    {
        "home" => T("Header.HomeSubtitle"),
        "quick-fixes" => T("Header.QuickFixesSubtitle"),
        "device-health" => T("Header.MyDeviceSubtitle"),
        "it-mode" => T("Header.ITModeSubtitle"),
        "about" => T("Header.AboutSubtitle"),
        _ => T("Header.QuickFixesSubtitle")
    };

    public string HealthCheckStatus
    {
        get => _healthCheckStatus;
        private set => SetProperty(ref _healthCheckStatus, value);
    }

    public string HealthCheckStatusBrush
    {
        get => _healthCheckStatusBrush;
        private set => SetProperty(ref _healthCheckStatusBrush, value);
    }

    public int DeviceHealthScore
    {
        get => _deviceHealthScore;
        private set => SetProperty(ref _deviceHealthScore, value);
    }

    public string DeviceHealthScoreStatus
    {
        get => _deviceHealthScoreStatus;
        private set => SetProperty(ref _deviceHealthScoreStatus, value);
    }

    public string DeviceHealthScoreBrush
    {
        get => _deviceHealthScoreBrush;
        private set => SetProperty(ref _deviceHealthScoreBrush, value);
    }

    public string LogsStatus
    {
        get => _logsStatus;
        private set => SetProperty(ref _logsStatus, value);
    }

    private void SelectNavigation(object? parameter)
    {
        if (parameter is not NavigationItemViewModel item)
        {
            return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, item);
        }

        SelectedNavigationItem = item;
        if (item.Key == "it-mode" && IsItModeUnlocked)
        {
            RefreshItDiagnostics();
            RefreshLogs();
        }
    }

    private void SelectWorkflowCategory(object? parameter)
    {
        if (parameter is not CategoryFilterViewModel category)
        {
            return;
        }

        foreach (var item in WorkflowCategories)
        {
            item.IsSelected = ReferenceEquals(item, category);
        }

        _selectedWorkflowCategory = category.Name;
        ApplyWorkflowFilters();
    }

    private void ApplyWorkflowFilters()
    {
        FilteredActions.Clear();
        var query = QuickFixSearchText.Trim();
        var allCategory = WorkflowCategories.Count > 0 ? WorkflowCategories[0].Name : "All";
        foreach (var action in Actions.Where(action =>
                     (_selectedWorkflowCategory == "All" || _selectedWorkflowCategory == allCategory || action.Category.Equals(_selectedWorkflowCategory, StringComparison.OrdinalIgnoreCase)) &&
                     (string.IsNullOrWhiteSpace(query) ||
                      action.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                      action.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                      action.Category.Contains(query, StringComparison.OrdinalIgnoreCase))))
        {
            FilteredActions.Add(action);
        }
    }

    private void SwitchLanguage(object? parameter)
    {
        var requested = parameter?.ToString();
        if (requested is not ("en" or "ar") || requested.Equals(_language, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _language = requested;
        _settings = _settings with { Language = _language };
        _settingsService.Save(_settings);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        SetNavigationTitle("home", T("Nav.Home"));
        SetNavigationTitle("quick-fixes", T("Nav.QuickFixes"));
        SetNavigationTitle("device-health", T("Nav.MyDevice"));
        SetNavigationTitle("it-mode", T("Nav.ITMode"));
        SetNavigationTitle("about", T("Nav.About"));

        ApplyWorkflowText(WorkflowId.InternetNotWorking, "Workflow.Internet.Title", "Workflow.Internet.Description", "Connectivity");
        ApplyWorkflowText(WorkflowId.ImproveDevicePerformance, "Workflow.Speed.Title", "Workflow.Speed.Description", "Performance");
        ApplyWorkflowText(WorkflowId.FixEverythingSafe, "Workflow.Everything.Title", "Workflow.Everything.Description", "Recommended");
        ApplyWorkflowText(WorkflowId.ChromeNotWorking, "Workflow.Chrome.Title", "Workflow.Chrome.Description", "Productivity");
        ApplyWorkflowText(WorkflowId.CameraOrMicrophoneNotWorking, "Workflow.CameraMic.Title", "Workflow.CameraMic.Description", "Meetings");
        ApplyWorkflowText(WorkflowId.SoundNotWorking, "Workflow.Sound.Title", "Workflow.Sound.Description", "Meetings");
        ApplyWorkflowText(WorkflowId.SlackNotWorking, "Workflow.Slack.Title", "Workflow.Slack.Description", "Productivity");
        ApplyWorkflowText(WorkflowId.ZoomNotWorking, "Workflow.Zoom.Title", "Workflow.Zoom.Description", "Meetings");
        ApplyWorkflowText(WorkflowId.GoogleDriveNotSyncing, "Workflow.Drive.Title", "Workflow.Drive.Description", "Productivity");
        ApplyWorkflowText(WorkflowId.SecurityCheck, "Workflow.Security.Title", "Workflow.Security.Description", "Security");
        ApplyWorkflowText(WorkflowId.DeviceHealthCheck, "Workflow.Device.Title", "Workflow.Device.Description", "My Device");
        ApplyWorkflowText(WorkflowId.CollectITReport, "Workflow.Package.Title", "Workflow.Package.Description", "Support");
        ApplyWorkflowText(WorkflowId.ContactIT, "Workflow.Contact.Title", "Workflow.Contact.Description", "Support");
        ApplyWorkflowCategoryLabels();

        OnPropertyChanged(string.Empty);

        if (string.IsNullOrWhiteSpace(HealthCheckStatus))
        {
            HealthCheckStatus = DeviceSubtitleText;
        }

        if (string.IsNullOrWhiteSpace(LastHealthCheckText))
        {
            LastHealthCheckText = T("Reports.NoReports");
        }

        if (string.IsNullOrWhiteSpace(LastSupportPackageText))
        {
            LastSupportPackageText = T("Reports.NoReports");
        }

        if (string.IsNullOrWhiteSpace(SupportStatus))
        {
            SupportStatus = HomeSupportText;
        }

        if (string.IsNullOrWhiteSpace(ItModeStatus))
        {
            SetItModeMessage(T("IT.UnlockIntro"), "#6B7A8A");
        }

        ApplyWorkflowFilters();
        UpdateHomeSummary();
    }

    private void ApplyWorkflowCategoryLabels()
    {
        var categories = new[] { "All", "Recommended", "Connectivity", "Performance", "Meetings", "Productivity", "Security", "My Device", "Support" };
        for (var index = 0; index < WorkflowCategories.Count && index < categories.Length; index++)
        {
            WorkflowCategories[index].Name = categories[index] == "All" && IsArabic ? "الكل" : LocalizeCategory(categories[index]);
            WorkflowCategories[index].IsSelected = index == 0;
        }

        _selectedWorkflowCategory = WorkflowCategories[0].Name;
    }

    private void SetNavigationTitle(string key, string title)
    {
        var item = NavigationItems.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            item.Title = title;
        }
    }

    private void ApplyWorkflowText(WorkflowId id, string titleKey, string descriptionKey, string category)
    {
        var localizedCategory = LocalizeCategory(category);
        foreach (var action in Actions.Where(action => action.Id == id))
        {
            var title = T(titleKey);
            action.ApplyDisplayText(title, T(descriptionKey), title, localizedCategory);
            action.ApplyLocalization(T);
            action.ApplyReadyText(T("Status.Available"));
        }

        foreach (var action in ITActions)
        {
            action.ApplyLocalization(T);
        }
    }

    private string LocalizeCategory(string category)
    {
        if (!IsArabic)
        {
            return category;
        }

        return category switch
        {
            "Recommended" => "موصى به",
            "Connectivity" => "الاتصال",
            "Performance" => "الأداء",
            "Meetings" => "الاجتماعات",
            "Productivity" => "الإنتاجية",
            "Security" => "الأمان",
            "My Device" => "جهازي",
            "Support" => "الدعم",
            _ => category
        };
    }

    private string T(string key)
    {
        return LocalizedStrings.Get(_language, key);
    }

    private void SelectLogFilter(object? parameter)
    {
        if (parameter is not CategoryFilterViewModel filter)
        {
            return;
        }

        foreach (var item in LogFilters)
        {
            item.IsSelected = ReferenceEquals(item, filter);
        }

        _selectedLogFilter = filter.Name;
        ApplyLogFilters();
    }

    private void SelectLogSort(object? parameter)
    {
        if (parameter is not CategoryFilterViewModel sort)
        {
            return;
        }

        foreach (var item in LogSortOptions)
        {
            item.IsSelected = ReferenceEquals(item, sort);
        }

        _selectedLogSort = sort.Name;
        ApplyLogFilters();
    }

    private void ApplyLogFilters()
    {
        if (FilteredLogRows is null)
        {
            return;
        }

        FilteredLogRows.Clear();
        var query = LogsSearchText.Trim();
        var rows = LogRows.Where(row =>
            MatchesLogFilter(row) &&
            (string.IsNullOrWhiteSpace(query) ||
             row.Action.Contains(query, StringComparison.OrdinalIgnoreCase)));
        rows = _selectedLogSort == "Oldest First"
            ? rows.OrderBy(row => row.Timestamp)
            : rows.OrderByDescending(row => row.Timestamp);

        foreach (var row in rows)
        {
            FilteredLogRows.Add(row);
        }
    }

    private bool MatchesLogFilter(LogRowViewModel row)
    {
        return _selectedLogFilter switch
        {
            "Success" => row.Result.Equals("Success", StringComparison.OrdinalIgnoreCase),
            "Warning" => row.Result.Contains("Needs", StringComparison.OrdinalIgnoreCase) ||
                         row.Result.Contains("Warning", StringComparison.OrdinalIgnoreCase),
            "Failed" => row.Result.Contains("Fail", StringComparison.OrdinalIgnoreCase),
            "IT Mode" => row.Action.Contains("IT Mode", StringComparison.OrdinalIgnoreCase) ||
                         row.ActionId.Contains("ITMode", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    public void SetItModePasswordValue(string fieldName, string password)
    {
        if (fieldName.Equals("Unlock", StringComparison.Ordinal))
        {
            _unlockPassword = password;
        }
    }

    private void SelectITWorkflowCategory(object? parameter)
    {
        NotifyUserActivity();
        if (parameter is not CategoryFilterViewModel category)
        {
            return;
        }

        foreach (var item in ITWorkflowCategories)
        {
            item.IsSelected = ReferenceEquals(item, category);
        }

        _selectedITWorkflowCategory = category.Name;
        ApplyITWorkflowFilters();
    }

    private void ApplyITWorkflowFilters()
    {
        FilteredITActions.Clear();
        foreach (var action in ITActions.Where(action =>
                     _selectedITWorkflowCategory == "All" ||
                     action.Category.Equals(_selectedITWorkflowCategory, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredITActions.Add(action);
        }
    }

    private async Task UnlockITModeAsync()
    {
        if (!IsITModePasswordConfigured)
        {
            SetItModeMessage(T("IT.PolicyUnavailableInstall"), "#E5484D");
            return;
        }

        IsAuthenticatingITMode = true;
        SetItModeMessage(T("IT.Authenticating"), "#6B7A8A");
        var attemptedUsername = ITModeUsernameAttempt.Trim();
        try
        {
            var usernameMatches = attemptedUsername.Equals(_itPolicy.ITModeUsername, StringComparison.OrdinalIgnoreCase);
            var passwordMatches = PasswordHashService.VerifyPassword(_unlockPassword, _itPolicy.ITModePasswordHash);
            if (usernameMatches && passwordMatches)
            {
                await LogITModeLoginAsync("Success", attemptedUsername);
                _signedInITUsername = _itPolicy.ITModeUsername;
                ClearPasswordValues();
                IsItModeUnlocked = true;
                OnPropertyChanged(nameof(SignedInITUserText));
                RefreshItDiagnostics();
                ResetITModeInactivityTimer();
                SetItModeMessage(T("IT.AdministratorVerified"), "#20B486");
                return;
            }

            await LogITModeLoginAsync("Failed", attemptedUsername);
            ClearPasswordValues();
            SetItModeMessage(T("IT.Invalid"), "#E5484D");
        }
        finally
        {
            IsAuthenticatingITMode = false;
        }
    }

    private Task LogITModeLoginAsync(string result, string attemptedUsername)
    {
        return _logService.LogActionAsync(new ActionLogEntry(
            DateTimeOffset.Now,
            Environment.UserName,
            Environment.MachineName,
            "ITModeLogin",
            "IT Mode Login",
            result,
            $"UsernameAttempted={attemptedUsername}; Timestamp={DateTimeOffset.Now:O}",
            Duration: TimeSpan.Zero));
    }

    private void LockITMode(string reason)
    {
        ClearPasswordValues();
        var signedInUser = _signedInITUsername;
        _signedInITUsername = string.Empty;
        _itModeSessionTimer.Stop();
        IsItModeUnlocked = false;
        OnPropertyChanged(nameof(SignedInITUserText));
        _ = LogITModeSessionAsync(reason, signedInUser);
        var timeout = reason.Equals("InactivityTimeout", StringComparison.OrdinalIgnoreCase);
        SetItModeMessage(timeout ? T("IT.LockedInactivity") : T("IT.Locked"), "#6B7A8A");
        if (timeout)
        {
            ShowToast(T("IT.LockedInactivity"), "#F5A623");
        }
    }

    public void NotifyUserActivity()
    {
        if (IsItModeUnlocked)
        {
            ResetITModeInactivityTimer();
        }
    }

    private void ResetITModeInactivityTimer()
    {
        _itModeSessionTimer.Stop();
        if (IsItModeUnlocked)
        {
            _itModeSessionTimer.Start();
        }
    }

    private void OnITModeSessionTimeout(object? sender, EventArgs e)
    {
        if (IsItModeUnlocked)
        {
            LockITMode("InactivityTimeout");
        }
    }

    private Task LogITModeSessionAsync(string reason, string signedInUser)
    {
        return _logService.LogActionAsync(new ActionLogEntry(
            DateTimeOffset.Now,
            Environment.UserName,
            Environment.MachineName,
            "ITModeSession",
            "IT Mode Session",
            "Locked",
            $"Reason={reason}; Username={signedInUser}",
            Duration: TimeSpan.Zero));
    }

    private void SetItModeMessage(string message, string brush)
    {
        ItModeStatus = message;
        ItModeStatusBrush = brush;
    }

    private void ClearPasswordValues()
    {
        _unlockPassword = string.Empty;
        PasswordFieldsCleared?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunHealthCheckAsync()
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            HealthCheckStatusBrush = "#6B7A8A";
            HealthCheckStatus = "Checking your device now...";

            var health = _deviceHealthService.GetCurrentHealth();
            DeviceStatus = CreateDeviceStatus(health);
            UpdateHealthMetrics(health);
            RefreshLogs();

            HealthCheckStatus = "Health check complete. Your device summary is up to date.";
            HealthCheckStatusBrush = "#20B486";
            await _logService.LogActionAsync(new ActionLogEntry(
                DateTimeOffset.Now,
                health.CurrentWindowsUsername,
                health.DeviceName,
                "DeviceHealthPage",
                "Device Health Check",
                "Success",
                "Device health page refreshed.",
                Duration: DateTimeOffset.Now - startedAt));
            RefreshLogs();
        }
        catch (Exception exception)
        {
            HealthCheckStatus = "Health check could not finish. IT Support can still help.";
            HealthCheckStatusBrush = "#E5484D";
            await _logService.LogActionAsync(new ActionLogEntry(
                DateTimeOffset.Now,
                Environment.UserName,
                Environment.MachineName,
                "DeviceHealthPage",
                "Device Health Check",
                "Failure",
                "Device health page check failed.",
                null,
                exception.Message,
                DateTimeOffset.Now - startedAt));
            RefreshLogs();
        }
    }

    private async Task CollectITReportAsync()
    {
        try
        {
            HealthCheckStatusBrush = "#6B7A8A";
            HealthCheckStatus = "Preparing a support report...";

            var executor = new LocalWorkflowExecutor(_processRunner, _deviceHealthService, _logService, _settings, _itPolicy);
            var result = await executor.ExecuteAsync(WorkflowId.CollectITReport);
            var duration = result.FinishedAt - result.StartedAt;

            HealthCheckStatus = result.Success ? "Support package created successfully." : result.UserMessage;
            HealthCheckStatusBrush = result.Success ? "#20B486" : "#E5484D";
            if (!string.IsNullOrWhiteSpace(result.ReportPath))
            {
                LastSupportPackageText = $"{Path.GetFileName(result.ReportPath)} - {DateTimeOffset.Now:MMM d, h:mm tt}";
            }

            await _logService.LogActionAsync(new ActionLogEntry(
                DateTimeOffset.Now,
                DeviceStatus.CurrentUser,
                DeviceStatus.DeviceName,
                result.WorkflowId.ToString(),
                "Create Support Package",
                result.Success ? "Success" : result.NeedsITSupport ? "Needs IT Support" : "Needs Attention",
                result.TechnicalMessage,
                result.ReportPath,
                result.Success ? null : result.TechnicalMessage,
                duration));
            RefreshLogs();
        }
        catch (Exception exception)
        {
            HealthCheckStatus = "Report could not be prepared. IT Support can still help.";
            HealthCheckStatusBrush = "#E5484D";
            await _logService.LogActionAsync(new ActionLogEntry(
                DateTimeOffset.Now,
                Environment.UserName,
                Environment.MachineName,
                WorkflowId.CollectITReport.ToString(),
                "Create Support Package",
                "Failure",
                "Device Health page report failed.",
                null,
                exception.Message,
                TimeSpan.Zero));
            RefreshLogs();
        }
    }

    private void UpdateHealthMetrics(DeviceHealthInfo health)
    {
        HealthMetrics.Clear();
        HealthStatusChips.Clear();
        ConnectivityHealthItems.Clear();
        SecurityHealthItems.Clear();
        PerformanceHealthItems.Clear();
        UpdatesHealthItems.Clear();

        var diskHealthy = health.TotalDiskSpaceBytes > 0 &&
                          health.FreeDiskSpaceBytes / (double)health.TotalDiskSpaceBytes >= 0.10 &&
                          health.FreeDiskSpaceBytes >= 10L * 1024L * 1024L * 1024L;
        var batteryHealthy = health.BatteryPercentage is null or >= 20;
        var batteryStatus = health.BatteryPercentage switch
        {
            null => ("Unavailable", "#6B7A8A"),
            < 20 => ("Low", "#F5A623"),
            _ => ("Good", "#20B486")
        };
        var uptimeStatus = health.Uptime.TotalDays >= 7
            ? ("Restart recommended", "#F5A623")
            : ("Normal", "#20B486");

        HealthStatusChips.Add(new HealthStatusChipViewModel("Internet", health.InternetConnectivityStatus == "Connected" ? "Online" : "Offline", health.InternetConnectivityStatus == "Connected" ? "#20B486" : "#E5484D"));
        HealthStatusChips.Add(new HealthStatusChipViewModel("Disk", diskHealthy ? "Healthy" : "Low space", diskHealthy ? "#20B486" : "#F5A623"));
        HealthStatusChips.Add(new HealthStatusChipViewModel("Battery", batteryStatus.Item1, batteryStatus.Item2));
        HealthStatusChips.Add(new HealthStatusChipViewModel("Uptime", uptimeStatus.Item1, uptimeStatus.Item2));

        HealthMetrics.Add(new HealthMetricViewModel("Device", health.DeviceName, "Computer name"));
        HealthMetrics.Add(new HealthMetricViewModel("User", health.CurrentWindowsUsername, "Signed-in macOS profile"));
        HealthMetrics.Add(new HealthMetricViewModel("macOS", health.WindowsVersion, "Operating system"));
        HealthMetrics.Add(new HealthMetricViewModel("Uptime", FormatUptime(health.Uptime), "Time since last restart"));
        HealthMetrics.Add(new HealthMetricViewModel("Internet", health.InternetConnectivityStatus, "Network availability"));
        HealthMetrics.Add(new HealthMetricViewModel("Free disk space", FormatBytes(health.FreeDiskSpaceBytes), $"{FormatBytes(health.TotalDiskSpaceBytes)} total"));
        HealthMetrics.Add(new HealthMetricViewModel("Battery", health.BatteryPercentage.HasValue ? $"{health.BatteryPercentage.Value}%" : "Not available", "Battery percentage"));
        HealthMetrics.Add(new HealthMetricViewModel("Local time", health.CurrentLocalTime.ToString("MMM d, yyyy h:mm tt"), "Current device time"));

        var gateway = GetDefaultGatewayText();
        var dns = GetDnsText();
        var driveRunning = MacSystemInfo.IsAnyProcessRunning(MacApplicationProfiles.GoogleDrive.ProcessNames);
        var internetOnline = health.InternetConnectivityStatus == "Connected";
        var gatewayAvailable = gateway != "Not available";
        var dnsAvailable = dns != "Not available";
        ConnectivityHealthItems.Add(CreateStatus("Internet", internetOnline ? "Online" : "Offline", internetOnline));
        ConnectivityHealthItems.Add(CreateStatus("Gateway", gatewayAvailable ? "Detected" : "Not detected", gatewayAvailable));
        ConnectivityHealthItems.Add(CreateStatus(T("Label.DNS"), dnsAvailable ? "Detected" : "Not detected", dnsAvailable));
        ConnectivityHealthItems.Add(CreateStatus("Google Drive Sync", driveRunning ? "Running" : "Not running", driveRunning));

        var jumpCloud = MacSystemInfo.IsAnyProcessRunning(MacApplicationProfiles.JumpCloud.ProcessNames);
        SecurityHealthItems.Add(CreateStatus("JumpCloud Status", jumpCloud ? "Detected" : "Not detected", jumpCloud));

        var memory = MacSystemInfo.GetMemoryStatus();
        var memoryGood = memory.AvailablePercent >= 15;
        var cpuUsage = MacSystemInfo.GetCpuUsageEstimate();
        PerformanceHealthItems.Add(new StatusItemViewModel("CPU Usage", cpuUsage.HasValue ? $"{cpuUsage.Value:0}%" : $"{Process.GetProcesses().Length} processes", cpuUsage is null or < 80 ? "Normal" : "High", cpuUsage is null or < 80 ? "#20B486" : "#F5A623"));
        PerformanceHealthItems.Add(new StatusItemViewModel("RAM Usage", $"{memory.AvailablePercent:0}% available", memoryGood ? "Healthy" : "Low", memoryGood ? "#20B486" : "#F5A623"));
        PerformanceHealthItems.Add(new StatusItemViewModel("Disk Space", FormatBytes(health.FreeDiskSpaceBytes), diskHealthy ? "Healthy" : "Low", diskHealthy ? "#20B486" : "#F5A623"));

        var crashReports = MacSystemInfo.CountRecentDiagnosticReports(7);
        UpdatesHealthItems.Add(new StatusItemViewModel("Crash Reports (7 days)", crashReports.ToString(), crashReports == 0 ? "Normal" : "Review", crashReports == 0 ? "#20B486" : "#F5A623"));
        UpdatesHealthItems.Add(new StatusItemViewModel("Software Updates", "Checked by IT via Software Update Check", "Info", "#6B7A8A"));
        UpdateDeviceHealthScore(new HealthScoreInput(
            internetOnline,
            gatewayAvailable,
            dnsAvailable,
            diskHealthy,
            memoryGood,
            cpuUsage is null or < 80,
            batteryHealthy,
            PendingReboot: false,
            jumpCloud,
            driveRunning));
        LastHealthCheckText = health.CurrentLocalTime.ToString("MMM d, yyyy h:mm tt");
    }

    private async Task ExportReportFormatAsync()
    {
        await CollectITReportAsync();
        ShowToast("Support package exported.", "#20B486");
    }

    private void UpdateDeviceHealthScore(HealthScoreInput input)
    {
        var result = HealthScoreCalculator.Calculate(input);
        DeviceHealthScore = result.Score;
        DeviceHealthScoreStatus = T($"HealthScore.{result.Status.Replace(" ", string.Empty, StringComparison.Ordinal)}");
        DeviceHealthScoreBrush = result.Brush;
    }

    private static DeviceStatus CreateDeviceStatus(DeviceHealthInfo health)
    {
        return new DeviceStatus(
            health.DeviceName,
            health.CurrentWindowsUsername,
            health.InternetConnectivityStatus,
            health.CurrentLocalTime);
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        }

        return $"{uptime.Hours}h {uptime.Minutes}m";
    }

    private static string FormatBytes(long bytes)
    {
        const double gibibyte = 1024d * 1024d * 1024d;
        return $"{bytes / gibibyte:0.0} GB";
    }

    private static StatusItemViewModel CreateStatus(string label, string value, bool isHealthy)
    {
        return new StatusItemViewModel(label, value, isHealthy ? "Healthy" : "Needs Attention", isHealthy ? "#20B486" : "#F5A623");
    }

    private static string GetDefaultGatewayText()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().GatewayAddresses)
            .Select(gateway => gateway.Address.ToString())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Not available";
    }

    private static string GetDnsText()
    {
        var dnsServers = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().DnsAddresses)
            .Select(address => address.ToString())
            .Distinct()
            .Take(2)
            .ToList();

        return dnsServers.Count == 0 ? "Not available" : string.Join(", ", dnsServers);
    }

    private static StatusItemViewModel CreateAppStatus(string label, ApplicationProcessProfile profile)
    {
        var running = MacSystemInfo.IsAnyProcessRunning(profile.ProcessNames);
        var installed = running || profile.ExecutableCandidates.Any(Directory.Exists);
        var status = installed ? running ? "Healthy" : "Not Running" : "Not Installed";
        var brush = installed ? running ? "#20B486" : "#F5A623" : "#E5484D";
        var value = installed ? $"Installed; Running={running}" : "Not installed";
        return new StatusItemViewModel(label, value, status, brush);
    }

    private void RefreshLogs()
    {
        LogRows.Clear();
        RecentActivity.Clear();
        var summaries = _logService.GetLatestSummaries(50);
        foreach (var summary in summaries)
        {
            LogRows.Add(new LogRowViewModel(summary));
        }

        foreach (var summary in summaries.Take(10))
        {
            RecentActivity.Add(new RecentActivityViewModel(summary, T));
        }

        UpdateLastSupportPackageFromSummaries(summaries);
        LogsStatus = (LogRows.Count, _logService.LastParseErrorCount) switch
        {
            (0, > 0) => "Some support history could not be read. IT Support can still open the log folder.",
            (0, _) => "No support history found yet. Run a quick fix and come back here.",
            (_, > 0) => "Latest support history is shown below. Some older entries could not be read.",
            _ => "Latest support history is shown below."
        };
        ApplyLogFilters();
        UpdateHomeSummary();
        UpdateLogDiagnostics();
    }

    private void UpdateLastSupportPackageFromSummaries(IReadOnlyList<ActionLogSummary> summaries)
    {
        var latestPackage = summaries.FirstOrDefault(summary =>
            !string.IsNullOrWhiteSpace(summary.ReportPath) &&
            summary.ActionName.Contains("Support Package", StringComparison.OrdinalIgnoreCase));
        if (latestPackage is null)
        {
            return;
        }

        LastSupportPackageText = $"{Path.GetFileName(latestPackage.ReportPath)} - {latestPackage.Timestamp.ToLocalTime():MMM d, h:mm tt}";
    }

    private void UpdateHomeSummary()
    {
        if (Actions is null)
        {
            return;
        }

        HomeActions.Clear();
        var preferred = new[]
        {
            WorkflowId.FixEverythingSafe,
            WorkflowId.ImproveDevicePerformance,
            WorkflowId.InternetNotWorking,
            WorkflowId.CameraOrMicrophoneNotWorking,
            WorkflowId.CollectITReport,
            WorkflowId.ContactIT
        };

        foreach (var action in preferred
                     .Select(id => Actions.FirstOrDefault(action => action.Id == id))
                     .Where(action => action is not null)
                     .Cast<WorkflowCardViewModel>())
        {
            HomeActions.Add(action);
        }
    }

    private async Task OpenLogFolderAsync()
    {
        await OpenKnownFolderAsync(ZenITPaths.LogsDirectory);
    }

    private async Task OpenReportsFolderAsync()
    {
        await OpenKnownFolderAsync(ZenITPaths.ReportsDirectory);
    }

    private async Task OpenKnownFolderAsync(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        await _processRunner.RunAsync("open", folderPath, TimeSpan.FromSeconds(5));
    }

    private void CopyLatestSummary()
    {
        var summaryText = _logService.GetLatestSummaryText(20);
        if (ClipboardSetter is not null)
        {
            _ = ClipboardSetter(summaryText);
        }

        LogsStatus = "Latest summary copied.";
    }

    private async Task ContactITAsync()
    {
        var startedAt = DateTimeOffset.Now;
        try
        {
            var executor = new LocalWorkflowExecutor(_processRunner, _deviceHealthService, _logService, _settings, _itPolicy);
            var result = await executor.ExecuteAsync(WorkflowId.ContactIT);
            var duration = result.FinishedAt - result.StartedAt;
            SupportStatus = result.UserMessage;
            ShowToast(result.UserMessage, result.Success ? "#20B486" : "#E5484D");
            await _logService.LogActionAsync(new ActionLogEntry(
                DateTimeOffset.Now,
                DeviceStatus.CurrentUser,
                DeviceStatus.DeviceName,
                WorkflowId.ContactIT.ToString(),
                "Contact IT",
                result.Success ? "Success" : "Failure",
                result.TechnicalMessage,
                result.ReportPath,
                result.Success ? null : result.TechnicalMessage,
                duration));
            RefreshLogs();
        }
        catch (Exception exception)
        {
            SupportStatus = "Could not open Slack. Please contact IT manually.";
            ShowToast(SupportStatus, "#E5484D");
            await _logService.LogActionAsync(new ActionLogEntry(
                DateTimeOffset.Now,
                DeviceStatus.CurrentUser,
                DeviceStatus.DeviceName,
                WorkflowId.ContactIT.ToString(),
                "Contact IT",
                "Failure",
                "ContactIT command failed.",
                null,
                exception.Message,
                DateTimeOffset.Now - startedAt));
            RefreshLogs();
        }
    }

    private Task<bool> ConfirmWorkflowAsync(WorkflowCardViewModel workflow)
    {
        if (workflow.AccessTier == WorkflowAccessTier.Employee)
        {
            return Task.FromResult(true);
        }

        _pendingConfirmationWorkflow = workflow;
        _confirmationCompletion = new TaskCompletionSource<bool>();
        OnPropertyChanged(nameof(ConfirmationWorkflowName));
        OnPropertyChanged(nameof(ConfirmationRiskLevel));
        OnPropertyChanged(nameof(ConfirmationDescription));
        OnPropertyChanged(nameof(ConfirmationContinueText));
        OnPropertyChanged(nameof(ConfirmationAdminText));
        IsConfirmationVisible = true;
        return _confirmationCompletion.Task;
    }

    private void ResolveConfirmation(bool shouldRun)
    {
        IsConfirmationVisible = false;
        _pendingConfirmationWorkflow = null;
        _confirmationCompletion?.TrySetResult(shouldRun);
        _confirmationCompletion = null;
        OnPropertyChanged(nameof(ConfirmationWorkflowName));
        OnPropertyChanged(nameof(ConfirmationRiskLevel));
        OnPropertyChanged(nameof(ConfirmationDescription));
        OnPropertyChanged(nameof(ConfirmationContinueText));
        OnPropertyChanged(nameof(ConfirmationAdminText));
    }

    private async void ShowToast(string message, string brush)
    {
        ToastBrush = brush;
        ToastMessage = message;
        await Task.Delay(TimeSpan.FromSeconds(4));
        if (ToastMessage.Equals(message, StringComparison.Ordinal))
        {
            ToastMessage = string.Empty;
        }
    }

    private void ExportLog()
    {
        try
        {
            Directory.CreateDirectory(ZenITPaths.ReportsDirectory);
            var path = Path.Combine(ZenITPaths.ReportsDirectory, $"ZenIT-LogSummary-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, _logService.GetLatestSummaryText(200));
            LogsStatus = "Log summary exported to Reports.";
        }
        catch
        {
            LogsStatus = "Log export could not finish. IT Support can still open the local log.";
        }
    }

    private void RefreshItDiagnostics()
    {
        ItNetworkDiagnostics.Clear();
        ItSystemDiagnostics.Clear();
        ItEnvironmentDiagnostics.Clear();
        ItSummaryItems.Clear();

        var internet = NetworkInterface.GetIsNetworkAvailable();
        var hardware = MacSystemInfo.GetHardwareInfo();
        var health = _deviceHealthService.GetCurrentHealth();
        ItSummaryItems.Add(new StatusItemViewModel("Device Name", health.DeviceName, "Info", "#6B7A8A"));
        ItSummaryItems.Add(new StatusItemViewModel("Serial Number", hardware.SerialNumber, "Info", "#6B7A8A"));
        ItSummaryItems.Add(new StatusItemViewModel("Manufacturer", hardware.Manufacturer, "Info", "#6B7A8A"));
        ItSummaryItems.Add(new StatusItemViewModel("Model", hardware.Model, "Info", "#6B7A8A"));
        ItSummaryItems.Add(new StatusItemViewModel("macOS Build", MacSystemInfo.GetMacOSBuild(), "Info", "#6B7A8A"));
        ItSummaryItems.Add(new StatusItemViewModel("Last Reboot", (DateTimeOffset.Now - health.Uptime).ToString("MMM d, yyyy h:mm tt"), "Info", "#6B7A8A"));
        ItSummaryItems.Add(CreateStatus("JumpCloud Status", MacSystemInfo.IsAnyProcessRunning(MacApplicationProfiles.JumpCloud.ProcessNames) ? "Detected" : "Not detected", MacSystemInfo.IsAnyProcessRunning(MacApplicationProfiles.JumpCloud.ProcessNames)));
        ItSummaryItems.Add(new StatusItemViewModel("FileVault Status", GetShortStatus("/usr/bin/fdesetup", "status"), "Review", "#F5A623"));
        ItNetworkDiagnostics.Add(CreateStatus("Public IP", "Not collected yet", true));
        ItNetworkDiagnostics.Add(CreateStatus("Gateway", GetDefaultGatewayText(), GetDefaultGatewayText() != "Not available"));
        ItNetworkDiagnostics.Add(CreateStatus(T("Label.DNS"), GetDnsText(), GetDnsText() != "Not available"));
        ItNetworkDiagnostics.Add(new StatusItemViewModel("VPN Status", MacSystemInfo.CountProcessesContaining("vpn") > 0 ? "Detected" : "No VPN detected", "Review", MacSystemInfo.CountProcessesContaining("vpn") > 0 ? "#F5A623" : "#20B486"));
        ItNetworkDiagnostics.Add(new StatusItemViewModel("Packet Loss Test", internet ? "Basic connectivity available" : "Offline", internet ? "Healthy" : "Needs attention", internet ? "#20B486" : "#E5484D"));
        ItNetworkDiagnostics.Add(new StatusItemViewModel("Latency Test", "Not collected yet", "Review", "#F5A623"));

        var crashReports = MacSystemInfo.CountRecentDiagnosticReports(7);
        ItSystemDiagnostics.Add(new StatusItemViewModel("Failed Launch Services", string.Join(", ", MacSystemInfo.GetFailedLaunchServices(5)), "Review", "#F5A623"));
        ItSystemDiagnostics.Add(new StatusItemViewModel("Crash Reports (7 days)", crashReports.ToString(), crashReports == 0 ? "Normal" : "Review", crashReports == 0 ? "#20B486" : "#F5A623"));
        ItSystemDiagnostics.Add(new StatusItemViewModel("SIP Status", GetShortStatus("/usr/bin/csrutil", "status"), "Review", "#F5A623"));
        ItSystemDiagnostics.Add(new StatusItemViewModel("Gatekeeper Status", GetShortStatus("/usr/sbin/spctl", "--status"), "Review", "#F5A623"));
        ItSystemDiagnostics.Add(new StatusItemViewModel("Software Update Status", "Run Software Update Check workflow", "Review", "#F5A623"));

        ItEnvironmentDiagnostics.Add(CreateAppStatus("JumpCloud", MacApplicationProfiles.JumpCloud));
        ItEnvironmentDiagnostics.Add(CreateAppStatus("Chrome", MacApplicationProfiles.Chrome));
        ItEnvironmentDiagnostics.Add(CreateAppStatus("Slack", MacApplicationProfiles.Slack));
        ItEnvironmentDiagnostics.Add(CreateAppStatus("Zoom", MacApplicationProfiles.Zoom));
        ItEnvironmentDiagnostics.Add(CreateAppStatus("Google Drive", MacApplicationProfiles.GoogleDrive));
    }

    private static string GetShortStatus(string fileName, string arguments)
    {
        var output = MacSystemInfo.RunCapture(fileName, arguments);
        if (string.IsNullOrWhiteSpace(output))
        {
            return "Not available";
        }

        return output.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private void StartBackgroundStartupMaintenance()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _logService.LogActionAsync(new ActionLogEntry(
                    DateTimeOffset.Now,
                    Environment.UserName,
                    Environment.MachineName,
                    "AppStartup",
                    "App Startup",
                    "Success",
                    $"AppMode={_settings.AppMode}; CompanyName={_settings.CompanyName}; Theme={_settings.Theme}; UpdateChannel={_settings.UpdateChannel}"));

                var configurationIssues = AppSettingsValidator.Validate(_settings);
                await _logService.LogActionAsync(new ActionLogEntry(
                    DateTimeOffset.Now,
                    Environment.UserName,
                    Environment.MachineName,
                    "ConfigurationValidation",
                    "Configuration Validation",
                    configurationIssues.Count == 0 ? "Success" : "Needs Attention",
                    configurationIssues.Count == 0 ? "Configuration passed validation." : string.Join("; ", configurationIssues),
                    ErrorMessage: configurationIssues.Count == 0 ? null : string.Join("; ", configurationIssues)));

                var workflowIssues = WorkflowIntegrityValidator.Validate();
                await _logService.LogActionAsync(new ActionLogEntry(
                    DateTimeOffset.Now,
                    Environment.UserName,
                    Environment.MachineName,
                    "WorkflowValidation",
                    "Workflow Integrity Validation",
                    workflowIssues.Count == 0 ? "Success" : "Needs Attention",
                    workflowIssues.Count == 0 ? "All workflows passed integrity checks." : string.Join("; ", workflowIssues),
                    ErrorMessage: workflowIssues.Count == 0 ? null : string.Join("; ", workflowIssues)));

                await new RetentionCleanupService(_settings, _logService).RunAsync();
            }
            catch
            {
                // Startup maintenance is diagnostic only and must never prevent UI startup.
            }
            finally
            {
                try
                {
                    Dispatcher.UIThread.Post(UpdateLogDiagnostics);
                }
                catch (Exception exception)
                {
                    await _logService.LogActionAsync(new ActionLogEntry(
                        DateTimeOffset.Now,
                        Environment.UserName,
                        Environment.MachineName,
                        "StartupDiagnostics",
                        "Startup Diagnostics",
                        "Warning",
                        "Startup diagnostics update failed after the app opened.",
                        ErrorMessage: exception.ToString()));
                }
            }
        });
    }

    private void UpdateLogDiagnostics()
    {
        FallbackLogPathText = _logService.IsUsingFallback ? _logService.FallbackLogPath : string.Empty;
        OnPropertyChanged(nameof(IsFallbackLogVisible));
    }
}
