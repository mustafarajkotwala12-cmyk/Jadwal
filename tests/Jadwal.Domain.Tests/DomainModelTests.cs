using System.Text.Json;
using FluentAssertions;
using Jadwal.Domain.Enums;
using Jadwal.Domain.Models;
using Jadwal.Domain.Rules;
using Xunit;

namespace Jadwal.Domain.Tests;

public class DomainModelTests
{
    [Fact]
    public void PeriodOccurrence_GeneratesStableDeterministicSlotId()
    {
        var period = new PeriodOccurrence(
            id: null,
            day: JadwalDayOfWeek.Monday,
            dateString: "2026-09-07",
            periodName: "Period 2",
            startTime: "08:50",
            endTime: "09:25",
            subject: "الرسالة الشريفة",
            details: "الشيخ ابراهيم بهائي"
        );

        period.Id.Should().Be("2026-09-07_Period_2");
    }

    [Fact]
    public void DomainModels_SerializeAndDeserializeDeterministically()
    {
        var original = new PeriodOccurrence(
            id: "2026-09-07_Period_2",
            day: JadwalDayOfWeek.Monday,
            dateString: "2026-09-07",
            periodName: "Period 2",
            startTime: "08:50",
            endTime: "09:25",
            subject: "الرسالة الشريفة",
            details: "الشيخ ابراهيم بهائي"
        );

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PeriodOccurrence>(json);

        restored.Should().NotBeNull();
        restored!.Id.Should().Be(original.Id);
        restored.Subject.Should().Be(original.Subject);
        restored.Day.Should().Be(original.Day);
    }

    [Fact]
    public void TaskItem_StatusTogglesAndDiscrepancyProtectionWorks()
    {
        var task = new TaskItem(
            title: "Prepare Linguistics Essay",
            notes: "Chapter 3",
            priority: TaskPriority.Urgent,
            category: TaskCategory.Academic,
            linkedSubject: "Linguistics",
            linkedPeriodId: "2026-09-07_Period_4"
        );

        task.IsCompleted.Should().BeFalse();
        task.MarkCompleted();
        task.IsCompleted.Should().BeTrue();
        task.CompletedAt.Should().NotBeNull();

        task.Reopen();
        task.IsCompleted.Should().BeFalse();
        task.CompletedAt.Should().BeNull();

        // Discrepancy protection
        task.Discrepancy = new TaskDiscrepancy("Linguistics", "Economics", "2026-09-07_Period_4");
        task.Title.Should().Be("Prepare Linguistics Essay"); // untouched
        task.AdoptNewSubject();
        task.LinkedSubject.Should().Be("Economics");
        task.Discrepancy.Should().BeNull();
    }

    [Fact]
    public void DayScheduleRule_DistinguishesMonThuFridayAndSaturdayExceptions()
    {
        var mon = DayScheduleRule.RuleFor(JadwalDayOfWeek.Monday);
        mon.HasPhysicalTraining.Should().BeTrue();
        mon.IsHalfDay.Should().BeFalse();
        mon.DaySubtitle.Should().Contain("10 Periods");

        var fri = DayScheduleRule.RuleFor(JadwalDayOfWeek.Friday);
        fri.HasPhysicalTraining.Should().BeFalse();
        fri.DaySubtitle.Should().Contain("Jumua Mubarak");
        fri.Row1Title.Should().Contain("Periods 2–5");

        var sat = DayScheduleRule.RuleFor(JadwalDayOfWeek.Saturday);
        sat.HasPhysicalTraining.Should().BeFalse();
        sat.IsHalfDay.Should().BeTrue();
        sat.DaySubtitle.Should().Contain("Saturday Half-Day");
        sat.Row1Title.Should().Contain("Periods 1–4");
        sat.Row2Title.Should().Contain("Periods 5–8");
    }

    [Fact]
    public void ScheduleTimelineBuilder_ComputesBreaksAndPartitionsCorrectly()
    {
        var periods = new List<PeriodOccurrence>
        {
            new(null, JadwalDayOfWeek.Monday, "2026-09-07", "Period 1", "06:00", "07:00", "PT", "Gym"),
            new(null, JadwalDayOfWeek.Monday, "2026-09-07", "Period 2", "08:50", "09:25", "Fiqh", "R1"),
            new(null, JadwalDayOfWeek.Monday, "2026-09-07", "Period 3", "09:25", "10:00", "Adab", "R2"),
            new(null, JadwalDayOfWeek.Monday, "2026-09-07", "Period 4", "10:00", "10:35", "Hadith", "R3"),
            new(null, JadwalDayOfWeek.Monday, "2026-09-07", "Period 5", "10:55", "11:30", "Nahw", "R4"),
            new(null, JadwalDayOfWeek.Monday, "2026-09-07", "Period 6", "11:30", "12:05", "Tafseer", "R5")
        };

        var timeline = ScheduleTimelineBuilder.BuildTimeline(periods);
        timeline.Should().Contain(i => i.Kind == ScheduleTimelineItemKind.BreakBlock && i.Break!.Name == "Morning Preparation");
        timeline.Should().Contain(i => i.Kind == ScheduleTimelineItemKind.BreakBlock && i.Break!.Name == "Recess");

        var (row1, row2) = ScheduleTimelineBuilder.SplitIntoTwoHorizontalRows(timeline, JadwalDayOfWeek.Monday);
        row1.Should().NotBeEmpty();
        row2.Should().NotBeEmpty();
        row1.Should().Contain(i => i.Kind == ScheduleTimelineItemKind.ClassPeriod && i.Period!.PeriodName == "Period 5");
        row2.Should().Contain(i => i.Kind == ScheduleTimelineItemKind.ClassPeriod && i.Period!.PeriodName == "Period 6");
    }

    [Fact]
    public void TimetableDiffEngine_DetectsChangesAccurately()
    {
        var oldPeriods = new List<PeriodOccurrence>
        {
            new("p1", JadwalDayOfWeek.Monday, "2026-09-07", "Period 1", "08:00", "08:45", "Arabic", "Room 101"),
            new("p2", JadwalDayOfWeek.Monday, "2026-09-07", "Period 2", "08:50", "09:25", "Linguistics", "Room 102")
        };
        var oldSnap = new TimetableSnapshot("1447", 25, oldPeriods);

        var newPeriods = new List<PeriodOccurrence>
        {
            new("p1", JadwalDayOfWeek.Monday, "2026-09-07", "Period 1", "08:15", "09:00", "Arabic", "Room 101"), // time changed
            new("p2", JadwalDayOfWeek.Monday, "2026-09-07", "Period 2", "08:50", "09:25", "Economics", "Room 102"), // subject changed
            new("p3", JadwalDayOfWeek.Monday, "2026-09-07", "Period 3", "10:00", "10:45", "Math", "Room 103") // added
        };
        var newSnap = new TimetableSnapshot("1447", 25, newPeriods);

        var engine = new TimetableDiffEngine();
        var diffs = engine.Diff(oldSnap, newSnap);

        diffs.Should().HaveCount(3);
        diffs.Should().Contain(d => d.PeriodId == "p1" && d.ChangeType == ChangeType.TimeChanged);
        diffs.Should().Contain(d => d.PeriodId == "p2" && d.ChangeType == ChangeType.SubjectChanged && d.NewSubject == "Economics");
        diffs.Should().Contain(d => d.PeriodId == "p3" && d.ChangeType == ChangeType.Added && d.NewSubject == "Math");
    }
}
