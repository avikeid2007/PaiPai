# Pai Pai

Pai Pai is a personal expense tracker for Android. It combines manual transaction entry, bank SMS parsing, spending summaries, and bill reminders in a private, on-device app built with .NET MAUI.

## What it does

- Tracks expenses and income, with search, filters, and transaction details.
- Adds clear completed transaction matches from SMS automatically. Unclear financial messages and reversals go to Review.
- Imports SMS history with progress, safe stop, and resume checkpoints.
- Shows monthly spending by category and payment method or card.
- Keeps recurring bills and subscriptions visible and schedules local reminders.
- Exports transactions to CSV and creates or restores a full JSON backup.

## Privacy

- Transaction data and relevant SMS text are stored in the app's local SQLite database.
- OTPs and unrelated messages are skipped by the parser.
- SMS import and live SMS detection use separate Android permissions and can be enabled in Settings.
- Release builds remove Android's `INTERNET` permission. Debug builds include it for Visual Studio debugger connectivity; the app does not upload data.
- JSON backups contain financial data and stored SMS text. Keep backup files private.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/PaiPai.Core` | Ledger models, SMS and bill parsing, categorization |
| `src/PaiPai.Data` | SQLite repository, import checkpoints, settings, backups |
| `src/PaiPai.Mobile` | .NET MAUI Android app and platform integrations |
| `tests/PaiPai.Core.Tests` | Parser tests and synthetic payment SMS corpus |
| `doc` | Product plan and UI mockup reference |

The synthetic SMS corpus includes source and CC BY 4.0 attribution in `tests/PaiPai.Core.Tests/Fixtures/IndianPaymentSmsCorpus`.

## Requirements

- .NET 10 SDK with the MAUI Android workload
- Android SDK and platform tools (`adb`)
- Android phone with USB debugging enabled, or an Android emulator

## Build and install

Run these commands from the repository root in PowerShell:

```powershell
dotnet workload restore src/PaiPai.Mobile/PaiPai.Mobile.csproj
dotnet build src/PaiPai.Mobile/PaiPai.Mobile.csproj -f net10.0-android -c Debug
dotnet build src/PaiPai.Mobile/PaiPai.Mobile.csproj -f net10.0-android -c Release
adb devices -l
adb install -r src/PaiPai.Mobile/bin/Release/net10.0-android/com.paipai.app-Signed.apk
```

The Release APK is produced at `src/PaiPai.Mobile/bin/Release/net10.0-android/com.paipai.app-Signed.apk`. The `-r` option updates the installed app while preserving its app data.

## Build APK with GitHub Actions

The `Build Android APK` workflow runs on pushes to `main`, pull requests, and manual dispatch. It runs the core tests, builds the Android Release APK, and uploads it as the `PaiPai-Android-Release` artifact for 14 days. To download it, open the successful workflow run under the repository's **Actions** tab and select the artifact.

Run the parser test suite with:

```powershell
dotnet test tests/PaiPai.Core.Tests/PaiPai.Core.Tests.csproj
```

## Branding

- Android launcher icon: `src/PaiPai.Mobile/Resources/AppIcon/appicon.svg`
- In-app Pai Pai mark: `src/PaiPai.Mobile/Resources/Images/paipai_mark.svg`
- Tab icons: `src/PaiPai.Mobile/Resources/Images/nav_*.svg`

The app uses a warm neutral canvas, evergreen surfaces, and lime accents. Update the SVG assets to change the mark or navigation symbols.

## Build status

The latest Release build completed successfully with no errors. MAUI currently reports XAML binding performance warnings (`XC0022`) where bindings are not compiled with `x:DataType`. The parser corpus has previously passed all 172 synthetic examples; this does not replace validation against real bank messages on the target phone.
