namespace Jadwal.Application.Models;

public record CityOption(
    int Id,
    string DisplayName,
    string CookieCityName,
    string Latitude,
    string Longitude,
    string Timezone
)
{
    public override string ToString() => DisplayName;

    public static readonly CityOption Mumbai = new(116, "Mumbai", "Mumbai", "19.07283", "72.88261", "5.5");
    public static readonly CityOption Surat = new(145, "Surat", "Surat", "21.17024", "72.831062", "5.5");
    public static readonly CityOption Karachi = new(365, "Karachi", "Karachi", "24.946218", "67.005615", "5");
    public static readonly CityOption Nairobi = new(321, "Nairobi", "Nairobi", "-1.362863", "36.834583", "3");
    public static readonly CityOption Mombasa = new(320, "Mombasa", "Mombasa", "-4.036878", "39.669571", "3");
    public static readonly CityOption KuwaitCity = new(323, "Kuwait City", "Kuwait+city", "29.378586", "47.990341", "3");
    public static readonly CityOption Ahmedabad = new(64, "Ahmedabad", "Ahmedabad", "23.033863", "72.585022", "5.5");
    public static readonly CityOption Pune = new(132, "Pune", "Pune", "18.516726", "73.856255", "5.5");
    public static readonly CityOption Udaipur = new(151, "Udaipur", "Udaipur", "24.58330000", "73.68330000", "5.5");
    public static readonly CityOption Indore = new(98, "Indore", "Indore", "22.71666670", "75.83333330", "5.5");
    public static readonly CityOption NewDelhi = new(124, "New Delhi", "New+delhi", "28.6448", "77.216721", "5.5");
    public static readonly CityOption Chennai = new(87, "Chennai", "Chennai", "13.067439", "80.237617", "5.5");
    public static readonly CityOption Kolkata = new(105, "Kolkata", "Kolkata", "22.572645", "88.363892", "5.5");
    public static readonly CityOption Nagpur = new(122, "Nagpur", "Nagpur", "21.16666670", "79.20000000", "5.5");
    public static readonly CityOption Hyderabad = new(684, "Hyderabad", "Hyderabad", "17.440081", "78.491684", "5.5");
    public static readonly CityOption NaviMumbai = new(713, "Navi Mumbai", "Navi+Mumbai", "19.0330", "73.0297", "5.5");

    public static readonly IReadOnlyList<CityOption> All = new[]
    {
        Mumbai,
        Surat,
        Karachi,
        Nairobi,
        Mombasa,
        KuwaitCity,
        Ahmedabad,
        Pune,
        Udaipur,
        Indore,
        NewDelhi,
        Chennai,
        Kolkata,
        Nagpur,
        Hyderabad,
        NaviMumbai
    };
}

public record PrayerTimings(
    DateOnly Date,
    int CityId,
    string CityName,
    string Zawaal,
    string ZuhrEnd,
    DateTime CachedAt
);
