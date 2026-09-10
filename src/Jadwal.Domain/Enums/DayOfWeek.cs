namespace Jadwal.Domain.Enums;

public enum JadwalDayOfWeek
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
    Sunday = 7
}

public static class JadwalDayOfWeekExtensions
{
    public static string ToEnglishString(this JadwalDayOfWeek day) => day switch
    {
        JadwalDayOfWeek.Monday => "Monday",
        JadwalDayOfWeek.Tuesday => "Tuesday",
        JadwalDayOfWeek.Wednesday => "Wednesday",
        JadwalDayOfWeek.Thursday => "Thursday",
        JadwalDayOfWeek.Friday => "Friday",
        JadwalDayOfWeek.Saturday => "Saturday",
        JadwalDayOfWeek.Sunday => "Sunday",
        _ => "Monday"
    };

    public static string ToArabicString(this JadwalDayOfWeek day) => day switch
    {
        JadwalDayOfWeek.Monday => "يوم الاثنين",
        JadwalDayOfWeek.Tuesday => "يوم الثلاثاء",
        JadwalDayOfWeek.Wednesday => "يوم الاربعاء",
        JadwalDayOfWeek.Thursday => "يوم الخميس",
        JadwalDayOfWeek.Friday => "يوم الجمعة",
        JadwalDayOfWeek.Saturday => "يوم السبت",
        JadwalDayOfWeek.Sunday => "يوم الأحد",
        _ => "يوم الاثنين"
    };

    public static JadwalDayOfWeek ParseFromDayString(string dayStr)
    {
        if (string.IsNullOrWhiteSpace(dayStr)) return JadwalDayOfWeek.Monday;
        var trimmed = dayStr.Trim().ToLowerInvariant();
        if (trimmed.Contains("mon") || trimmed.Contains("اثنين")) return JadwalDayOfWeek.Monday;
        if (trimmed.Contains("tue") || trimmed.Contains("ثلاثاء")) return JadwalDayOfWeek.Tuesday;
        if (trimmed.Contains("wed") || trimmed.Contains("اربعاء")) return JadwalDayOfWeek.Wednesday;
        if (trimmed.Contains("thu") || trimmed.Contains("خميس")) return JadwalDayOfWeek.Thursday;
        if (trimmed.Contains("fri") || trimmed.Contains("جمعة")) return JadwalDayOfWeek.Friday;
        if (trimmed.Contains("sat") || trimmed.Contains("سبت")) return JadwalDayOfWeek.Saturday;
        if (trimmed.Contains("sun") || trimmed.Contains("أحد")) return JadwalDayOfWeek.Sunday;
        return JadwalDayOfWeek.Monday;
    }
}
