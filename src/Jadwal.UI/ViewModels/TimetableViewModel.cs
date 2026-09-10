using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Services;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;

namespace Jadwal.UI.ViewModels;

public partial class TimetableViewModel : ViewModelBase
{
    private readonly TimetableService _timetableService;
    private readonly TaskService _taskService;
    private readonly ITimeProvider _timeProvider;

    [ObservableProperty]
    private JadwalDayOfWeek _selectedDay = JadwalDayOfWeek.Monday;

    [ObservableProperty]
    private string _dayTitle = "Monday";

    [ObservableProperty]
    private string _dayArabicTitle = "يوم الإثنين";

    [ObservableProperty]
    private string _daySubtitle = "10 Periods • Morning & Afternoon";

    [ObservableProperty]
    private string _row1Title = "Morning Session";

    [ObservableProperty]
    private string _row2Title = "Afternoon Session";

    [ObservableProperty]
    private bool _isEmptyDay = false;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<JadwalDayOfWeek> AvailableDays { get; } = new()
    {
        JadwalDayOfWeek.Monday,
        JadwalDayOfWeek.Tuesday,
        JadwalDayOfWeek.Wednesday,
        JadwalDayOfWeek.Thursday,
        JadwalDayOfWeek.Friday,
        JadwalDayOfWeek.Saturday
    };

    public ObservableCollection<object> Row1Items { get; } = new();
    public ObservableCollection<object> Row2Items { get; } = new();

    public TimetableViewModel(
        TimetableService timetableService,
        TaskService taskService,
        ITimeProvider timeProvider)
    {
        _timetableService = timetableService;
        _taskService = taskService;
        _timeProvider = timeProvider;
    }

    public async Task InitializeAsync()
    {
        SelectedDay = _timeProvider.CurrentDayOfWeek switch
        {
            JadwalDayOfWeek.Sunday => JadwalDayOfWeek.Monday,
            var d => d
        };

        await LoadDayScheduleAsync();
    }

    partial void OnSelectedDayChanged(JadwalDayOfWeek value)
    {
        _ = LoadDayScheduleAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = LoadDayScheduleAsync();
    }

    [RelayCommand]
    public async Task SelectDayAsync(JadwalDayOfWeek day)
    {
        SelectedDay = day;
        await LoadDayScheduleAsync();
    }

    [RelayCommand]
    public async Task LoadDayScheduleAsync()
    {
        var snapshot = await _timetableService.GetLatestTimetableAsync();
        var allTasks = await _taskService.GetAllTasksAsync();
        var rule = DayScheduleRule.RuleFor(SelectedDay);

        DayTitle = SelectedDay.ToString();
        DayArabicTitle = SelectedDay.ToArabicString();
        DaySubtitle = rule.DaySubtitle;
        Row1Title = rule.Row1Title;
        Row2Title = rule.Row2Title;

        Row1Items.Clear();
        Row2Items.Clear();

        if (snapshot == null)
        {
            IsEmptyDay = true;
            return;
        }

        var dayPeriods = snapshot.Periods.Where(p => p.Day == SelectedDay).ToList();
        var timeline = ScheduleTimelineBuilder.BuildTimeline(dayPeriods);
        var (row1, row2) = ScheduleTimelineBuilder.SplitIntoTwoHorizontalRows(timeline, SelectedDay);

        var query = SearchText?.Trim() ?? string.Empty;

        foreach (var item in row1)
        {
            if (item.Kind == ScheduleTimelineItemKind.ClassPeriod && item.Period != null)
            {
                if (!string.IsNullOrEmpty(query) && !MatchesSearch(item.Period, query))
                    continue;

                var cardVm = new ClassCardItemViewModel(item.Period, item.Status);
                var related = allTasks.Where(t => t.LinkedPeriodId == item.Period.Id || t.LinkedSubject == item.Period.Subject);
                foreach (var t in related) cardVm.Tasks.Add(t);
                Row1Items.Add(cardVm);
            }
            else if (item.Kind == ScheduleTimelineItemKind.BreakBlock && item.Break != null)
            {
                if (string.IsNullOrEmpty(query))
                    Row1Items.Add(new BreakPillItemViewModel(item.Break));
            }
        }

        foreach (var item in row2)
        {
            if (item.Kind == ScheduleTimelineItemKind.ClassPeriod && item.Period != null)
            {
                if (!string.IsNullOrEmpty(query) && !MatchesSearch(item.Period, query))
                    continue;

                var cardVm = new ClassCardItemViewModel(item.Period, item.Status);
                var related = allTasks.Where(t => t.LinkedPeriodId == item.Period.Id || t.LinkedSubject == item.Period.Subject);
                foreach (var t in related) cardVm.Tasks.Add(t);
                Row2Items.Add(cardVm);
            }
            else if (item.Kind == ScheduleTimelineItemKind.BreakBlock && item.Break != null)
            {
                if (string.IsNullOrEmpty(query))
                    Row2Items.Add(new BreakPillItemViewModel(item.Break));
            }
        }

        IsEmptyDay = Row1Items.Count == 0 && Row2Items.Count == 0;
    }

    private static bool MatchesSearch(PeriodOccurrence period, string query)
    {
        return period.Subject.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               period.Details.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               period.PeriodName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
