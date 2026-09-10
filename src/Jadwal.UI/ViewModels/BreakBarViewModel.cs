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
}
