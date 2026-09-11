using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jadwal.Application.Services;
using Jadwal.Domain.Models;

namespace Jadwal.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _activeSectionTitle = "Today";

    public TodayViewModel TodayVm { get; }
    public CalendarViewModel CalendarVm { get; }
    public TimetableViewModel TimetableVm { get; }
    public TasksViewModel TasksVm { get; }
    public SettingsViewModel SettingsVm { get; }

    private readonly CalendarService _calendarService;
    private DispatcherTimer? _clockTimer;

    [ObservableProperty]
    private string _bigTimeDigits = string.Empty;

    [ObservableProperty]
    private string _bigTimeAmPm = string.Empty;

    [ObservableProperty]
    private string _bigTimeSeconds = string.Empty;

    [ObservableProperty]
    private string _bigTimeString = string.Empty;

    [ObservableProperty]
    private string _bigDateDay = string.Empty;

    [ObservableProperty]
    private string _bigDateArabicDay = string.Empty;

    [ObservableProperty]
    private string _bigDateEnglish = string.Empty;

    [ObservableProperty]
    private string _bigDateHijri = string.Empty;

    [ObservableProperty]
    private string _bigDateHijriArabic = string.Empty;

    public MainViewModel(
        TodayViewModel todayVm,
        CalendarViewModel calendarVm,
        TimetableViewModel timetableVm,
        TasksViewModel tasksVm,
        SettingsViewModel settingsVm,
        CalendarService? calendarService = null)
    {
        TodayVm = todayVm;
        CalendarVm = calendarVm;
        TimetableVm = timetableVm;
        TasksVm = tasksVm;
        SettingsVm = settingsVm;
        _calendarService = calendarService ?? new CalendarService();

        _currentPage = todayVm;
        StartClockTimer();
    }

    public void StartClockTimer()
    {
        if (_clockTimer == null)
        {
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
        }
        UpdateClock();
    }

    public void StopClockTimer()
    {
        _clockTimer?.Stop();
        _clockTimer = null;
    }

    public void UpdateClock()
    {
        var now = DateTime.Now;
        BigTimeDigits = now.ToString("h:mm", CultureInfo.InvariantCulture);
        BigTimeAmPm = now.ToString("tt", CultureInfo.InvariantCulture);
        BigTimeSeconds = now.ToString(":ss", CultureInfo.InvariantCulture);
        BigTimeString = now.ToString("h:mm tt", CultureInfo.InvariantCulture);

        BigDateDay = now.ToString("dddd", CultureInfo.InvariantCulture);
        BigDateEnglish = now.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

        var hijri = _calendarService.GetHijriDate(now);
        BigDateHijri = hijri.ToFormattedString();
        BigDateHijriArabic = hijri.ToArabicString();
        BigDateArabicDay = now.DayOfWeek switch
        {
            DayOfWeek.Monday => "يوم الإثنين",
            DayOfWeek.Tuesday => "يوم الثلاثاء",
            DayOfWeek.Wednesday => "يوم الأربعاء",
            DayOfWeek.Thursday => "يوم الخميس",
            DayOfWeek.Friday => "يوم الجمعة",
            DayOfWeek.Saturday => "يوم السبت",
            DayOfWeek.Sunday => "يوم الأحد",
            _ => string.Empty
        };
    }

    public async Task InitializeAsync()
    {
        await TodayVm.InitializeAsync();
        await CalendarVm.InitializeAsync();
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
    public void NavigateToCalendar()
    {
        CurrentPage = CalendarVm;
        ActiveSectionTitle = "Calendar";
        _ = CalendarVm.InitializeAsync();
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
