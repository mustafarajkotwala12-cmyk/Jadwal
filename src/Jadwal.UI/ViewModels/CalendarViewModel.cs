using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jadwal.Application.DTOs;
using Jadwal.Application.Services;
using Jadwal.Domain.Models;

namespace Jadwal.UI.ViewModels;

public partial class CalendarDayItemViewModel : ObservableObject
{
    public CalendarDayDto Dto { get; }

    public FatimidHijriDate HijriDate => Dto.HijriDate;
    public DateTime GregorianDate => Dto.GregorianDate;

    public int DayNumber => HijriDate.Day;
    public string HijriDayText => DayNumber.ToString();
    public int GregorianDay => GregorianDate.Day;
    public string GregorianDayText => GregorianDay.ToString();
    public string GregorianMonthText => GregorianDate.ToString("MMM");
    public string GregorianDateFormatted => $"{GregorianDate:d MMM yyyy}";

    public bool IsCurrentMonth => Dto.IsCurrentMonth;
    public bool IsToday => Dto.IsToday;
    public bool HasMiqaats => Dto.HasMiqaats;
    public IReadOnlyList<MiqaatItem> Miqaats => Dto.Miqaats;

    public string TopMiqaatTitle => (Miqaats != null && Miqaats.Count > 0) ? Miqaats[0].Title : string.Empty;
    public string MiqaatsCountText => (Miqaats != null && Miqaats.Count > 1) ? $"+{Miqaats.Count - 1} more" : string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(CellBackground));
                OnPropertyChanged(nameof(CellBorderBrush));
                OnPropertyChanged(nameof(CellBorderThickness));
            }
        }
    }

    public string CellBackground => IsSelected ? "#25CFA342" :
        IsToday ? "#150D5C43" :
        !IsCurrentMonth ? "#05000000" : "Transparent";

    public string CellBorderBrush => IsSelected ? "#CFA342" :
        IsToday ? "#0D5C43" :
        HasMiqaats ? "#80B3822E" : "#22C29447";

    public int CellBorderThickness => (IsSelected || IsToday) ? 2 : 1;

    public double CellOpacity => IsCurrentMonth ? 1.0 : 0.4;

    public CalendarDayItemViewModel(CalendarDayDto dto)
    {
        Dto = dto;
    }
}

public partial class CalendarViewModel : ViewModelBase
{
    private readonly CalendarService _calendarService;

    [ObservableProperty]
    private int _displayedHijriYear;

    [ObservableProperty]
    private int _displayedHijriMonth; // 0-11

    [ObservableProperty]
    private string _monthTitle = string.Empty;

    [ObservableProperty]
    private string _monthTitleArabic = string.Empty;

    [ObservableProperty]
    private string _gregorianSpan = string.Empty;

    [ObservableProperty]
    private CalendarDayItemViewModel? _selectedDay;

    [ObservableProperty]
    private string _selectedDayHeader = string.Empty;

    [ObservableProperty]
    private string _selectedDayArabicDate = string.Empty;

    [ObservableProperty]
    private string _selectedDayGregorianDate = string.Empty;

    [ObservableProperty]
    private bool _hasSelectedDayMiqaats;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<CalendarDayItemViewModel> Days { get; } = new();
    public ObservableCollection<MiqaatItem> SelectedDayMiqaats { get; } = new();

    public CalendarViewModel(CalendarService calendarService)
    {
        _calendarService = calendarService;

        var todayHijri = _calendarService.GetHijriDate(DateTime.Today);
        _displayedHijriYear = todayHijri.Year;
        _displayedHijriMonth = todayHijri.Month;
    }

    public async Task InitializeAsync()
    {
        await LoadCalendarMonthAsync();
    }

    [RelayCommand]
    public async Task PreviousMonthAsync()
    {
        if (DisplayedHijriMonth == 0)
        {
            DisplayedHijriYear--;
            DisplayedHijriMonth = 11;
        }
        else
        {
            DisplayedHijriMonth--;
        }

        await LoadCalendarMonthAsync();
    }

    [RelayCommand]
    public async Task NextMonthAsync()
    {
        if (DisplayedHijriMonth == 11)
        {
            DisplayedHijriYear++;
            DisplayedHijriMonth = 0;
        }
        else
        {
            DisplayedHijriMonth++;
        }

        await LoadCalendarMonthAsync();
    }

    [RelayCommand]
    public async Task TodayAsync()
    {
        var todayHijri = _calendarService.GetHijriDate(DateTime.Today);
        DisplayedHijriYear = todayHijri.Year;
        DisplayedHijriMonth = todayHijri.Month;
        await LoadCalendarMonthAsync();

        var todayCard = Days.FirstOrDefault(d => d.IsToday);
        if (todayCard != null)
        {
            SelectDay(todayCard);
        }
    }

    [RelayCommand]
    public void SelectDay(CalendarDayItemViewModel? day)
    {
        if (day == null) return;

        foreach (var d in Days)
        {
            d.IsSelected = (d == day);
        }

        SelectedDay = day;
        SelectedDayHeader = $"{day.HijriDate.Day} {day.HijriDate.MonthNameLong} {day.HijriDate.Year} AH";
        SelectedDayArabicDate = day.HijriDate.ToArabicString();
        SelectedDayGregorianDate = day.GregorianDate.ToString("dddd, d MMMM yyyy");

        SelectedDayMiqaats.Clear();
        if (day.Miqaats != null && day.Miqaats.Count > 0)
        {
            foreach (var m in day.Miqaats)
            {
                SelectedDayMiqaats.Add(m);
            }
            HasSelectedDayMiqaats = true;
        }
        else
        {
            HasSelectedDayMiqaats = false;
        }
    }

    private async Task LoadCalendarMonthAsync()
    {
        IsLoading = true;
        try
        {
            var calendarDto = await _calendarService.BuildMonthCalendarAsync(
                DisplayedHijriYear,
                DisplayedHijriMonth,
                DateTime.Today);

            MonthTitle = $"{calendarDto.HijriMonthName} {calendarDto.HijriYear} AH";
            MonthTitleArabic = $"{calendarDto.HijriMonthArabic} {calendarDto.HijriYear}";
            GregorianSpan = calendarDto.GregorianSpan;

            Days.Clear();
            CalendarDayItemViewModel? dayToSelect = null;

            foreach (var dayDto in calendarDto.Days)
            {
                var vm = new CalendarDayItemViewModel(dayDto);
                Days.Add(vm);

                if (dayDto.IsToday)
                {
                    dayToSelect = vm;
                }
            }

            // Default selection: today if present, otherwise first day of current month
            dayToSelect ??= Days.FirstOrDefault(d => d.IsCurrentMonth && d.DayNumber == 1) ?? Days.FirstOrDefault();
            if (dayToSelect != null)
            {
                SelectDay(dayToSelect);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
