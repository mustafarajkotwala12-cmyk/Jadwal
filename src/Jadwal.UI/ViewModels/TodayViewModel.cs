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
    private readonly TaskService? _taskService;
    private readonly Action? _onTaskChanged;

    public PeriodOccurrence Period { get; }
    
    private ClassLiveStatus _status;
    public ClassLiveStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBadgeBackground));
                OnPropertyChanged(nameof(StatusBadgeForeground));
                OnPropertyChanged(nameof(StatusBadgeBorder));
            }
        }
    }

    public string StartTime12H => PeriodOccurrence.FormatTo12Hour(Period.StartTime);
    public string EndTime12H => PeriodOccurrence.FormatTo12Hour(Period.EndTime);
    public string EndTimeDisplay => $"Ends {EndTime12H}";
    public string TimeRangeFormatted => $"{StartTime12H} – {EndTime12H}";

    public void UpdateStatus(TimeOnly now)
    {
        if (Period.ChangeRecord != null)
        {
            if (TimeOnly.TryParse(Period.EndTime, out var end) && now >= end)
            {
                Status = ClassLiveStatus.Completed;
            }
            else
            {
                Status = ClassLiveStatus.Changed;
            }
        }
        else
        {
            Status = ClassLiveStatus.Compute(Period.StartTime, Period.EndTime, now);
        }
    }

    private bool _isFlipped;
    public bool IsFlipped
    {
        get => _isFlipped;
        set => SetProperty(ref _isFlipped, value);
    }

    private string _newTaskTitle = string.Empty;
    public string NewTaskTitle
    {
        get => _newTaskTitle;
        set => SetProperty(ref _newTaskTitle, value);
    }

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public ClassCardItemViewModel(
        PeriodOccurrence period,
        ClassLiveStatus status,
        TaskService? taskService = null,
        Action? onTaskChanged = null)
    {
        Period = period;
        _status = status;
        _taskService = taskService;
        _onTaskChanged = onTaskChanged;

        AddTaskCommand = new AsyncRelayCommand(AddTaskAsync);
        ToggleTaskCompletionCommand = new AsyncRelayCommand<TaskItem>(ToggleTaskCompletionAsync);
        DeleteTaskCommand = new AsyncRelayCommand<TaskItem>(DeleteTaskAsync);
    }

    public void ToggleFlip()
    {
        IsFlipped = !IsFlipped;
    }

    public IAsyncRelayCommand AddTaskCommand { get; }
    public IAsyncRelayCommand<TaskItem> ToggleTaskCompletionCommand { get; }
    public IAsyncRelayCommand<TaskItem> DeleteTaskCommand { get; }

    public async Task AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        var title = NewTaskTitle.Trim();
        NewTaskTitle = string.Empty;

        if (_taskService != null)
        {
            var task = await _taskService.CreateTaskAsync(
                title: title,
                linkedSubject: Period.Subject,
                linkedPeriodId: Period.Id,
                priority: TaskPriority.Medium,
                category: TaskCategory.Academic
            );
            Tasks.Add(task);
        }
        else
        {
            var local = new TaskItem
            {
                Title = title,
                LinkedSubject = Period.Subject,
                LinkedPeriodId = Period.Id,
                Priority = TaskPriority.Medium,
                Category = TaskCategory.Academic
            };
            Tasks.Add(local);
        }

        OnPropertyChanged(nameof(TasksCountText));
        _onTaskChanged?.Invoke();
    }

    public async Task ToggleTaskCompletionAsync(TaskItem? task)
    {
        if (task == null) return;
        if (_taskService != null)
        {
            var updated = await _taskService.ToggleTaskCompletionAsync(task.Id);
            if (updated != null)
            {
                task.IsCompleted = updated.IsCompleted;
            }
        }
        else
        {
            task.IsCompleted = !task.IsCompleted;
        }
        OnPropertyChanged(nameof(TasksCountText));
        _onTaskChanged?.Invoke();
    }

    public async Task DeleteTaskAsync(TaskItem? task)
    {
        if (task == null) return;
        Tasks.Remove(task);
        if (_taskService != null)
        {
            await _taskService.DeleteTaskAsync(task.Id);
        }
        OnPropertyChanged(nameof(TasksCountText));
        _onTaskChanged?.Invoke();
    }

    public string TasksCountText => Tasks.Count == 1 ? "1 task" : $"{Tasks.Count} tasks";

    public string StatusText => Status.Kind switch
    {
        ClassStatusKind.InProgress => "IN SESSION",
        ClassStatusKind.StartingSoon => $"IN {Status.MinutesRemaining}M",
        ClassStatusKind.Completed => "DONE",
        ClassStatusKind.Changed => "CHANGED",
        ClassStatusKind.Cancelled => "CANCELLED",
        _ => "UPCOMING"
    };

    public string StatusBadgeBackground => Status.Kind switch
    {
        ClassStatusKind.InProgress => "#D4EDDA",
        ClassStatusKind.StartingSoon => "#CCE5FF",
        ClassStatusKind.Completed => "#E9ECEF",
        ClassStatusKind.Changed => "#FFF3CD",
        ClassStatusKind.Cancelled => "#F8D7DA",
        _ => "#F8F4EC"
    };

    public string StatusBadgeForeground => Status.Kind switch
    {
        ClassStatusKind.InProgress => "#155724",
        ClassStatusKind.StartingSoon => "#004085",
        ClassStatusKind.Completed => "#495057",
        ClassStatusKind.Changed => "#856404",
        ClassStatusKind.Cancelled => "#721C24",
        _ => "#8C6D37"
    };

    public string StatusBadgeBorder => Status.Kind switch
    {
        ClassStatusKind.InProgress => "#C3E6CB",
        ClassStatusKind.StartingSoon => "#B8DAFF",
        ClassStatusKind.Completed => "#CED4DA",
        ClassStatusKind.Changed => "#FFEEBA",
        ClassStatusKind.Cancelled => "#F5C6CB",
        _ => "#DFD5C2"
    };
}

