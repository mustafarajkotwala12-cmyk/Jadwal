using FluentAssertions;
using Jadwal.Application.Services;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;
using Jadwal.UI.ViewModels;
using Xunit;

namespace Jadwal.UI.Tests;

public class TimetableViewModelTests
{
    private TimetableSnapshot CreateFullWeekSnapshot()
    {
        var periods = new List<PeriodOccurrence>();

        // Friday: 9 periods (p2 to p10), no PT, Jumua break
        for (int p = 2; p <= 10; p++)
        {
            var start = $"{p + 5:D2}:00";
            var end = $"{p + 5:D2}:45";
            periods.Add(new($"fri_p{p}", JadwalDayOfWeek.Friday, "2026-09-18", $"Period {p}", start, end, p == 6 ? "جمعة مباركة" : "حديث", "Jumua"));
        }

        // Saturday: 8 periods (p1 to p8), concludes at 1:15 PM
        for (int p = 1; p <= 8; p++)
        {
            var start = $"{p + 5:D2}:00";
            var end = $"{p + 5:D2}:45";
            periods.Add(new($"sat_p{p}", JadwalDayOfWeek.Saturday, "2026-09-19", $"Period {p}", start, end, "تاريخ", "Room 101"));
        }

        return new TimetableSnapshot("1446-1447", 1, periods, DateTime.UtcNow);
    }

    [Fact]
    public async Task TimetableViewModel_FridayRule_ReflectsJumuaSchedule()
    {
        var snapshot = CreateFullWeekSnapshot();
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo(snapshot);
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);

        var vm = new TimetableViewModel(timetableService, taskService, timeProvider);
        await vm.SelectDayAsync(JadwalDayOfWeek.Friday);

        vm.DayArabicTitle.Should().Be("يوم الجمعة");
        vm.DaySubtitle.Should().Contain("No Morning PT");
        vm.Row1Items.Should().NotBeEmpty();
        vm.Row2Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TimetableViewModel_SaturdayRule_ReflectsHalfDaySchedule()
    {
        var snapshot = CreateFullWeekSnapshot();
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo(snapshot);
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);

        var vm = new TimetableViewModel(timetableService, taskService, timeProvider);
        await vm.SelectDayAsync(JadwalDayOfWeek.Saturday);

