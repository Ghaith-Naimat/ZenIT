using System.Windows.Input;
using ZenIT.Core.Logging;
using ZenIT.Core.Models;
using ZenIT.Core.Services;
using ZenIT.Core.Workflows;

namespace ZenIT.Mac.App.ViewModels;

public sealed class WorkflowCardViewModel : ObservableObject
{
    private readonly IWorkflowExecutionQueue _workflowQueue;
    private readonly LogService _logService;
    private readonly DeviceStatus _deviceStatus;
    private readonly Func<WorkflowCardViewModel, Task<bool>>? _confirmationHandler;
    private readonly Action<string, string>? _toastHandler;
    private Func<string, string> _localize = key => key;
    private string _statusText = string.Empty;
    private string _statusBrush = "#6B7A8A";
    private string _title;
    private string _description;
    private string _buttonText;
    private string _category;
    private bool _isRunning;

    public WorkflowCardViewModel(
        WorkflowDefinition definition,
        IWorkflowExecutor workflowExecutor,
        LogService logService,
        DeviceStatus deviceStatus,
        Func<WorkflowCardViewModel, Task<bool>>? confirmationHandler = null,
        Action<string, string>? toastHandler = null)
    {
        _workflowQueue = new ImmediateWorkflowExecutionQueue(workflowExecutor);
        _logService = logService;
        _deviceStatus = deviceStatus;
        _confirmationHandler = confirmationHandler;
        _toastHandler = toastHandler;

        Id = definition.Id;
        IconText = definition.IconCode;
        _title = definition.Title;
        _description = definition.Description;
        _buttonText = definition.ButtonText;
        _category = definition.Category;
        RiskLevel = definition.RiskLevel;
        AccessTier = definition.AccessTier;
        RequiresConfirmation = definition.RequiresConfirmation;
        RequiresITMode = definition.RequiresITMode;
        RequiresAdmin = definition.RequiresAdmin;
        BadgeText = GetBadgeText(definition);
        HasBadge = !string.IsNullOrWhiteSpace(BadgeText);
        CardBorderBrush = GetCardBorderBrush(definition);
        CardBackground = GetCardBackground(definition);
        RunCommand = new AsyncRelayCommand(RunAsync);
    }

    public WorkflowId Id { get; }
    public string IconText { get; }
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public string ButtonText
    {
        get => _buttonText;
        private set => SetProperty(ref _buttonText, value);
    }

    public string Category
    {
        get => _category;
        private set => SetProperty(ref _category, value);
    }
    public WorkflowRiskLevel RiskLevel { get; }
    public WorkflowAccessTier AccessTier { get; }
    public bool RequiresConfirmation { get; }
    public bool RequiresITMode { get; }
    public bool RequiresAdmin { get; }
    public string BadgeText { get; }
    public bool HasBadge { get; }
    public string CardBorderBrush { get; }
    public string CardBackground { get; }
    public ICommand RunCommand { get; }
    public string RiskText => RiskLevel.ToString();
    public bool IsITWorkflow => AccessTier == WorkflowAccessTier.IT;

    public void ApplyDisplayText(string title, string description, string buttonText, string category)
    {
        Title = title;
        Description = description;
        ButtonText = buttonText;
        Category = category;
    }

    public void ApplyLocalization(Func<string, string> localize)
    {
        _localize = localize;
    }

