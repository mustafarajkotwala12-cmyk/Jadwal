using System;
using Jadwal.Domain.Models;
using Xunit;

namespace Jadwal.Domain.Tests;

public class FatimidHijriDateTests
{
    [Theory]
    [InlineData(2, true)]
    [InlineData(5, true)]
    [InlineData(8, true)]
    [InlineData(10, true)]
    [InlineData(13, true)]
    [InlineData(16, true)]
    [InlineData(19, true)]
    [InlineData(21, true)]
    [InlineData(24, true)]
    [InlineData(27, true)]
    [InlineData(29, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(1442, true)]  // 1442 % 30 == 2 -> Kabisa
    [InlineData(1445, true)]  // 1445 % 30 == 25 -> not Kabisa
    public void IsKabisa_FollowsFatimid30YearCycle(int year, bool expectedKabisa)
    {
        bool actual = FatimidHijriDate.IsKabisa(year);
        Assert.Equal(expectedKabisa, actual);
    }

    [Fact]
    public void DaysInMonth_AlternatesEvenOdd_AndZilhajLeapYear()
    {
        int commonYear = 1443; // 1443 % 30 = 3 (not Kabisa)
        int kabisaYear = 1442; // 1442 % 30 = 2 (Kabisa)

        // Even months (0, 2, 4, 6, 8, 10) have 30 days
        Assert.Equal(30, FatimidHijriDate.GetDaysInMonth(commonYear, 0));  // Moharram
        Assert.Equal(29, FatimidHijriDate.GetDaysInMonth(commonYear, 1));  // Safar
        Assert.Equal(30, FatimidHijriDate.GetDaysInMonth(commonYear, 2));  // Rabi I
        Assert.Equal(29, FatimidHijriDate.GetDaysInMonth(commonYear, 3));  // Rabi II
        Assert.Equal(30, FatimidHijriDate.GetDaysInMonth(commonYear, 4));  // Jumada I
        Assert.Equal(29, FatimidHijriDate.GetDaysInMonth(commonYear, 5));  // Jumada II
        Assert.Equal(30, FatimidHijriDate.GetDaysInMonth(commonYear, 6));  // Rajab
        Assert.Equal(29, FatimidHijriDate.GetDaysInMonth(commonYear, 7));  // Shabaan
        Assert.Equal(30, FatimidHijriDate.GetDaysInMonth(commonYear, 8));  // Ramadaan
        Assert.Equal(29, FatimidHijriDate.GetDaysInMonth(commonYear, 9));  // Shawwal
        Assert.Equal(30, FatimidHijriDate.GetDaysInMonth(commonYear, 10)); // Zilqadah

        // Zilhaj (month 11) is 29 in common year, 30 in Kabisa year
        Assert.Equal(29, FatimidHijriDate.GetDaysInMonth(commonYear, 11));
        Assert.Equal(30, FatimidHijriDate.GetDaysInMonth(kabisaYear, 11));
    }

    [Fact]
    public void MonthNames_LongShortAndArabic_AreAccurate()
    {
        var hDate = new FatimidHijriDate(1446, 0, 10);
        Assert.Equal("Moharram al-Haraam", hDate.MonthNameLong);
        Assert.Equal("Moharram", hDate.MonthNameShort);
        Assert.Contains("المحرّم", hDate.MonthNameArabic);
        Assert.Equal("10 Moharram al-Haraam 1446 AH", hDate.ToFormattedString());
        Assert.Contains("١٠", hDate.ToArabicString());
        Assert.Contains("١٤٤٦", hDate.ToArabicString());
    }

    [Fact]
    public void Roundtrip_FromGregorian_ToGregorian_PreservesDate()
    {
        // Test various dates across the year
        var testDates = new[]
        {
            new DateTime(2024, 7, 7),
            new DateTime(2025, 1, 1),
            new DateTime(2025, 3, 31),
            new DateTime(2026, 9, 11)
        };

        foreach (var original in testDates)
        {
            var hijri = FatimidHijriDate.FromGregorian(original);
            var backToGregorian = hijri.ToGregorian().Date;

            Assert.Equal(original.Date, backToGregorian);
        }
    }

    [Fact]
    public void DayOfWeek_MatchesSystemDayOfWeek()
    {
        var date = new DateTime(2026, 9, 11); // Friday
        var hijri = FatimidHijriDate.FromGregorian(date);

        Assert.Equal(DayOfWeek.Friday, date.DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, hijri.GetDayOfWeek());
    }
}