public class BreakPillItemViewModel
{
    public BreakBlock Break { get; }
    public BreakPillItemViewModel(BreakBlock breakBlock) => Break = breakBlock;

    public string ShortName => Break.Name.Contains("Morning") ? "Morning\nPrep" :
        Break.Name.Contains("Lunch") ? "Lunch &\nNamaz" :
        Break.Name.Contains("Recess") ? "Recess" : Break.Name;

    public string Icon => (Break.Name.Contains("Lunch", StringComparison.OrdinalIgnoreCase) || Break.Name.Contains("Namaz", StringComparison.OrdinalIgnoreCase)) ? "🕌" :
        Break.Name.Contains("Morning", StringComparison.OrdinalIgnoreCase) ? "🌅" :
        Break.Name.Contains("Recess", StringComparison.OrdinalIgnoreCase) ? "☕" : "⏸️";

    public string TimeRange => $"{PeriodOccurrence.FormatTo12Hour(Break.StartTime)} – {PeriodOccurrence.FormatTo12Hour(Break.EndTime)}";

    public string DurationFormatted => ScheduleTimelineBuilder.FormatDuration(Break.DurationMinutes);
}

public partial class TodayViewModel : ViewModelBase
{
    private readonly DashboardService _dashboardService;
    private readonly TaskService _taskService;
    private readonly ITimeProvider _timeProvider;
    private readonly TimetableService? _timetableService;

    private readonly CalendarService _calendarService;

    private int _tickCount;
    private JadwalDayOfWeek _loadedDay;

    [ObservableProperty]
    private string _currentDateString = string.Empty;

    [ObservableProperty]
    private string _currentTimeString = string.Empty;

    [ObservableProperty]
    private string _arabicWeekday = string.Empty;

    [ObservableProperty]
    private string _englishDate = string.Empty;

    [ObservableProperty]
    private string _hijriDateFormatted = string.Empty;

    [ObservableProperty]
    private string _hijriDateArabic = string.Empty;

    [ObservableProperty]
    private bool _hasTodayMiqaats;

    [ObservableProperty]
    private string _todayMiqaatsSummary = string.Empty;

    public ObservableCollection<MiqaatItem> TodayMiqaats { get; } = new();

    [ObservableProperty]
    private string _daySubtitle = string.Empty;

    [ObservableProperty]
    private string _row1Title = string.Empty;

    [ObservableProperty]
    private string _row2Title = string.Empty;

    [ObservableProperty]
    private string _activeStatusDescription = string.Empty;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string _changesSummary = string.Empty;

    [ObservableProperty]
    private bool _hasPhysicalEducation;

    [ObservableProperty]
    private ClassCardItemViewModel? _physicalEducationCard;

    [ObservableProperty]
    private BreakBarViewModel? _break1;

    [ObservableProperty]
    private bool _hasBreak1;

    [ObservableProperty]
    private BreakBarViewModel? _break2;

    [ObservableProperty]
    private bool _hasBreak2;

    [ObservableProperty]
    private bool _hasRow3;

    [ObservableProperty]
    private bool _isEmptyDay;

    public ObservableCollection<ClassCardItemViewModel> Row1Cards { get; } = new();
    public ObservableCollection<ClassCardItemViewModel> Row2Cards { get; } = new();
    public ObservableCollection<ClassCardItemViewModel> Row3Cards { get; } = new();

    public ObservableCollection<object> Row1Items { get; } = new();
    public ObservableCollection<object> Row2Items { get; } = new();

    private Avalonia.Threading.DispatcherTimer? _clockTimer;