        vm.DayArabicTitle.Should().Be("يوم السبت");
        vm.DaySubtitle.Should().Contain("Concludes at 1:15 PM");
        vm.Row1Title.Should().Contain("Periods 1–4");
        vm.Row2Title.Should().Contain("Periods 5–8");
    }

    [Fact]
    public async Task TimetableViewModel_SearchFilter_FiltersPeriodsBySubject()
    {
        var snapshot = CreateFullWeekSnapshot();
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo(snapshot);
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);

        var vm = new TimetableViewModel(timetableService, taskService, timeProvider);
        await vm.SelectDayAsync(JadwalDayOfWeek.Friday);

        vm.SearchText = "جمعة";
        await vm.LoadDayScheduleAsync();

        // Only the Jumua period should match
        var matchingCards = vm.Row2Items.OfType<ClassCardItemViewModel>().ToList();
        matchingCards.Should().HaveCount(1);
        matchingCards[0].Period.Subject.Should().Be("جمعة مباركة");
    }

    [Fact]
    public async Task TimetableViewModel_Rows_ArePopulatedInRtlOrder()
    {
        var periods = new List<PeriodOccurrence>();
        // Add Mon periods: PT at 06:00, then Periods 2-10
        periods.Add(new("mon_pt", JadwalDayOfWeek.Monday, "2026-09-14", "PT", "06:00", "07:00", "Physical Education", "Field"));
        for (int p = 2; p <= 10; p++)
        {
            var start = $"{p + 5:D2}:00";
            var end = $"{p + 5:D2}:45";
            periods.Add(new($"mon_p{p}", JadwalDayOfWeek.Monday, "2026-09-14", $"Period {p}", start, end, $"Subject {p}", "Room"));
        }
        var snapshot = new TimetableSnapshot("1446-1447", 1, periods, DateTime.UtcNow);
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo(snapshot);
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var taskService = new TaskService(taskRepo, timeProvider);
        var timetableService = new TimetableService(timeRepo, changeRepo, taskRepo, dummyProvider);

        var vm = new TimetableViewModel(timetableService, taskService, timeProvider);
        await vm.SelectDayAsync(JadwalDayOfWeek.Monday);

        // Physical Education card is on the rightmost slot
        vm.HasPhysicalEducation.Should().BeTrue();
        vm.PhysicalEducationCard.Should().NotBeNull();
        vm.PhysicalEducationCard!.Period.Subject.Should().Be("Physical Education");

        // Row 1: Periods 2, 3, 4 are reversed for RTL (so [P4, P3, P2] left to right; earliest P2 on right next to PE!)
        vm.Row1Cards.Should().HaveCount(3);
        vm.Row1Cards[0].Period.PeriodName.Should().Be("Period 4");
        vm.Row1Cards[1].Period.PeriodName.Should().Be("Period 3");
        vm.Row1Cards[2].Period.PeriodName.Should().Be("Period 2");

        // Row 2: Periods 5, 6, 7 reversed for RTL ([P7, P6, P5])
        vm.Row2Cards.Should().HaveCount(3);
        vm.Row2Cards[0].Period.PeriodName.Should().Be("Period 7");
        vm.Row2Cards[1].Period.PeriodName.Should().Be("Period 6");
        vm.Row2Cards[2].Period.PeriodName.Should().Be("Period 5");

        // Row 3: Periods 8, 9, 10 reversed for RTL ([P10, P9, P8])
        vm.Row3Cards.Should().HaveCount(3);
        vm.Row3Cards[0].Period.PeriodName.Should().Be("Period 10");
        vm.Row3Cards[1].Period.PeriodName.Should().Be("Period 9");
        vm.Row3Cards[2].Period.PeriodName.Should().Be("Period 8");
    }

    [Fact]
    public async Task TimetableViewModel_Cards_Use12HourAmPm_And_SyncStatusWithComputerTime()
    {
        var periods = new List<PeriodOccurrence>
        {
            new("p1", JadwalDayOfWeek.Monday, "2026-09-14", "Period 1", "08:00", "08:45", "Subject 1", "Room A"),
            new("p2", JadwalDayOfWeek.Monday, "2026-09-14", "Period 2", "14:00", "14:45", "Subject 2", "Room B")
        };
        var snapshot = new TimetableSnapshot("1446-1447", 1, periods, DateTime.UtcNow);
        var taskRepo = new MemoryTaskRepo();
        var timeRepo = new MemoryTimetableRepo(snapshot);
        var changeRepo = new MemoryChangeRepo();
        var dummyProvider = new DummyJamiaProvider();
        var timeProvider = new TestTimeProvider();

        var card1 = new ClassCardItemViewModel(periods[0], ClassLiveStatus.Upcoming);
        var card2 = new ClassCardItemViewModel(periods[1], ClassLiveStatus.Upcoming);

        // Verify 12H AM/PM formatting
        card1.StartTime12H.Should().Be("8:00 AM");
        card1.EndTime12H.Should().Be("8:45 AM");
        card1.EndTimeDisplay.Should().Be("Ends 8:45 AM");
        card1.TimeRangeFormatted.Should().Be("8:00 AM – 8:45 AM");

        card2.StartTime12H.Should().Be("2:00 PM");
        card2.EndTime12H.Should().Be("2:45 PM");
        card2.EndTimeDisplay.Should().Be("Ends 2:45 PM");
        card2.TimeRangeFormatted.Should().Be("2:00 PM – 2:45 PM");

        // Verify live status sync with computer clock
        // Time is 08:15 AM -> card1 should be IN SESSION
        card1.UpdateStatus(new TimeOnly(8, 15));
        card1.Status.Kind.Should().Be(ClassStatusKind.InProgress);
        card1.StatusText.Should().Be("IN SESSION");

        // Time is 09:00 AM -> card1 should be DONE
        card1.UpdateStatus(new TimeOnly(9, 0));
        card1.Status.Kind.Should().Be(ClassStatusKind.Completed);
        card1.StatusText.Should().Be("DONE");

        // Time is 01:50 PM -> card2 should be StartingSoon (10m)
        card2.UpdateStatus(new TimeOnly(13, 50));
        card2.Status.Kind.Should().Be(ClassStatusKind.StartingSoon);
        card2.StatusText.Should().Be("IN 10M");
    }
}


