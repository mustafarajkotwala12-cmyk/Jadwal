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

    [Fact]
    public void BuildThreeRowSchedule_Monday_IncludesPhysicalEducationInSmallSlot_AndThreeRows()
    {
        var periods = new List<PeriodOccurrence>
        {
            new("pt", JadwalDayOfWeek.Monday, "2026-09-07", "(PT)", "06:00", "07:00", "Physical Training", "خيمة الرياضة"),
            new("p2", JadwalDayOfWeek.Monday, "2026-09-07", "Period 2", "08:50", "09:25", "Subject 2", "Room A"),
            new("p3", JadwalDayOfWeek.Monday, "2026-09-07", "Period 3", "09:25", "10:00", "Subject 3", "Room B"),
            new("p4", JadwalDayOfWeek.Monday, "2026-09-07", "Period 4", "10:00", "10:35", "Subject 4", "Room C"),
            new("p5", JadwalDayOfWeek.Monday, "2026-09-07", "Period 5", "10:55", "11:30", "Subject 5", "Room D"),
            new("p6", JadwalDayOfWeek.Monday, "2026-09-07", "Period 6", "11:30", "12:05", "Subject 6", "Room E"),
            new("p7", JadwalDayOfWeek.Monday, "2026-09-07", "Period 7", "12:05", "12:40", "Subject 7", "Room F"),
            new("p8", JadwalDayOfWeek.Monday, "2026-09-07", "Period 8", "14:00", "14:35", "Subject 8", "Room G"),
            new("p9", JadwalDayOfWeek.Monday, "2026-09-07", "Period 9", "14:35", "15:10", "Subject 9", "Room H"),
            new("p10", JadwalDayOfWeek.Monday, "2026-09-07", "Period 10", "15:10", "15:45", "Subject 10", "Room I")
        };

        var three = ScheduleTimelineBuilder.BuildThreeRowSchedule(periods, JadwalDayOfWeek.Monday);

        three.HasPhysicalEducation.Should().BeTrue();
        three.PhysicalEducationItem.Should().NotBeNull();
        three.PhysicalEducationItem!.Period!.Subject.Should().Be("Physical Training");

        three.Row1Items.Should().HaveCount(3);
        three.Break1.Should().NotBeNull();
        three.Break1!.Name.Should().Contain("Recess");

        three.Row2Items.Should().HaveCount(3);
        three.Break2.Should().NotBeNull();
        three.Break2!.Name.Should().Contain("Lunch & Namaz");

        three.Row3Items.Should().HaveCount(3);
        three.HasRow3.Should().BeTrue();
    }

    [Fact]
    public void BuildThreeRowSchedule_Friday_ExplicitlyRemovesPhysicalEducationSlot()
    {
        var periods = new List<PeriodOccurrence>
        {
            new("pt", JadwalDayOfWeek.Friday, "2026-09-11", "(PT)", "06:00", "07:00", "Physical Training", "خيمة الرياضة"),
            new("p2", JadwalDayOfWeek.Friday, "2026-09-11", "Period 2", "08:50", "09:25", "Subject 2", "Room A"),
            new("p3", JadwalDayOfWeek.Friday, "2026-09-11", "Period 3", "09:25", "10:00", "Subject 3", "Room B"),
            new("p4", JadwalDayOfWeek.Friday, "2026-09-11", "Period 4", "10:00", "10:35", "Subject 4", "Room C"),
            new("p5", JadwalDayOfWeek.Friday, "2026-09-11", "Period 5", "10:55", "11:30", "Subject 5", "Room D"),
            new("p6", JadwalDayOfWeek.Friday, "2026-09-11", "Period 6", "11:30", "12:05", "Subject 6", "Room E"),
            new("p7", JadwalDayOfWeek.Friday, "2026-09-11", "Period 7", "12:05", "12:40", "Subject 7", "Room F"),
            new("p8", JadwalDayOfWeek.Friday, "2026-09-11", "Period 8", "14:00", "14:35", "Subject 8", "Room G"),
            new("p9", JadwalDayOfWeek.Friday, "2026-09-11", "Period 9", "14:35", "15:10", "Subject 9", "Room H"),
            new("p10", JadwalDayOfWeek.Friday, "2026-09-11", "Period 10", "15:10", "15:45", "Subject 10", "Room I")
        };

        var three = ScheduleTimelineBuilder.BuildThreeRowSchedule(periods, JadwalDayOfWeek.Friday);

        // Friday rule: PE slot MUST be removed
        three.HasPhysicalEducation.Should().BeFalse();
        three.PhysicalEducationItem.Should().BeNull();

        three.Row1Items.Should().HaveCount(3);
        three.Break1.Should().NotBeNull();
        three.Row2Items.Should().HaveCount(3);
        three.Break2.Should().NotBeNull();
        three.Break2!.Name.Should().Contain("Jumua");
        three.Row3Items.Should().HaveCount(3);
    }

    [Fact]
    public void PeriodOccurrence_FormatTo12Hour_FormatsCorrectly()
    {
        PeriodOccurrence.FormatTo12Hour("06:00").Should().Be("6:00 AM");
        PeriodOccurrence.FormatTo12Hour("08:00").Should().Be("8:00 AM");
        PeriodOccurrence.FormatTo12Hour("08:45").Should().Be("8:45 AM");
        PeriodOccurrence.FormatTo12Hour("12:40").Should().Be("12:40 PM");
        PeriodOccurrence.FormatTo12Hour("14:00").Should().Be("2:00 PM");
        PeriodOccurrence.FormatTo12Hour("15:30").Should().Be("3:30 PM");
        PeriodOccurrence.FormatTo12Hour("23:15").Should().Be("11:15 PM");
        PeriodOccurrence.FormatTo12Hour("00:30").Should().Be("12:30 AM");

        var period = new PeriodOccurrence("p1", JadwalDayOfWeek.Monday, "2026-09-14", "Period 1", "08:00", "14:00", "Quran", "Room A");
        period.StartTime12H.Should().Be("8:00 AM");
        period.EndTime12H.Should().Be("2:00 PM");
    }

    [Fact]
    public void BreakBarInfo_TimeRangeFormatted_Uses12HourAmPm()
    {
        var breakInfo = new BreakBarInfo(
            Id: "break_recess",
            Name: "Recess Break",
            NamaazNote: "Refreshment",
            StartTime: "10:35",
            EndTime: "10:55",
            DurationMinutes: 20,
            Icon: "☕"
        );

        breakInfo.TimeRangeFormatted.Should().Be("10:35 AM – 10:55 AM");

        var lunchBreak = new BreakBarInfo(
            Id: "break_lunch",
            Name: "Lunch & Namaz",
            NamaazNote: "🕌 Zohr",
            StartTime: "12:40",
            EndTime: "14:00",
            DurationMinutes: 80,
            Icon: "☀️"
        );

        lunchBreak.TimeRangeFormatted.Should().Be("12:40 PM – 2:00 PM");
    }
}