    public TodayViewModel(
        DashboardService dashboardService,
        TaskService taskService,
        TimetableService timetableService,
        ITimeProvider timeProvider,
        CalendarService? calendarService = null)
    {
        _dashboardService = dashboardService;
        _taskService = taskService;
        _timetableService = timetableService;
        _timeProvider = timeProvider;
        _calendarService = calendarService ?? new CalendarService();

        CurrentTimeString = DateTime.Now.ToString("h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task InitializeAsync()
    {
        await RefreshScheduleAsync();
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

    public void OnTick()
    {
        CurrentTimeString = DateTime.Now.ToString("h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
        _tickCount++;

        // Sync date/day change if computer clock crossed midnight
        if (_timeProvider.CurrentDayOfWeek != _loadedDay)
        {
            _ = RefreshScheduleAsync();
            return;
        }

        // Keep all card statuses updated in sync with computer clock every 5 seconds
        if (_tickCount % 5 == 0)
        {
            UpdateLiveStatuses();
        }

        // Full refresh periodically (every 30 seconds)
        if (_tickCount % 30 == 0)
        {
            _ = RefreshScheduleAsync();
        }
    }

    public void UpdateLiveStatuses()
    {
        var now = _timeProvider.CurrentTime;
        PhysicalEducationCard?.UpdateStatus(now);
        foreach (var card in Row1Cards) card.UpdateStatus(now);
        foreach (var card in Row2Cards) card.UpdateStatus(now);
        foreach (var card in Row3Cards) card.UpdateStatus(now);
    }

    [RelayCommand]
    public async Task RefreshScheduleAsync()
    {
        var dto = await _dashboardService.GetTodayClassTimetableAsync();
        var allTasks = await _taskService.GetAllTasksAsync();

        _loadedDay = dto.Day;
        EnglishDate = DateTime.Now.ToString("dddd, d MMMM yyyy");
        ArabicWeekday = dto.Day.ToArabicString();
        DaySubtitle = dto.Rule.DaySubtitle;
        CurrentTimeString = DateTime.Now.ToString("h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
        Row1Title = dto.Rule.Row1Title;
        Row2Title = dto.Rule.Row2Title;

        // Calculate Fatimid Hijri date & fetch today's Miqaats
        var todayHijri = _calendarService.GetHijriDate(DateTime.Today);
        HijriDateFormatted = todayHijri.ToFormattedString();
        HijriDateArabic = todayHijri.ToArabicString();

        var miqaats = await _calendarService.GetMiqaatsForHijriDateAsync(todayHijri.Month, todayHijri.Day);
        TodayMiqaats.Clear();
        if (miqaats != null && miqaats.Count > 0)
        {
            foreach (var m in miqaats)
            {
                TodayMiqaats.Add(m);
            }
            HasTodayMiqaats = true;
            TodayMiqaatsSummary = string.Join(" • ", miqaats.Select(m => m.Title));
        }
        else
        {
            HasTodayMiqaats = false;
            TodayMiqaatsSummary = "No specific miqaat recorded for today";
        }

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

        Row1Cards.Clear();
        Row2Cards.Clear();
        Row3Cards.Clear();

        if (dto.ThreeRowSchedule != null)
        {
            var three = dto.ThreeRowSchedule;

            // Physical Education Slot (Shown on Mon-Thu in small slot; omitted on Friday)
            if (three.HasPhysicalEducation && three.PhysicalEducationItem?.Period != null)
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

            // Row 1 Cards (RTL: right-to-left layout)
            var r1List = new List<ClassCardItemViewModel>();
            foreach (var item in three.Row1Items)
            {
                if (item.Period != null)
                {
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
            if (three.Break1 != null)
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
            if (three.Break2 != null)
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
        }

        // Backwards compatibility for Row1Items and Row2Items
        Row1Items.Clear();
        foreach (var item in dto.Row1Items)
        {
            if (item.Kind == ScheduleTimelineItemKind.ClassPeriod && item.Period != null)
            {
                var cardVm = new ClassCardItemViewModel(item.Period, item.Status, _taskService);
                var related = allTasks.Where(t => IsTaskRelatedToPeriod(t, item.Period));
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
                var cardVm = new ClassCardItemViewModel(item.Period, item.Status, _taskService);
                var related = allTasks.Where(t => IsTaskRelatedToPeriod(t, item.Period));
                foreach (var t in related) cardVm.Tasks.Add(t);
                Row2Items.Add(cardVm);
            }
            else if (item.Kind == ScheduleTimelineItemKind.BreakBlock && item.Break != null)
            {
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

    [RelayCommand]
    public async Task AcknowledgeChangesAsync()
    {
        if (_timetableService != null)
        {
            await _timetableService.AcknowledgeAllChangesAsync();
        }
        HasChanges = false;
        await RefreshScheduleAsync();
    }
}