    public void ApplyReadyText(string readyText)
    {
        if (!IsRunning && string.IsNullOrWhiteSpace(_statusText))
        {
            StatusText = string.Empty;
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    private async Task RunAsync()
    {
        try
        {
            var hasConfirmationConsent = false;
            if (RequiresConfirmation && _confirmationHandler is not null)
            {
                hasConfirmationConsent = await _confirmationHandler(this);
                if (!hasConfirmationConsent)
                {
                    StatusBrush = "#6B7A8A";
                    StatusText = _localize("Status.Canceled");
                    return;
                }
            }

            using var consent = hasConfirmationConsent ? WorkflowExecutionConsent.Grant(Id) : null;

            IsRunning = true;
            StatusBrush = "#6B7A8A";
            StatusText = _localize("Status.Running");

            var result = await _workflowQueue.EnqueueAsync(new WorkflowExecutionRequest(Id, DateTimeOffset.Now, AccessTier.ToString()));

            var durationText = FormatDuration(result.FinishedAt - result.StartedAt);
            var statusLabel = result.Outcome switch
            {
                WorkflowOutcome.Success => _localize("Status.Completed"),
                WorkflowOutcome.RepairAttempted => _localize("Status.RepairAttempted"),
                WorkflowOutcome.NeedsIT => _localize("Status.NeedsIT"),
                WorkflowOutcome.CannotVerify => _localize("Status.CannotVerify"),
                _ => _localize("Status.Error")
            };
            StatusText = $"{statusLabel}\n{result.UserMessage}\n{_localize("Status.Duration")}: {durationText}";
            StatusBrush = result.Outcome == WorkflowOutcome.Success ? "#20B486" : result.Outcome == WorkflowOutcome.NeedsIT ? "#F5A623" : "#E5484D";
            _toastHandler?.Invoke(result.UserMessage, StatusBrush);
            await LogAsync(
                FormatOutcome(result.Outcome),
                result.TechnicalMessage,
                result.ReportPath,
                result.Outcome == WorkflowOutcome.Success ? null : result.TechnicalMessage,
                result.FinishedAt - result.StartedAt);
        }
        catch (Exception exception)
        {
            StatusText = _localize("Status.Error");
            StatusBrush = "#E5484D";
            _toastHandler?.Invoke(StatusText, StatusBrush);
            await LogAsync("Failure", "Workflow failed before returning a result.", null, exception.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private Task LogAsync(string result, string? technicalMessage, string? reportPath, string? errorMessage, TimeSpan? duration = null)
    {
        return _logService.LogActionAsync(new ActionLogEntry(
            DateTimeOffset.Now,
            _deviceStatus.CurrentUser,
            _deviceStatus.DeviceName,
            Id.ToString(),
            Title,
            result,
            technicalMessage,
            reportPath,
            errorMessage,
            duration));
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:0.0} seconds"
            : $"{duration.TotalMinutes:0.0} minutes";
    }

    private static string FormatOutcome(WorkflowOutcome outcome)
    {
        return outcome switch
        {
            WorkflowOutcome.Success => "Success",
            WorkflowOutcome.RepairAttempted => "Repair Attempted",
            WorkflowOutcome.NeedsIT => "Needs IT",
            WorkflowOutcome.CannotVerify => "Cannot Verify",
            _ => "Failure"
        };
    }

    private static string GetBadgeText(WorkflowDefinition definition)
    {
        if (definition.Id == WorkflowId.SecurityCheck)
        {
            return "Security";
        }

        if (definition.Id == WorkflowId.CollectITReport)
        {
            return "IT Report";
        }

        if (definition.Id == WorkflowId.ContactIT)
        {
            return "Support";
        }

        if (definition.AccessTier == WorkflowAccessTier.IT)
        {
            return definition.RiskLevel.ToString();
        }

        return definition.IsRecommended ? "Recommended" : string.Empty;
    }

    private static string GetCardBorderBrush(WorkflowDefinition definition)
    {
        return definition.Id switch
        {
            WorkflowId.ContactIT => "#9CE2DF",
            WorkflowId.CollectITReport => "#BCEDEA",
            WorkflowId.DeviceHealthCheck => "#BCEDEA",
            WorkflowId.SecurityCheck => "#F8D99D",
            _ when definition.AccessTier == WorkflowAccessTier.IT => definition.RiskLevel == WorkflowRiskLevel.High ? "#F0B4B6" : "#F8D99D",
            _ => "#DCEAF4"
        };
    }

    private static string GetCardBackground(WorkflowDefinition definition)
    {
        return definition.Id switch
        {
            WorkflowId.ContactIT => "#F4FFFE",
            WorkflowId.CollectITReport => "#F7FCFF",
            WorkflowId.DeviceHealthCheck => "#F7FCFF",
            WorkflowId.SecurityCheck => "#FFF9EE",
            _ when definition.AccessTier == WorkflowAccessTier.IT => definition.RiskLevel == WorkflowRiskLevel.High ? "#FFF7F7" : "#FFF9EE",
            _ => "#FFFFFF"
        };
    }
}
