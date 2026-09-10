using FluentAssertions;
using Jadwal.Domain.Enums;
using Jadwal.Integrations.Jamia;
using Xunit;

namespace Jadwal.Integrations.Jamia.Tests;

public class JamiaIntegrationTests
{
    [Fact]
    public void TimetableImporter_ParsesNormalMonThuFixtureAccurately()
    {
        var fixturePath = Path.Combine("..", "..", "..", "..", "..", "tools", "fixtures", "timetable_normal_mon_thu.json");
        var json = File.ReadAllText(fixturePath);

        var importer = new TimetableImporter();
        var snapshot = importer.ImportFromJson(json);

        snapshot.Should().NotBeNull();
        snapshot.AcademicYear.Should().Contain("1447");
        snapshot.WeekNumber.Should().Be(25);
        snapshot.Periods.Should().HaveCount(10);

        var pt = snapshot.Periods.First();
        pt.PeriodName.Should().Be("(PT)");
        pt.Subject.Should().Be("Physical Training");
        pt.StartTime.Should().Be("06:00");
        pt.EndTime.Should().Be("07:00");
        pt.Day.Should().Be(JadwalDayOfWeek.Monday);

        var p2 = snapshot.Periods[1];
        p2.PeriodName.Should().Be("Period 2");
        p2.Subject.Should().Be("الرسالة الشريفة");
        p2.Id.Should().Be("2026-09-07_Period_2");
    }

    [Fact]
    public void TimetableImporter_ParsesFridayAndSaturdayFixturesAccurately()
    {
        var friPath = Path.Combine("..", "..", "..", "..", "..", "tools", "fixtures", "timetable_friday_jumua.json");
        var satPath = Path.Combine("..", "..", "..", "..", "..", "tools", "fixtures", "timetable_saturday_halfday.json");

        var importer = new TimetableImporter();
        var friSnap = importer.ImportFromJson(File.ReadAllText(friPath));
        var satSnap = importer.ImportFromJson(File.ReadAllText(satPath));

        friSnap.Periods.Should().HaveCount(9);
        friSnap.Periods.Should().NotContain(p => p.PeriodName == "(PT)");
        friSnap.Periods.First().PeriodName.Should().Be("Period 2");

        satSnap.Periods.Should().HaveCount(8);
        satSnap.Periods.First().PeriodName.Should().Be("Period 1");
        satSnap.Periods.First().Subject.Should().Be("نهج البلاغة");
        satSnap.Periods.Last().PeriodName.Should().Be("Period 8");
        satSnap.Periods.Last().EndTime.Should().Be("13:15");
    }

    [Fact]
    public void JwtValidator_ValidatesTokenSafely()
    {
        // Malformed token
        JwtValidator.IsTokenValid(null).Should().BeFalse();
        JwtValidator.IsTokenValid("").Should().BeFalse();
        JwtValidator.IsTokenValid("abc.def").Should().BeFalse();

        // Expired token payload: {"exp": 1000000000} (Sat Sep 09 2001)
        // base64url for '{"exp": 1000000000}' is 'eyJleHAiOiAxMDAwMDAwMDAwfQ'
        var expiredToken = "header.eyJleHAiOiAxMDAwMDAwMDAwfQ.signature";
        JwtValidator.IsTokenValid(expiredToken).Should().BeFalse();

        // Unexpired token payload: {"exp": 4102444800} (Jan 01 2100)
        // base64url for '{"exp": 4102444800}' is 'eyJleHAiOiA0MTAyNDQ0ODAwfQ'
        var validToken = "header.eyJleHAiOiA0MTAyNDQ0ODAwfQ.signature";
        JwtValidator.IsTokenValid(validToken).Should().BeTrue();
    }
}
