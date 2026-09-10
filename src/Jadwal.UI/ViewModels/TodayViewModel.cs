using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jadwal.Application.DTOs;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Services;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.UI.ViewModels;

public class ClassCardItemViewModel : ObservableObject
{
    public PeriodOccurrence Period { get; }
    public ClassLiveStatus Status { get; set; }

    private bool _isFlipped;
    public bool IsFlipped
    {
        get => _isFlipped;
        set => SetProperty(ref _isFlipped, value);
    }

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public ClassCardItemViewModel(PeriodOccurrence period, ClassLiveStatus status)
    {
        Period = period;
        Status = status;
    }

    public void ToggleFlip()
    {
        IsFlipped = !IsFlipped;
    }

    public string StatusText => Status.Kind switch
    {
        ClassStatusKind.InProgress => "IN SESSION",
        ClassStatusKind.StartingSoon => $"IN {Status.MinutesRemaining}M",
        ClassStatusKind.Completed => "DONE",
        ClassStatusKind.Changed => "CHANGED",
        ClassStatusKind.Cancelled => "CANCELLED",
        _ => "UPCOMING"
    };
}

public class BreakPillItemViewModel
{
    public BreakBlock Break { get; }
    public BreakPillItemViewModel(BreakBlock breakBlock) => Break = breakBlock;

    public string ShortName => Break.Name.Contains("Morning") ? "Morning\nPrep" :
        Break.Name.Contains("Lunch") ? "Lunch &\nNamaz" :
        Break.Name.Contains("Recess") ? "Recess" : Break.Name;

    public string DurationFormatted => ScheduleTimelineBuilder.FormatDuration(Break.DurationMinutes);
}

public partial class TodayViewModel : ViewModelBase
{
    private readonly DashboardService _dashboardService;
    private readonly TaskService _taskService;
    private readonly TimetableService _timetableService;
    private readonly ITimeProvider _timeProvider;

    [ObservableProperty]
    private string _englishDate = string.Empty;

    [ObservableProperty]
    private string _arabicWeekday = string.Empty;

    [ObservableProperty]
    private string _daySubtitle = string.Empty;

    [ObservableProperty]
    private string _currentTimeString = string.Empty;

    [ObservableProperty]
    private string _activeStatusDescription = "No classes active";

    [ObservableProperty]
    private string _row1Title = string.Empty;

    [ObservableProperty]
    private string _row2Title = string.Empty;

    [ObservableProperty]
    private bool _hasChanges = false;

    [ObservableProperty]
    private string _changesSummary = string.Empty;

    [ObservableProperty]
    private bool _isEmptyDay = false;

    public ObservableCollection<object> Row1Items { get; } = new();
    public ObservableCollection<object> Row2Items { get; } = new();

    public TodayViewModel(
        DashboardService dashboardService,
        TaskService taskService,
        TimetableService timetableService,
        ITimeProvider timeProvider)
    {
        _dashboardService = dashboardService;
        _taskService = taskService;
        _timetableService = timetableService;
        _timeProvider = timeProvider;
    }

    public async Task InitializeAsync()
    {
        await RefreshScheduleAsync();
    }

    public void OnTick()
    {
        CurrentTimeString = DateTime.Now.ToString("h:mm:ss tt");
    }

    [RelayCommand]
    public async Task RefreshScheduleAsync()
    {
        var dto = await _dashboardService.GetTodayClassTimetableAsync();
        var allTasks = await _taskService.GetAllTasksAsync();

        EnglishDate = DateTime.Now.ToString("dddd, d MMMM yyyy");
        ArabicWeekday = dto.Day.ToArabicString();
        DaySubtitle = dto.Rule.DaySubtitle;
        CurrentTimeString = DateTime.Now.ToString("h:mm:ss tt");
        Row1Title = dto.Rule.Row1Title;
        Row2Title = dto.Rule.Row2Title;

        if (dto.ActivePeriod != null)
        {
            ActiveStatusDescription = $"Live: {dto.ActivePeriod.PeriodName} ({dto.ActivePeriod.Subject})";
        }
        else if (dto.NextPeriod != null)
        {
            ActiveStatusDescription = $"Next: {dto.NextPeriod.PeriodName} ({dto.NextPeriod.Subject})";
        }
        else
        {
            ActiveStatusDescription = "All classes concluded";
        }

        HasChanges = dto.ActiveChanges.Count > 0;
        ChangesSummary = string.Join(" • ", dto.ActiveChanges.Select(c => c.SummaryMessage));

        Row1Items.Clear();
        foreach (var item in dto.Row1Items)
        {
            if (item.Kind == ScheduleTimelineItemKind.ClassPeriod && item.Period != null)
            {
                var cardVm = new ClassCardItemViewModel(item.Period, item.Status);
                var related = allTasks.Where(t => t.LinkedPeriodId == item.Period.Id || t.LinkedSubject == item.Period.Subject);
                foreach (var t in related) cardVm.Tasks.Add(t);
                Row1Items.Add(cardVm);
            }
            else if (item.Kind == ScheduleTimelineItemKind.BreakBlock && item.Break != null)
            {
                Row1Items.Add(new BreakPillItemViewModel(item.Break));
            }
        }

        Row2Items.Clear();
        foreach (var item in dto.Row2Items)
        {
            if (item.Kind == ScheduleTimelineItemKind.ClassPeriod && item.Period != null)
            {
                var cardVm = new ClassCardItemViewModel(item.Period, item.Status);
                var related = allTasks.Where(t => t.LinkedPeriodId == item.Period.Id || t.LinkedSubject == item.Period.Subject);
                foreach (var t in related) cardVm.Tasks.Add(t);
                Row2Items.Add(cardVm);
            }
            else if (item.Kind == ScheduleTimelineItemKind.BreakBlock && item.Break != null)
            {
                Row2Items.Add(new BreakPillItemViewModel(item.Break));
            }
        }

        IsEmptyDay = Row1Items.Count == 0 && Row2Items.Count == 0;
    }

    [RelayCommand]
    public async Task AcknowledgeChangesAsync()
    {
        await _timetableService.AcknowledgeAllChangesAsync();
        HasChanges = false;
        await RefreshScheduleAsync();
    }
}
