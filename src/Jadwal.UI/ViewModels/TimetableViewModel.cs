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

    public ObservableCollection<DayTabViewModel> DayTabs { get; } = new()
    {
        new(JadwalDayOfWeek.Monday),
        new(JadwalDayOfWeek.Tuesday),
        new(JadwalDayOfWeek.Wednesday),
        new(JadwalDayOfWeek.Thursday),
        new(JadwalDayOfWeek.Friday),
        new(JadwalDayOfWeek.Saturday)
    };

    [ObservableProperty]
    private ClassCardItemViewModel? _physicalEducationCard;

    [ObservableProperty]
    private bool _hasPhysicalEducation = false;

    [ObservableProperty]
    private BreakBarViewModel? _break1;

    [ObservableProperty]
    private bool _hasBreak1 = false;

    [ObservableProperty]
    private BreakBarViewModel? _break2;

    [ObservableProperty]
    private bool _hasBreak2 = false;

    [ObservableProperty]
    private bool _hasRow3 = false;

    public ObservableCollection<ClassCardItemViewModel> Row1Cards { get; } = new();
    public ObservableCollection<ClassCardItemViewModel> Row2Cards { get; } = new();
    public ObservableCollection<ClassCardItemViewModel> Row3Cards { get; } = new();

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

    private Avalonia.Threading.DispatcherTimer? _clockTimer;
    private JadwalDayOfWeek _currentLoadedDay;

    public async Task InitializeAsync()
    {
        _currentLoadedDay = _timeProvider.CurrentDayOfWeek switch
        {
            JadwalDayOfWeek.Sunday => JadwalDayOfWeek.Monday,
            var d => d
        };
        SelectedDay = _currentLoadedDay;

        await LoadDayScheduleAsync();
        StartClockTimer();
    }

    public void StartClockTimer()
    {
        if (_clockTimer == null)
        {
            _clockTimer = new Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => OnTick();
            _clockTimer.Start();
        }
    }

    public void StopClockTimer()
    {
        _clockTimer?.Stop();
        _clockTimer = null;
    }

    private void OnTick()
    {
        var currentDay = _timeProvider.CurrentDayOfWeek switch
        {
            JadwalDayOfWeek.Sunday => JadwalDayOfWeek.Monday,
            var d => d
        };

        if (currentDay != _currentLoadedDay)
        {
            _currentLoadedDay = currentDay;
            _ = LoadDayScheduleAsync();
            return;
        }

        // When viewing today's day schedule, sync live card statuses with current computer time
        if (SelectedDay == _timeProvider.CurrentDayOfWeek)
        {
            var now = _timeProvider.CurrentTime;
            PhysicalEducationCard?.UpdateStatus(now);
            foreach (var card in Row1Cards) card.UpdateStatus(now);
            foreach (var card in Row2Cards) card.UpdateStatus(now);
            foreach (var card in Row3Cards) card.UpdateStatus(now);
        }
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
    public async Task SelectDayTabAsync(DayTabViewModel tab)
    {
        if (tab == null) return;
        SelectedDay = tab.Day;
        await LoadDayScheduleAsync();
    }

    [RelayCommand]
    public async Task LoadDayScheduleAsync()
    {
        Row1Items.Clear();
        Row2Items.Clear();

        DayTitle = SelectedDay.ToEnglishString();
        DayArabicTitle = SelectedDay.ToArabicString();

        var rule = DayScheduleRule.RuleFor(SelectedDay);
        DaySubtitle = rule.DaySubtitle;
        Row1Title = rule.Row1Title;
        Row2Title = rule.Row2Title;

        var snapshot = await _timetableService.GetLatestTimetableAsync();
        var allTasks = await _taskService.GetAllTasksAsync();

        if (snapshot == null)
        {
            IsEmptyDay = true;
            return;
        }

        var dayPeriods = snapshot.Periods.Where(p => p.Day == SelectedDay).ToList();
        var three = ScheduleTimelineBuilder.BuildThreeRowSchedule(dayPeriods, SelectedDay);
        var query = SearchText?.Trim() ?? string.Empty;

        Row1Cards.Clear();
        Row2Cards.Clear();
        Row3Cards.Clear();

        // Physical Education Slot (Enabled on Mon-Thu in small slot; omitted completely on Friday)
        if (three.HasPhysicalEducation && three.PhysicalEducationItem?.Period != null)
        {
            if (string.IsNullOrEmpty(query) || MatchesSearch(three.PhysicalEducationItem.Period, query))
            {
                var peCard = new ClassCardItemViewModel(three.PhysicalEducationItem.Period, three.PhysicalEducationItem.Status, _taskService);
                var peTasks = allTasks.Where(t => IsTaskRelatedToPeriod(t, three.PhysicalEducationItem.Period));
                foreach (var t in peTasks) peCard.Tasks.Add(t);
                PhysicalEducationCard = peCard;
                HasPhysicalEducation = true;
            }
            else
            {
                PhysicalEducationCard = null;
                HasPhysicalEducation = false;
            }
        }
        else
        {
            PhysicalEducationCard = null;
            HasPhysicalEducation = false;
        }

        // Row 1 Cards (RTL: right-to-left layout)
        var r1List = new List<ClassCardItemViewModel>();
        foreach (var item in three.Row1Items)
        {
            if (item.Period != null)
            {
                if (!string.IsNullOrEmpty(query) && !MatchesSearch(item.Period, query))
                    continue;

                var cardVm = new ClassCardItemViewModel(item.Period, item.Status, _taskService);
                var related = allTasks.Where(t => IsTaskRelatedToPeriod(t, item.Period));
                foreach (var t in related) cardVm.Tasks.Add(t);
                r1List.Add(cardVm);
            }
        }
        r1List.Reverse();
        foreach (var card in r1List)
        {
            Row1Cards.Add(card);
        }

        // Break 1 (Recess Break)
        if (three.Break1 != null && string.IsNullOrEmpty(query))
        {
            Break1 = new BreakBarViewModel(three.Break1);
            HasBreak1 = true;
        }
        else
        {
            Break1 = null;
            HasBreak1 = false;
        }

        // Row 2 Cards (RTL: right-to-left layout)
        var r2List = new List<ClassCardItemViewModel>();
        foreach (var item in three.Row2Items)
        {
            if (item.Period != null)
            {
                if (!string.IsNullOrEmpty(query) && !MatchesSearch(item.Period, query))
                    continue;

                var cardVm = new ClassCardItemViewModel(item.Period, item.Status, _taskService);
                var related = allTasks.Where(t => IsTaskRelatedToPeriod(t, item.Period));
                foreach (var t in related) cardVm.Tasks.Add(t);
                r2List.Add(cardVm);
            }
        }
        r2List.Reverse();
        foreach (var card in r2List)
        {
            Row2Cards.Add(card);
        }

        // Break 2 (Lunch & Namaz Break)
        if (three.Break2 != null && string.IsNullOrEmpty(query))
        {
            Break2 = new BreakBarViewModel(three.Break2);
            HasBreak2 = true;
        }
        else
        {
            Break2 = null;
            HasBreak2 = false;
        }

        // Row 3 Cards (RTL: right-to-left layout)
        var r3List = new List<ClassCardItemViewModel>();
        foreach (var item in three.Row3Items)
        {
            if (item.Period != null)
            {
                if (!string.IsNullOrEmpty(query) && !MatchesSearch(item.Period, query))
                    continue;

                var cardVm = new ClassCardItemViewModel(item.Period, item.Status, _taskService);
                var related = allTasks.Where(t => IsTaskRelatedToPeriod(t, item.Period));
                foreach (var t in related) cardVm.Tasks.Add(t);
                r3List.Add(cardVm);
            }
        }
        r3List.Reverse();
        foreach (var card in r3List)
        {
            Row3Cards.Add(card);
        }
        HasRow3 = Row3Cards.Count > 0;

        // Backwards compatibility for Row1Items and Row2Items
        var timeline = ScheduleTimelineBuilder.BuildTimeline(dayPeriods);
        var (row1, row2) = ScheduleTimelineBuilder.SplitIntoTwoHorizontalRows(timeline, SelectedDay);

        foreach (var item in row1)
        {
            if (item.Kind == ScheduleTimelineItemKind.ClassPeriod && item.Period != null)
            {
                if (!string.IsNullOrEmpty(query) && !MatchesSearch(item.Period, query))
                    continue;

                var cardVm = new ClassCardItemViewModel(item.Period, item.Status, _taskService);
                var related = allTasks.Where(t => IsTaskRelatedToPeriod(t, item.Period));
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

                var cardVm = new ClassCardItemViewModel(item.Period, item.Status, _taskService);
                var related = allTasks.Where(t => IsTaskRelatedToPeriod(t, item.Period));
                foreach (var t in related) cardVm.Tasks.Add(t);
                Row2Items.Add(cardVm);
            }
            else if (item.Kind == ScheduleTimelineItemKind.BreakBlock && item.Break != null)
            {
                if (string.IsNullOrEmpty(query))
                    Row2Items.Add(new BreakPillItemViewModel(item.Break));
            }
        }

        IsEmptyDay = Row1Cards.Count == 0 && Row2Cards.Count == 0;
    }

    private static bool IsTaskRelatedToPeriod(TaskItem task, PeriodOccurrence period)
    {
        if (!string.IsNullOrEmpty(task.LinkedPeriodId) && task.LinkedPeriodId == period.Id)
            return true;

        if (string.IsNullOrWhiteSpace(task.LinkedSubject) || string.IsNullOrWhiteSpace(period.Subject))
            return false;

        var taskSub = task.LinkedSubject.Trim();
        var periodSub = period.Subject.Trim();

        return string.Equals(taskSub, periodSub, StringComparison.OrdinalIgnoreCase) ||
               taskSub.Contains(periodSub, StringComparison.OrdinalIgnoreCase) ||
               periodSub.Contains(taskSub, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSearch(PeriodOccurrence period, string query)
    {
        return period.Subject.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               period.Details.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               period.PeriodName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

public class DayTabViewModel
{
    public JadwalDayOfWeek Day { get; }
    public string ArabicName { get; }
    public string EnglishName { get; }

    public DayTabViewModel(JadwalDayOfWeek day)
    {
        Day = day;
        ArabicName = day.ToArabicString().Replace("يوم ", "");
        EnglishName = day.ToEnglishString();
    }
}

