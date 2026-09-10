using FluentAssertions;
using Jadwal.Application.Services;
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

    [Fact]
    public void ExcelTimetableParser_ParsesActualExcelFileAccurately()
    {
        var excelPath = Path.Combine("..", "..", "..", "..", "..", "data", "TimeTable_3032130520.xlsx");
        if (!File.Exists(excelPath)) return;

        var snapshot = ExcelTimetableParser.Parse(excelPath);
        snapshot.Should().NotBeNull();
        snapshot.AcademicYear.Should().Contain("1447");
        snapshot.Periods.Should().NotBeEmpty();

        var mondayPeriods = snapshot.GetPeriodsForDay(JadwalDayOfWeek.Monday);
        mondayPeriods.Should().NotBeEmpty();

        var pt = mondayPeriods.FirstOrDefault(p => p.PeriodName == "(PT)");
        pt.Should().NotBeNull();
        pt!.Subject.Should().Be("Physical Training");
    }

    [Fact]
    public void JwtValidator_ExtractsItsIdCorrectly()
    {
        // {"itsId": "30711980", "exp": 4102444800} -> eyJpdHNJZCI6ICIzMDcxMTk4MCIsICJleHAiOiA0MTAyNDQ0ODAwfQ
        var tokenWithItsId = "header.eyJpdHNJZCI6ICIzMDcxMTk4MCIsICJleHAiOiA0MTAyNDQ0ODAwfQ.signature";
        JwtValidator.GetTokenItsId(tokenWithItsId).Should().Be("30711980");

        // {"studentITSID": "30327222", "exp": 4102444800} -> eyJzdHVkZW50SVRTSUQiOiAiMzAzMjcyMjIiLCAiZXhwIjogNDEwMjQ0NDgwMH0
        var tokenWithStudentItsId = "header.eyJzdHVkZW50SVRTSUQiOiAiMzAzMjcyMjIiLCAiZXhwIjogNDEwMjQ0NDgwMH0.signature";
        JwtValidator.GetTokenItsId(tokenWithStudentItsId).Should().Be("30327222");

        // No itsId
        var tokenWithoutItsId = "header.eyJleHAiOiA0MTAyNDQ0ODAwfQ.signature";
        JwtValidator.GetTokenItsId(tokenWithoutItsId).Should().BeNull();

        // Null / Malformed
        JwtValidator.GetTokenItsId(null).Should().BeNull();
        JwtValidator.GetTokenItsId("invalid").Should().BeNull();
    }

    [Fact]
    public async Task JamiaTimetableProvider_RejectsTokenForDifferentItsId()
    {
        var mockStorage = new TestSecureStorage();
        // User is configured with non-matching ITS ID 99999999
        await mockStorage.SetSecretAsync("its_id", "99999999");
        // Stored token belongs to 30327222
        var foreignToken = "header.eyJpdHNJZCI6ICIzMDMyNzIyMiIsICJleHAiOiA0MTAyNDQ0ODAwfQ.sig";
        await mockStorage.SetSecretAsync("jamea_access_token", foreignToken);

        var provider = new JamiaTimetableProvider(mockStorage, "/tmp/nonexistent");
        var token = await provider.GetAccessTokenAsync();

        // Must reject foreign token!
        token.Should().BeNull();

        // Stored token matching 99999999
        var ownToken = "header.eyJpdHNJZCI6ICI5OTk5OTk5OSIsICJleHAiOiA0MTAyNDQ0ODAwfQ.sig";
        await mockStorage.SetSecretAsync("jamea_access_token", ownToken);

        var validToken = await provider.GetAccessTokenAsync();
        validToken.Should().Be(ownToken);
    }
}

internal class TestSecureStorage : Jadwal.Application.Interfaces.ISecureStorage
{
    private readonly Dictionary<string, string> _store = new();

    public Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        _store.TryGetValue(key, out var val);
        return Task.FromResult<string?>(val);
    }

    public Task SetSecretAsync(string key, string value, CancellationToken ct = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task DeleteSecretAsync(string key, CancellationToken ct = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}
