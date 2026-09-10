using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Services;
using Jadwal.Domain.Models;
using Xunit;

namespace Jadwal.Application.Tests;

public class TestMiqaatRepository : IMiqaatRepository
{
    private readonly List<DayMiqaatsRecord> _records;

    public TestMiqaatRepository(List<DayMiqaatsRecord>? records = null)
    {
        _records = records ?? new List<DayMiqaatsRecord>
        {
            new()
            {
                Month = 0,
                Date = 10,
                Miqaats = new List<MiqaatItem>
                {
                    new() { Title = "Yawme Ashura", Phase = "day", Priority = 1 },
                    new() { Title = "Shahadat Imam Hussain (SA)", Phase = "day", Priority = 1 }
                }
            },
            new()
            {
                Month = 8,
                Date = 22,
                Miqaats = new List<MiqaatItem>
                {
                    new() { Title = "Lailat al-Qadr", Phase = "night", Priority = 1 }
                }
            }
        };
    }

    public Task<IReadOnlyList<DayMiqaatsRecord>> GetAllMiqaatsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<DayMiqaatsRecord>>(_records.AsReadOnly());
    }

    public Task<IReadOnlyList<MiqaatItem>> GetMiqaatsForHijriDayAsync(int monthZeroIndexed, int day, CancellationToken ct = default)
    {
        var match = _records.FirstOrDefault(r => r.Month == monthZeroIndexed && r.Date == day);
        return Task.FromResult<IReadOnlyList<MiqaatItem>>(match?.Miqaats?.AsReadOnly() ?? (IReadOnlyList<MiqaatItem>)Array.Empty<MiqaatItem>());
    }
}

public class CalendarServiceTests
{
    [Fact]
    public async Task GetMiqaatsForHijriDateAsync_ReturnsMatchingMiqaats()
    {
        var repo = new TestMiqaatRepository();
        var service = new CalendarService(repo);

        var ashuraMiqaats = await service.GetMiqaatsForHijriDateAsync(0, 10);
        ashuraMiqaats.Should().NotBeEmpty();
        ashuraMiqaats.Should().HaveCount(2);
        ashuraMiqaats[0].Title.Should().Be("Yawme Ashura");
        ashuraMiqaats[1].Title.Should().Be("Shahadat Imam Hussain (SA)");

        var emptyDay = await service.GetMiqaatsForHijriDateAsync(0, 5);
        emptyDay.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildMonthCalendarAsync_GeneratesCompleteWeeksAlignedToSunday()
    {
        var repo = new TestMiqaatRepository();
        var service = new CalendarService(repo);

        // Rabi I 1448 (Month 2)
        var referenceToday = new DateTime(2026, 9, 11);
        var monthCalendar = await service.BuildMonthCalendarAsync(1448, 2, referenceToday);

        monthCalendar.HijriYear.Should().Be(1448);
        monthCalendar.HijriMonth.Should().Be(2);
        monthCalendar.HijriMonthName.Should().Be("Rabi al-Awwal");
        monthCalendar.Days.Should().NotBeEmpty();

        // Must be a multiple of 7 (full weeks)
        (monthCalendar.Days.Count % 7).Should().Be(0);

        // First cell in calendar must be a Sunday
        monthCalendar.Days[0].GregorianDate.DayOfWeek.Should().Be(DayOfWeek.Sunday);

        // Last cell in calendar must be a Saturday
        monthCalendar.Days.Last().GregorianDate.DayOfWeek.Should().Be(DayOfWeek.Saturday);

        // Total current month days in Rabi al-Awwal (month 2) must be 30
        var currentMonthDays = monthCalendar.Days.Where(d => d.IsCurrentMonth).ToList();
        currentMonthDays.Should().HaveCount(30);

        // Days should be consecutive (1 to 30)
        for (int i = 0; i < 30; i++)
        {
            currentMonthDays[i].HijriDate.Day.Should().Be(i + 1);
        }
    }

    [Fact]
    public async Task BuildMonthCalendarAsync_IdentifiesTodayAccurately()
    {
        var repo = new TestMiqaatRepository();
        var service = new CalendarService(repo);

        var today = DateTime.Today;
        var todayHijri = service.GetHijriDate(today);

        var monthCalendar = await service.BuildMonthCalendarAsync(todayHijri.Year, todayHijri.Month, today);

        var todayCell = monthCalendar.Days.FirstOrDefault(d => d.IsToday);
        todayCell.Should().NotBeNull();
        todayCell!.GregorianDate.Date.Should().Be(today.Date);
        todayCell.HijriDate.Day.Should().Be(todayHijri.Day);
        todayCell.HijriDate.Month.Should().Be(todayHijri.Month);
    }
}
