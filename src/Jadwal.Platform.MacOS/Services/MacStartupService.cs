using Jadwal.Application.Interfaces;

namespace Jadwal.Platform.MacOS.Services;

public class MacStartupService : IStartupService
{
    private const string PlistName = "com.jadwal.app.plist";
    private readonly string _plistPath;

    public MacStartupService()
    {
        var launchAgentsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents");
        Directory.CreateDirectory(launchAgentsDir);
        _plistPath = Path.Combine(launchAgentsDir, PlistName);
    }

    public Task<bool> IsLaunchAtStartupEnabledAsync(CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(_plistPath));
    }

    public async Task SetLaunchAtStartupAsync(bool enable, CancellationToken ct = default)
    {
        if (enable)
        {
            var appPath = Environment.ProcessPath ?? "/Applications/Jadwal.app/Contents/MacOS/Jadwal";
            var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.jadwal.app</string>
    <key>ProgramArguments</key>
    <array>
        <string>{appPath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
</dict>
</plist>";
            await File.WriteAllTextAsync(_plistPath, plistContent, ct);
        }
        else
        {
            if (File.Exists(_plistPath))
            {
                File.Delete(_plistPath);
            }
        }
    }
}
