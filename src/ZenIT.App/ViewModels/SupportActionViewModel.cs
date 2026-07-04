using System.Windows.Input;
using ZenIT.Core.Actions;
using ZenIT.Core.Logging;
using ZenIT.Core.Models;
using ZenIT.Core.Services;

namespace ZenIT.App.ViewModels;

public sealed class SupportActionViewModel : ObservableObject
{
    private readonly IActionExecutor _actionExecutor;
    private readonly LogService _logService;
    private readonly DeviceStatus _deviceStatus;
    private string _statusText = string.Empty;
    private string _statusBrush = "#6B7A8A";

    public SupportActionViewModel(ActionDefinition definition, IActionExecutor actionExecutor, LogService logService, DeviceStatus deviceStatus)
    {
        _actionExecutor = actionExecutor;
        _logService = logService;
        _deviceStatus = deviceStatus;
        Id = definition.Id;
        IconText = GetIconText(definition.Id);
        Title = definition.Title;
        Description = definition.Description;
        ButtonText = definition.ButtonText;
        RiskLevel = definition.RiskLevel;
        RequiresAdmin = definition.RequiresAdmin;
        BadgeText = GetBadgeText(definition.Id);
        HasBadge = !string.IsNullOrWhiteSpace(BadgeText);
        CardBorderBrush = definition.Id switch
        {
            ActionId.RequestITHelp => "#9CE2DF",
            ActionId.DeviceHealthCheck => "#BCEDEA",
            _ => "#DCEAF4"
        };
        CardBackground = definition.Id switch
        {
            ActionId.RequestITHelp => "#F4FFFE",
            ActionId.DeviceHealthCheck => "#F7FCFF",
            _ => "#FFFFFF"
        };
        RunCommand = new AsyncRelayCommand(RunAsync);
    }

    public ActionId Id { get; }
    public string IconText { get; }
    public string Title { get; }
    public string Description { get; }
    public string ButtonText { get; }
    public ActionRiskLevel RiskLevel { get; }
    public bool RequiresAdmin { get; }
    public string BadgeText { get; }
    public bool HasBadge { get; }
    public string CardBorderBrush { get; }
    public string CardBackground { get; }
    public ICommand RunCommand { get; }

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

    private async Task RunAsync()
    {
        try
        {
            StatusBrush = "#6B7A8A";
            StatusText = "Running a safe guided check...";

            var result = await _actionExecutor.ExecuteAsync(Id);

            StatusText = result.UserMessage;
            StatusBrush = result.Success ? "#20B486" : "#E5484D";
            await LogAsync(
                result.Success ? "Success" : "Failure",
                result.TechnicalMessage,
                result.ReportPath,
                result.Success ? null : result.TechnicalMessage);
        }
        catch (Exception exception)
        {
            StatusText = "Something did not finish. IT Support can help from here.";
            StatusBrush = "#E5484D";
            await LogAsync("Failure", "Action failed before returning a result.", null, exception.Message);
        }
    }

    private Task LogAsync(string result, string? technicalMessage = null, string? reportPath = null, string? errorMessage = null)
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
            errorMessage));
    }

    private static string GetIconText(ActionId actionId)
    {
        return actionId switch
        {
            ActionId.FixInternet => "NET",
            ActionId.FixZoom => "ZOM",
            ActionId.FixSlack => "SLK",
            ActionId.FixChrome => "CHR",
            ActionId.FixGoogleDrive => "DRV",
            ActionId.DeviceHealthCheck => "HLT",
            ActionId.RequestITHelp => "IT",
            ActionId.FixCamera => "CAM",
            ActionId.FixMicrophone => "MIC",
            ActionId.RestartHelper => "RST",
            _ => "IT"
        };
    }

    private static string GetBadgeText(ActionId actionId)
    {
        return actionId switch
        {
            ActionId.DeviceHealthCheck => "Recommended",
            ActionId.RequestITHelp => "Support",
            _ => string.Empty
        };
    }
}
