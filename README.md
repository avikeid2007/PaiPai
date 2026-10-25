# Pai Pai

Personal Android expense tracker built with .NET MAUI. Transactions, reminders and financial SMS processing are stored locally.

## Project layout

- `src/PaiPai.Core`: ledger models, SMS and bill-message parsing, and categorization
- `src/PaiPai.Data`: SQLite repositories, import checkpoints, backups and local settings
- `src/PaiPai.Mobile`: Android app screens and platform integrations
- `tests/PaiPai.Core.Tests`: parser tests and a 172-row synthetic Indian payment SMS corpus

The corpus fixture includes its source and CC BY 4.0 attribution in `tests/PaiPai.Core.Tests/Fixtures/IndianPaymentSmsCorpus`.

## Build and run

```powershell
dotnet build src/PaiPai.Mobile -t:Run -f net10.0-android
dotnet test tests/PaiPai.Core.Tests
```

Pai Pai can import the inbox once, stop and resume an interrupted scan, and catch up on later messages. New SMS can be processed by the Android receiver after **Enable live SMS detection** is enabled in Settings. Clear completed transactions are added automatically; unclear financial messages and reversals stay in Review. OTPs and unrelated messages are skipped. Transactions can be edited or deleted later. Release APKs remove `INTERNET` permission. Debug builds include it for Visual Studio's local debugger transport; the app itself does not make network requests.

## Current scope

- Home, manual transaction entry, transaction history, search, filters, editing and deletion
- Monthly income and spending summaries, with category and account/card breakdowns
- SMS import, live detection, catch-up sync and a local review queue
- Bill reminders and recurring subscriptions, including rescheduling after device restart
- Permission controls, CSV export, full JSON backup/restore and on-device data clearing

Build validation currently succeeds for `net10.0-android` with no warnings. The earlier parser corpus run passed all 172 synthetic examples. Real bank messages have not yet been validated on this phone; SMS inbox and live SMS permissions remain opt-in in Android.
