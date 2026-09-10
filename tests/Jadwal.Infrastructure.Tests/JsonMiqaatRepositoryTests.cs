using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Jadwal.Infrastructure.Persistence;
using Xunit;

namespace Jadwal.Infrastructure.Tests;

public class JsonMiqaatRepositoryTests
{
    [Fact]
    public async Task GetAllMiqaatsAsync_LoadsFullDatasetSuccessfully()
    {
        var repo = new JsonMiqaatRepository();
        var all = await repo.GetAllMiqaatsAsync();

        all.Should().NotBeNull();
        all.Should().NotBeEmpty();

        // Check that all 12 months (0-11) are represented in the dataset
        var distinctMonths = all.Select(r => r.Month).Distinct().OrderBy(m => m).ToList();
        distinctMonths.Should().Equal(Enumerable.Range(0, 12));
    }

    [Theory]
    [InlineData(0, 10, "Yawme Ashura")]
    [InlineData(2, 16, "Urus Syedna Mohammed Burhanuddin (AQ)")]
    [InlineData(8, 22, "Lailat al-Qadr")]
    [InlineData(11, 18, "Yawm al-Eid-e-Gadhir-e-Khum")]
    public async Task GetMiqaatsForHijriDayAsync_ReturnsExpectedLandmarkMiqaats(int month, int date, string expectedTitleSubstring)
    {
        var repo = new JsonMiqaatRepository();
        var miqaats = await repo.GetMiqaatsForHijriDayAsync(month, date);

        miqaats.Should().NotBeNull();
        miqaats.Should().NotBeEmpty();
        miqaats.Should().Contain(m => m.Title.Contains(expectedTitleSubstring, System.StringComparison.OrdinalIgnoreCase));
    }
}
