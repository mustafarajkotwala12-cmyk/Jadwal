using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jadwal.Domain.Models;

namespace Jadwal.Application.Interfaces;

public interface IMiqaatRepository
{
    Task<IReadOnlyList<DayMiqaatsRecord>> GetAllMiqaatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MiqaatItem>> GetMiqaatsForHijriDayAsync(int monthZeroIndexed, int day, CancellationToken ct = default);
}
