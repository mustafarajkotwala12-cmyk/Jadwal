using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Jadwal.UI.ViewModels;

namespace Jadwal.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsViewModel vm)
        {
            vm.PickFileHandler = async () =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return null;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Timetable File (.json or .xlsx)",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Timetable Files (*.json, *.xlsx)")
                        {
                            Patterns = new[] { "*.json", "*.xlsx" }
                        }
                    }
                });

                return files.Count > 0 ? files[0].TryGetLocalPath() : null;
            };
        }
    }
}
