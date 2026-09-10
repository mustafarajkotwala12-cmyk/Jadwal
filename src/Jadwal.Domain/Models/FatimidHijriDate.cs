using System;
using System.Linq;

namespace Jadwal.Domain.Models;

/// <summary>
/// Represents a date in the Fatimid Misri tabular lunar Hijri calendar.
/// Faithfully ports the astronomical Julian date conversions and 30-year cycle algorithm.
/// Note: Month is 0-indexed (0 = Moharram, 11 = Zilhaj) internally to match the canonical specification and dataset,
/// with helper properties/methods for 1-indexed usages.
/// </summary>
public readonly struct FatimidHijriDate : IEquatable<FatimidHijriDate>, IComparable<FatimidHijriDate>
{
    private static readonly int[] KabisaYearRemainders = [2, 5, 8, 10, 13, 16, 19, 21, 24, 27, 29];

    private static readonly int[] DaysInYear = [30, 59, 89, 118, 148, 177, 207, 236, 266, 295, 325];

    private static readonly int[] DaysIn30Years =
    [
        354,  708, 1063, 1417, 1771, 2126, 2480, 2834,  3189,  3543,
        3898, 4252, 4606, 4961, 5315, 5669, 6024, 6378,  6732,  7087,
        7441, 7796, 8150, 8504, 8859, 9213, 9567, 9922, 10276, 10631
    ];

    public static readonly string[] MonthNamesLongEn =
    [
        "Moharram al-Haraam",
        "Safar al-Muzaffar",
        "Rabi al-Awwal",
        "Rabi al-Aakhar",
        "Jumada al-Ula",
        "Jumada al-Ukhra",
        "Rajab al-Asab",
        "Shabaan al-Karim",
        "Ramadaan al-Moazzam",
        "Shawwal al-Mukarram",
        "Zilqadah al-Haraam",
        "Zilhaj al-Haraam"
    ];

    public static readonly string[] MonthNamesShortEn =
    [
        "Moharram",
        "Safar",
        "Rabi I",
        "Rabi II",
        "Jumada I",
        "Jumada II",
        "Rajab",
        "Shabaan",
        "Ramadaan",
        "Shawwal",
        "Zilqadah",
        "Zilhaj"
    ];

    public static readonly string[] MonthNamesArabic =
    [
        "المحرّم الحرام",
        "صفر المظفّر",
        "ربيع الأوّل",
        "ربيع الآخر",
        "جمادى الأولى",
        "جمادى الأخرى",
        "رجب الأصبّ",
        "شعبان الكريم",
        "رمضان المعظّم",
        "شوّال المكرّم",
        "ذو القعدة الحرام",
        "ذو الحجّة الحرام"
    ];

    public int Year { get; }
    /// <summary>
    /// 0-indexed month (0 = Moharram, 11 = Zilhaj)
    /// </summary>
    public int Month { get; }
    public int Day { get; }

    /// <summary>
    /// 1-indexed month (1 = Moharram, 12 = Zilhaj)
    /// </summary>
    public int MonthOneIndexed => Month + 1;

    public FatimidHijriDate(int year, int monthZeroIndexed, int day)
    {
        Year = year;
        Month = Math.Clamp(monthZeroIndexed, 0, 11);
        Day = Math.Max(1, day);
    }

    public static bool IsKabisa(int year)
    {
        int remainder = ((year % 30) + 30) % 30;
        return KabisaYearRemainders.Contains(remainder);
    }

    public static int GetDaysInMonth(int year, int monthZeroIndexed)
    {
        if (monthZeroIndexed == 11 && IsKabisa(year))
        {
            return 30;
        }

        return (monthZeroIndexed % 2 == 0) ? 30 : 29;
    }

    public int DayOfYear()
    {
        return (Month == 0) ? Day : (DaysInYear[Month - 1] + Day);
    }

    public string MonthNameLong => (Month >= 0 && Month < 12) ? MonthNamesLongEn[Month] : string.Empty;
    public string MonthNameShort => (Month >= 0 && Month < 12) ? MonthNamesShortEn[Month] : string.Empty;
    public string MonthNameArabic => (Month >= 0 && Month < 12) ? MonthNamesArabic[Month] : string.Empty;

    public static bool IsJulian(DateTime date)
    {
        if (date.Year < 1582) return true;
        if (date.Year == 1582)
        {
            if (date.Month < 10) return true;
            if (date.Month == 10 && date.Day < 5) return true;
        }
        return false;
    }

    public static double GregorianToAjd(DateTime date)
    {
        int year = date.Year;
        int month = date.Month; // 1 to 12
        double day = date.Day
            + date.Hour / 24.0
            + date.Minute / 1440.0
            + date.Second / 86400.0
            + date.Millisecond / 86400000.0;

        if (month < 3)
        {
            year--;
            month += 12;
        }

        double b;
        if (IsJulian(date))
        {
            b = 0;
        }
        else
        {
            int a = (int)Math.Floor(year / 100.0);
            b = 2 - a + (int)Math.Floor(a / 4.0);
        }

        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
    }

    public static DateTime AjdToGregorian(double ajd)
    {
        double z = Math.Floor(ajd + 0.5);
        double f = (ajd + 0.5 - z);
        double a;

        if (z < 2299161)
        {
            a = z;
        }
        else
        {
            double alpha = Math.Floor((z - 1867216.25) / 36524.25);
            a = z + 1 + alpha - Math.Floor(0.25 * alpha);
        }

        double b = a + 1524;
        double c = Math.Floor((b - 122.1) / 365.25);
        double d = Math.Floor(365.25 * c);
        double e = Math.Floor((b - d) / 30.6001);

        double dayDouble = b - d - Math.Floor(30.6001 * e) + f;
        int day = (int)Math.Floor(dayDouble);

        double hrsDouble = (dayDouble - day) * 24.0;
        int hrs = Math.Clamp((int)Math.Floor(hrsDouble), 0, 23);

        double minDouble = (hrsDouble - hrs) * 60.0;
        int min = Math.Clamp((int)Math.Floor(minDouble), 0, 59);

        double secDouble = (minDouble - min) * 60.0;
        int sec = Math.Clamp((int)Math.Floor(secDouble), 0, 59);

        int month = (e < 14) ? (int)(e - 2) : (int)(e - 14);
        int year = (month < 2) ? (int)(c - 4715) : (int)(c - 4716);

        // month is 0-indexed in JS Date constructor (0 = Jan, 11 = Dec), but in C# DateTime it is 1-12
        int gregorianMonth = month + 1;
        day = Math.Max(1, day);

        return new DateTime(year, Math.Clamp(gregorianMonth, 1, 12), Math.Clamp(day, 1, DateTime.DaysInMonth(year, gregorianMonth)), hrs, min, sec);
    }

    public double ToAjd()
    {
        int y30 = (int)Math.Floor(Year / 30.0);
        double ajd = 1948083.5 + y30 * 10631 + DayOfYear();
        if (Year % 30 != 0)
        {
            int index = (Year - y30 * 30 - 1);
            if (index >= 0 && index < DaysIn30Years.Length)
            {
                ajd += DaysIn30Years[index];
            }
        }
        return ajd;
    }

    public static FatimidHijriDate FromAjd(double ajd)
    {
        int i = 0;
        double left = Math.Floor(ajd - 1948083.5);
        int y30 = (int)Math.Floor(left / 10631.0);

        left -= y30 * 10631;
        while (i < DaysIn30Years.Length && left > DaysIn30Years[i])
        {
            i += 1;
        }

        int year = (int)Math.Round(y30 * 30.0 + i);
        if (i > 0)
        {
            left -= DaysIn30Years[i - 1];
        }

        i = 0;
        while (i < DaysInYear.Length && left > DaysInYear[i])
        {
            i += 1;
        }

        int month = i;
        int date = (i > 0) ? (int)Math.Round(left - DaysInYear[i - 1]) : (int)Math.Round(left);

        return new FatimidHijriDate(year, month, Math.Max(1, date));
    }

    public static FatimidHijriDate FromGregorian(DateTime date)
    {
        // Use midday 12:00 to avoid timezone/day boundary rounding discrepancies
        var noonDate = new DateTime(date.Year, date.Month, date.Day, 12, 0, 0);
        return FromAjd(GregorianToAjd(noonDate));
    }

    public DateTime ToGregorian()
    {
        return AjdToGregorian(ToAjd());
    }

    public DayOfWeek GetDayOfWeek()
    {
        // (toAJD() + 1.5) % 7 where 0 is Sunday, 1 is Monday ...
        double val = (ToAjd() + 1.5) % 7.0;
        int dayIndex = ((int)Math.Floor(val) % 7 + 7) % 7;
        return (DayOfWeek)dayIndex;
    }

    public string ToFormattedString() => $"{Day} {MonthNameLong} {Year} AH";

    public string ToArabicString()
    {
        string arabicDay = ToArabicDigits(Day);
        string arabicYear = ToArabicDigits(Year);
        return $"{arabicDay} {MonthNameArabic} {arabicYear} هـ";
    }

    private static string ToArabicDigits(int value)
    {
        var chars = value.ToString().Select(c => c switch
        {
            '0' => '٠',
            '1' => '١',
            '2' => '٢',
            '3' => '٣',
            '4' => '٤',
            '5' => '٥',
            '6' => '٦',
            '7' => '٧',
            '8' => '٨',
            '9' => '٩',
            _ => c
        });
        return new string(chars.ToArray());
    }

    public bool Equals(FatimidHijriDate other) => Year == other.Year && Month == other.Month && Day == other.Day;
    public override bool Equals(object? obj) => obj is FatimidHijriDate other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Year, Month, Day);
    public static bool operator ==(FatimidHijriDate left, FatimidHijriDate right) => left.Equals(right);
    public static bool operator !=(FatimidHijriDate left, FatimidHijriDate right) => !left.Equals(right);

    public int CompareTo(FatimidHijriDate other)
    {
        int cmp = Year.CompareTo(other.Year);
        if (cmp != 0) return cmp;
        cmp = Month.CompareTo(other.Month);
        if (cmp != 0) return cmp;
        return Day.CompareTo(other.Day);
    }

    public override string ToString() => ToFormattedString();
}
