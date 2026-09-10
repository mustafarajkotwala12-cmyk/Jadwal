using System;
using System.Collections.Generic;
using Jadwal.Domain.Models;

namespace Jadwal.Application.DTOs;

public record CalendarDayDto(
    FatimidHijriDate HijriDate,
    DateTime GregorianDate,
    IReadOnlyList<MiqaatItem> Miqaats,
    bool IsCurrentMonth,
    bool IsToday
)
{
    public bool HasMiqaats => Miqaats != null && Miqaats.Count > 0;
    public string HijriDayText => HijriDate.Day.ToString();
    public string GregorianDayText => GregorianDate.Day.ToString();
    public string GregorianMonthText => GregorianDate.ToString("MMM");
}

public record MonthCalendarDto(
    int HijriYear,
    int HijriMonth,
    string HijriMonthName,
    string HijriMonthArabic,
    string GregorianSpan,
    IReadOnlyList<CalendarDayDto> Days
);
