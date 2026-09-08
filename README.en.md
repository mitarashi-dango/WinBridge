# WinBridge — Build Your Own Windows 11 Control Panel

[Download the latest release](https://github.com/mitarashi-dango/WinBridge/releases/latest) · [日本語・開発者向けドキュメント](README.md)

WinBridge is a free Windows 11 desktop app for building your own custom control panel. Choose the Windows settings you need, arrange shortcuts your way, hide unused modules, and mark favorites to create a settings workspace that fits how you use your PC.

Your personal home screen and sidebar bring together shortcuts to official Windows settings and built-in controls such as screen-off and sleep timers.

## Make it yours

1. **Choose settings:** add shortcuts for sound, Bluetooth, mouse, display, and more from the built-in catalog.
2. **Keep what you need:** hide unused modules and remove shortcuts you no longer use.
3. **Arrange your panel:** reorder items by dragging them or using the up and down buttons.
4. **Keep favorites handy:** mark frequently used items as favorites and choose which settings to pin to the sidebar.

For example, collect microphone, camera, and sound settings for online meetings, or keep display and Bluetooth settings within easy reach. Your selections and ordering are saved for the next launch.

## Features

- **Custom control panel:** add and remove settings shortcuts, show or hide modules, reorder items, and organize favorites and sidebar pins. Hardware-dependent settings appear when supported devices or features are detected.
- **Screen and sleep timers:** view and change the active power plan's timeouts, with separate plugged-in and battery settings where available.
- **Windows Update:** open update checks, update history, active hours, and advanced options in Windows Settings.
- **File Explorer options:** manage file name extensions and hidden files in the EXE and portable editions. The Microsoft Store edition opens Windows Folder Options.
- **Devices and connections:** review device status and organize shortcuts for Bluetooth, printers, cameras, mouse, and audio input settings.
- **Start and Windows Search:** open search and indexing settings and read troubleshooting guidance.

WinBridge changes only a limited set of settings directly and opens the official Windows settings screens for other operations. It does not disable Windows Update or automatically install drivers.

## Download and install

Requires **Windows 11 x64**. Release packages include the .NET runtime; no separate runtime installation is needed.

1. Open the [latest WinBridge release](https://github.com/mitarashi-dango/WinBridge/releases/latest).
2. Download the file ending in `win-x64-Setup.exe` for the installer, or `win-x64-portable.zip` for the portable edition.
3. Run the installer, or extract the ZIP and launch `WinBridge.exe` from the extracted folder.
4. Add the Windows settings you use and mark favorites for quick access.

The installer installs for the current user and normally does not require administrator privileges. Release checksums are provided in `SHA256SUMS.txt`.

## Languages and pricing

All features are free. Optional donations do not unlock additional features.

The interface supports Japanese, English, Spanish, Simplified Chinese, and Traditional Chinese. Choose a display language in the app settings, or follow the Windows display language automatically. Unsupported Windows languages fall back to English.

## Development

WinBridge is built with **C#, WPF, and .NET 10**. Building requires Windows and a .NET SDK capable of targeting .NET 10.

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

Run the automated tests with:

```powershell
dotnet run --project tests\WinBridge.Tests\WinBridge.Tests.csproj -c Release
```

See the [Japanese project documentation](README.md) for packaging, settings storage, Windows API usage, and current limitations.
