using CommunityToolkit.Mvvm.ComponentModel;
using Jadwal.Application.Services;

namespace Jadwal.UI.ViewModels;

public partial class MenuBarViewModel : ViewModelBase
{
    private readonly DashboardService _dashboardService;

    [ObservableProperty]
    private string _trayTitle = "Jadwal";

    [ObservableProperty]
    private string _currentStatusText = "No active class";

    [ObservableProperty]
    private string _headerSubtitle = string.Empty;

    [ObservableProperty]
    private int _pendingTasksCount = 0;

    [ObservableProperty]
    private bool _hasActiveChanges = false;

    public MenuBarViewModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task RefreshAsync()
    {
        var summary = await _dashboardService.GetMenuBarSummaryAsync();
        HeaderSubtitle = summary.HeaderSubtitle;
        PendingTasksCount = summary.TopPendingTasks.Count;
        HasActiveChanges = summary.HasScheduleChanges;

        if (summary.NextOrCurrentClass != null)
        {
            CurrentStatusText = $"{summary.NextOrCurrentClass.PeriodName}: {summary.NextOrCurrentClass.Subject} ({summary.NextOrCurrentClass.StartTime})";
            TrayTitle = $"Jadwal: {summary.NextOrCurrentClass.Subject}";
        }
        else
        {
            CurrentStatusText = "No active classes";
            TrayTitle = "Jadwal";
        }
    }
}
