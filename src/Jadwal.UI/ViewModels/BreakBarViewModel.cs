using Avalonia;
using Jadwal.Domain.Models;

namespace Jadwal.UI.ViewModels;

public class BreakBarViewModel
{
    public BreakBarInfo Info { get; }

    public BreakBarViewModel(BreakBarInfo info)
    {
        Info = info;
    }

    public string Name => Info.Name;
    public string NamaazNote => Info.NamaazNote;
    public string TimeRange => Info.TimeRangeFormatted;
    public string Duration => Info.DurationFormatted;
    public string Icon => Info.Icon;
    public string? ZawaalTime => Info.ZawaalTime;
    public string? ZuhrEndTime => Info.ZuhrEndTime;
    public bool HasNamaazTimings => !string.IsNullOrEmpty(ZawaalTime) && !string.IsNullOrEmpty(ZuhrEndTime);

    public bool IsRefreshmentBreak => Info.Id.Contains("recess") || Info.Id.Contains("morning");
    public bool IsNamazBreak => Info.Id.Contains("namaz") || Info.Id.Contains("jumua");

    // Dynamic sizing: thinner, sleeker refreshment bar per user request
    public Thickness BarPadding => IsRefreshmentBreak ? new Thickness(12, 3) : new Thickness(16, 7);
    public Thickness BarMargin => IsRefreshmentBreak ? new Thickness(0, 3) : new Thickness(0, 6);
    public CornerRadius BarCornerRadius => IsRefreshmentBreak ? new CornerRadius(8) : new CornerRadius(12);
    public double IconFontSize => IsRefreshmentBreak ? 12 : 16;
    public double TitleFontSize => IsRefreshmentBreak ? 11 : 13;
    public double NoteFontSize => IsRefreshmentBreak ? 10 : 12;
    public double TimeFontSize => IsRefreshmentBreak ? 10 : 11;
}
