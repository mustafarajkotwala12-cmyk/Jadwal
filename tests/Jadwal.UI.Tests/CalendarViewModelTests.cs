using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Services;
using Jadwal.Domain.Models;
using Jadwal.UI.ViewModels;
using Xunit;

namespace Jadwal.UI.Tests;

public class TestMiqaatRepo : IMiqaatRepository
{
    private readonly List<DayMiqaatsRecord> _records;

    public TestMiqaatRepo(List<DayMiqaatsRecord>? records = null)
    {
        _records = records ?? new List<DayMiqaatsRecord>();
    }

    public Task<IReadOnlyList<DayMiqaatsRecord>> GetAllMiqaatsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DayMiqaatsRecord>>(_records.AsReadOnly());

    public Task<IReadOnlyList<MiqaatItem>> GetMiqaatsForHijriDayAsync(int monthZeroIndexed, int day, CancellationToken ct = default)
    {
        var match = _records.FirstOrDefault(r => r.Month == monthZeroIndexed && r.Date == day);
        return Task.FromResult<IReadOnlyList<MiqaatItem>>(match?.Miqaats?.AsReadOnly() ?? (IReadOnlyList<MiqaatItem>)Array.Empty<MiqaatItem>());
    }
}

public class CalendarViewModelTests
{
    private static CalendarService CreateCalendarService()
    {
        var testRepo = new TestMiqaatRepo(new List<DayMiqaatsRecord>
        {
            new()
            {
                Month = 2, // Rabi I
                Date = 16,
                Miqaats = new List<MiqaatItem>
                {
                    new()
                    {
                        Title = "Urus Syedna Mohammed Burhanuddin (AQ)",
                        Description = "52nd Dai, Mumbai, India.",
                        Phase = "day",
                        Priority = 1,
                        Year = 1435
                    }
                }
            }
        });

        return new CalendarService(testRepo);
    }

    [Fact]
    public async Task CalendarViewModel_InitializesWithCurrentMonthAndSelectsDay()
    {
        var service = CreateCalendarService();
        var vm = new CalendarViewModel(service);

        await vm.InitializeAsync();

        vm.Days.Should().NotBeEmpty();
        vm.MonthTitle.Should().NotBeNullOrWhiteSpace();
        vm.GregorianSpan.Should().NotBeNullOrWhiteSpace();
        vm.SelectedDay.Should().NotBeNull();
        vm.SelectedDayHeader.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CalendarViewModel_SelectingDayWithMiqaat_PopulatesSelectedDayMiqaats()
    {
        var service = CreateCalendarService();
        var vm = new CalendarViewModel(service);

        // Set to Rabi I 1448
        vm.DisplayedHijriYear = 1448;
        vm.DisplayedHijriMonth = 2; // Rabi I

        await vm.InitializeAsync();

        var day16 = vm.Days.FirstOrDefault(d => d.IsCurrentMonth && d.DayNumber == 16);
        day16.Should().NotBeNull();
        day16!.HasMiqaats.Should().BeTrue();
        day16.TopMiqaatTitle.Should().Contain("Mohammed Burhanuddin");

        vm.SelectDay(day16);

        vm.HasSelectedDayMiqaats.Should().BeTrue();
        vm.SelectedDayMiqaats.Should().HaveCount(1);
        vm.SelectedDayMiqaats[0].Title.Should().Contain("Mohammed Burhanuddin");
        vm.SelectedDayMiqaats[0].PhaseBadge.Should().Be("☀️ Day");
    }

    [Fact]
    public async Task CalendarViewModel_PreviousAndNextMonth_UpdatesDisplayedMonth()
    {
        var service = CreateCalendarService();
        var vm = new CalendarViewModel(service);

        vm.DisplayedHijriYear = 1448;
        vm.DisplayedHijriMonth = 5; // Jumada II

        await vm.InitializeAsync();
        vm.DisplayedHijriMonth.Should().Be(5);

        await vm.NextMonthAsync();
        vm.DisplayedHijriMonth.Should().Be(6); // Rajab
        vm.MonthTitle.Should().Contain("Rajab");

        await vm.PreviousMonthAsync();
        vm.DisplayedHijriMonth.Should().Be(5); // Back to Jumada II
    }

    [Fact]
    public async Task TodayViewModel_LoadsHijriDateAndTodayMiqaats()
    {
        var service = CreateCalendarService();
        var snapshot = TodayViewModelTests.CreateStandardSnapshot();
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo(snapshot);
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);
        var dashboardService = new DashboardService(timeRepo, taskRepo, changeRepo, timeProvider);

        var vm = new TodayViewModel(dashboardService, taskService, timetableService, timeProvider, service);

        await vm.RefreshScheduleAsync();

        vm.HijriDateFormatted.Should().NotBeNullOrWhiteSpace();
        vm.HijriDateFormatted.Should().Contain("AH");
        vm.HijriDateArabic.Should().NotBeNullOrWhiteSpace();
    }
}
