using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jadwal.Application.DTOs;
using Jadwal.Application.Interfaces;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Services;

public class NullMiqaatRepository : IMiqaatRepository
{
    public Task<IReadOnlyList<DayMiqaatsRecord>> GetAllMiqaatsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DayMiqaatsRecord>>(Array.Empty<DayMiqaatsRecord>());

    public Task<IReadOnlyList<MiqaatItem>> GetMiqaatsForHijriDayAsync(int monthZeroIndexed, int day, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MiqaatItem>>(Array.Empty<MiqaatItem>());
}

public class CalendarService
{
    private readonly IMiqaatRepository _miqaatRepository;

    public CalendarService(IMiqaatRepository? miqaatRepository = null)
    {
        _miqaatRepository = miqaatRepository ?? new NullMiqaatRepository();
    }

    public FatimidHijriDate GetHijriDate(DateTime gregorianDate)
    {
        return FatimidHijriDate.FromGregorian(gregorianDate);
    }

    public DateTime GetGregorianDate(FatimidHijriDate hijriDate)
    {
        return hijriDate.ToGregorian();
    }

    public async Task<IReadOnlyList<MiqaatItem>> GetMiqaatsForHijriDateAsync(int monthZeroIndexed, int day, CancellationToken ct = default)
    {
        return await _miqaatRepository.GetMiqaatsForHijriDayAsync(monthZeroIndexed, day, ct);
    }

    public async Task<IReadOnlyList<MiqaatItem>> GetMiqaatsForGregorianDateAsync(DateTime date, CancellationToken ct = default)
    {
        var hijri = GetHijriDate(date);
        return await _miqaatRepository.GetMiqaatsForHijriDayAsync(hijri.Month, hijri.Day, ct);
    }

    public async Task<MonthCalendarDto> BuildMonthCalendarAsync(
        int hijriYear,
        int hijriMonthZeroIndexed,
        DateTime? referenceToday = null,
        CancellationToken ct = default)
    {
        var today = (referenceToday ?? DateTime.Today).Date;
        hijriMonthZeroIndexed = Math.Clamp(hijriMonthZeroIndexed, 0, 11);

        int daysInMonth = FatimidHijriDate.GetDaysInMonth(hijriYear, hijriMonthZeroIndexed);
        var firstDayHijri = new FatimidHijriDate(hijriYear, hijriMonthZeroIndexed, 1);
        int leadingDaysCount = (int)firstDayHijri.GetDayOfWeek(); // 0 = Sunday, 1 = Monday...

        // Calculate previous month
        int prevYear = (hijriMonthZeroIndexed == 0) ? hijriYear - 1 : hijriYear;
        int prevMonth = (hijriMonthZeroIndexed == 0) ? 11 : hijriMonthZeroIndexed - 1;
        int prevDaysInMonth = FatimidHijriDate.GetDaysInMonth(prevYear, prevMonth);

        // Preload all miqaats for quick mapping
        var allMiqaats = await _miqaatRepository.GetAllMiqaatsAsync(ct);
        var miqaatsMap = allMiqaats.ToDictionary(
            r => (r.Month, r.Date),
            r => (IReadOnlyList<MiqaatItem>)(r.Miqaats ?? new List<MiqaatItem>())
        );

        IReadOnlyList<MiqaatItem> GetMiqaats(int m, int d)
        {
            return miqaatsMap.TryGetValue((m, d), out var list) ? list : Array.Empty<MiqaatItem>();
        }

        var days = new List<CalendarDayDto>();

        // 1. Filler days from previous month
        for (int i = 0; i < leadingDaysCount; i++)
        {
            int dayNum = prevDaysInMonth - leadingDaysCount + i + 1;
            var hDate = new FatimidHijriDate(prevYear, prevMonth, dayNum);
            var gDate = hDate.ToGregorian().Date;
            bool isToday = (gDate == today);
            var miqaats = GetMiqaats(prevMonth, dayNum);
            days.Add(new CalendarDayDto(hDate, gDate, miqaats, IsCurrentMonth: false, IsToday: isToday));
        }

        // 2. Current month days
        for (int dayNum = 1; dayNum <= daysInMonth; dayNum++)
        {
            var hDate = new FatimidHijriDate(hijriYear, hijriMonthZeroIndexed, dayNum);
            var gDate = hDate.ToGregorian().Date;
            bool isToday = (gDate == today);
            var miqaats = GetMiqaats(hijriMonthZeroIndexed, dayNum);
            days.Add(new CalendarDayDto(hDate, gDate, miqaats, IsCurrentMonth: true, IsToday: isToday));
        }

        // 3. Trailing filler days from next month to complete the 7-day row
        int trailingDaysCount = (7 - (days.Count % 7)) % 7;
        int nextYear = (hijriMonthZeroIndexed == 11) ? hijriYear + 1 : hijriYear;
        int nextMonth = (hijriMonthZeroIndexed == 11) ? 0 : hijriMonthZeroIndexed + 1;

        for (int dayNum = 1; dayNum <= trailingDaysCount; dayNum++)
        {
            var hDate = new FatimidHijriDate(nextYear, nextMonth, dayNum);
            var gDate = hDate.ToGregorian().Date;
            bool isToday = (gDate == today);
            var miqaats = GetMiqaats(nextMonth, dayNum);
            days.Add(new CalendarDayDto(hDate, gDate, miqaats, IsCurrentMonth: false, IsToday: isToday));
        }

        // Compute Gregorian span description (e.g. "Jul - Aug 2026")
        var firstDayG = firstDayHijri.ToGregorian();
        var lastDayG = new FatimidHijriDate(hijriYear, hijriMonthZeroIndexed, daysInMonth).ToGregorian();
        string gregorianSpan;
        if (firstDayG.Year == lastDayG.Year)
        {
            if (firstDayG.Month == lastDayG.Month)
            {
                gregorianSpan = firstDayG.ToString("MMMM yyyy");
            }
            else
            {
                gregorianSpan = $"{firstDayG:MMM} – {lastDayG:MMM} {lastDayG.Year}";
            }
        }
        else
        {
            gregorianSpan = $"{firstDayG:MMM yyyy} – {lastDayG:MMM yyyy}";
        }

        string monthNameEn = FatimidHijriDate.MonthNamesLongEn[hijriMonthZeroIndexed];
        string monthNameAr = FatimidHijriDate.MonthNamesArabic[hijriMonthZeroIndexed];

        return new MonthCalendarDto(
            HijriYear: hijriYear,
            HijriMonth: hijriMonthZeroIndexed,
            HijriMonthName: monthNameEn,
            HijriMonthArabic: monthNameAr,
            GregorianSpan: gregorianSpan,
            Days: days.AsReadOnly()
        );
    }
}
