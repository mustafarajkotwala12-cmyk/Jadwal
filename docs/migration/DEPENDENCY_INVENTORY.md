# Dependency Inventory: Jadwal Desktop

**Date**: 2026-09-10  
**Project**: Jadwal  
**Scope**: All external libraries, packages, system frameworks, and target replacements for C# / .NET 10 / Avalonia.

---

## 1. Swift Implementation Dependencies

| Dependency | Scope | Target Replacement in C# / .NET 10 |
|---|---|---|
| `SwiftUI` | UI Framework | `Avalonia UI` (11.x) |
| `AppKit` (NSApplication, NSStatusItem) | macOS Windowing & Tray | `Avalonia.Desktop` + `TrayIcon` / `Jadwal.Platform.MacOS` |
| `Foundation` | Standard Library | .NET 10 BCL (`System`, `System.IO`, `System.Text.Json`) |
| `Combine` (Timer.publish) | Reactive updates | `System.Reactive` / `Observable` or `DispatcherTimer` |
| `UserNotifications` | Notifications | `INotificationService` -> Windows Toast / macOS notifications |
| `Security.framework` (Keychain) | Credential storage | `ISecureStorage` -> Windows Credential Manager / macOS Keychain |
| `SwiftPM` | Build tool | `dotnet` CLI, MSBuild, NuGet |

---

## 2. Python Integration Dependencies

| Python Package | Version | Usage in Python | Target Replacement in C# |
|---|---|---|---|
| `playwright` | $\ge 1.40.0$ | Automated ITS portal login | `Microsoft.Playwright` or C# HttpClient with cookie handling |
| `openpyxl` | $\ge 3.1.0$ | Reading timetable `.xlsx` spreadsheets | `ClosedXML` / `ExcelDataReader` (or direct API JSON) |
| `pandas` | $\ge 2.0.0$ | DataFrame table extraction | BCL Linq / `System.Text.Json` |
| `asyncio` | Built-in | Async execution | `async` / `await`, `Task` |

---

## 3. C# / .NET Target Dependencies & NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| `Avalonia` | 11.2.* | Cross-platform XAML desktop UI |
| `Avalonia.Desktop` | 11.2.* | macOS / Windows native windowing backends |
| `Avalonia.Themes.Fluent` | 11.2.* | Fluent design theme for desktop |
| `CommunityToolkit.Mvvm` | 8.3.* | Source-generated MVVM ViewModels, Commands, ObservableObject |
| `Microsoft.Extensions.DependencyInjection` | 10.0.* | IoC container for service wiring |
| `System.Text.Json` | Built-in | Fast, secure JSON serialization |
| `xunit` / `xunit.v3` | Latest | Cross-platform test framework |
| `FluentAssertions` | Latest | Readable test assertions |
