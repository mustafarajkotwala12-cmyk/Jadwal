using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Jadwal.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _activeSectionTitle = "Today";

    public TodayViewModel TodayVm { get; }
    public TimetableViewModel TimetableVm { get; }
    public TasksViewModel TasksVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainViewModel(
        TodayViewModel todayVm,
        TimetableViewModel timetableVm,
        TasksViewModel tasksVm,
        SettingsViewModel settingsVm)
    {
        TodayVm = todayVm;
        TimetableVm = timetableVm;
        TasksVm = tasksVm;
        SettingsVm = settingsVm;

        _currentPage = todayVm;
    }

    public async Task InitializeAsync()
    {
        await TodayVm.InitializeAsync();
        await TimetableVm.InitializeAsync();
        await TasksVm.InitializeAsync();
        await SettingsVm.InitializeAsync();
    }

    [RelayCommand]
    public void NavigateToToday()
    {
        CurrentPage = TodayVm;
        ActiveSectionTitle = "Today";
        _ = TodayVm.RefreshScheduleAsync();
    }

    [RelayCommand]
    public void NavigateToTimetable()
    {
        CurrentPage = TimetableVm;
        ActiveSectionTitle = "Timetable";
        _ = TimetableVm.LoadDayScheduleAsync();
    }

    [RelayCommand]
    public void NavigateToTasks()
    {
        CurrentPage = TasksVm;
        ActiveSectionTitle = "Tasks & Discrepancies";
        _ = TasksVm.RefreshTasksAsync();
    }

    [RelayCommand]
    public void NavigateToSettings()
    {
        CurrentPage = SettingsVm;
        ActiveSectionTitle = "Settings";
        _ = SettingsVm.InitializeAsync();
    }
}
