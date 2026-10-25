# Pai Pai: Final Project Plan

Pai Pai is a personal Android app that reads your SMS inbox, detects bank, card and UPI messages, and turns them into a clean expense history. Everything stays on your phone.

## Current implementation status (25 October 2026)

- The app is open on the connected Motorola Razr 50 Ultra. This set of fixes is being build-verified; it has not been installed over the running app.
- The parser passes its unit tests and has zero transaction-field or decision mismatches across the 172-row synthetic corpus. This does not replace validation against your bank messages.
- One-time import, stop/resume, live SMS receiver, catch-up sync and local review are implemented. Clear completed matches are added automatically; unclear financial messages and reversals stay in Review.
- Transaction history/details/editing, monthly summaries, category and card/account breakdowns, bills, notifications, CSV export and JSON backup/restore are implemented. First inbox import and parsing still need validation against real bank messages.

## 1. Goals

- Track expenses automatically from SMS, with no manual typing for most transactions.
- Import your existing inbox once, then keep detecting new messages as they arrive.
- Handle multiple banks, credit cards and UPI payments with one general parser.
- Pull out other useful information: income and credits, balances, bill due dates, subscriptions, travel and order updates.
- Keep all data on the device. No account, no server, no cloud.

## 2. Decisions so far

| Area | Decision |
| --- | --- |
| Platform | Android only |
| Use | Personal use only, installed as an APK (no Play Store) |
| Message access | Reads the inbox after the user taps Import SMS; live detection has its own opt-in permission |
| Reading mode | One-time import, ongoing SMS receiver and catch-up sync |
| Storage | On-device SQLite |
| Developer background | .NET / C# |
| Framework | .NET MAUI (final recommendation); Uno Platform is a valid alternative with the same Android access |
| Currency | INR (₹) by default, other currencies later |

## 3. Features

### Version 1

- **SMS import:** On user request, read the inbox and build history. Skip unrelated messages and OTPs; retain relevant financial messages locally for review.
- **Live detection:** Process new SMS locally; add clear completed transactions automatically and send unclear financial messages or reversals to Review.
- **Transaction types:** Debits (expenses), credits (income, refunds), card spends, UPI payments, ATM withdrawals.
- **Extracted fields:** Amount, date, merchant or payee, payment method, account or card last digits, balance, reference number.
- **Categories:** Built-in categories include food, transport, shopping, bills and other. User-managed category and merchant rules are future work.
- **Review list:** Confirm or ignore unclear financial messages and reversals; clear parsed transactions are already added.
- **Manual entry:** Add expense or income with date, category, payment method, account/card digits and note; edit or delete history entries.
- **Summaries:** Monthly spending and income, with category and account/card breakdowns.
- **Search and filter:** Search transactions and filter by direction and month.
- **Bill reminders:** Add or import due messages, set monthly or yearly cadence, mark paid or dismiss, and receive local notifications when allowed. Recurring items create the next due date when paid.
- **Data tools:** Export transactions as CSV, back up or restore local data as JSON, and clear app data.

### Later versions

- Monthly budgets with progress and alerts.
- Subscription detection and a renewals calendar.
- Merchant learning and user-managed categories.
- Recurring expense detection.
- Travel and order tracking views.
- Multi-currency support.
- Encryption at rest and encrypted backups.

## 4. Tech stack

- **UI framework:** .NET MAUI, targeting Android only.
- **Language:** C# with XAML.
- **Architecture:** MVVM using CommunityToolkit.Mvvm.
- **Storage:** SQLite through sqlite-net-pcl.
- **SMS access:** Android ContentResolver for inbox import and BroadcastReceivers for incoming SMS and device restart.
- **Parsing:** Regex-based C# parser in a separate class library.
- **Visual summaries:** Native MAUI progress bars.
- **Notifications:** Android local notifications for bill reminders.
- **Testing:** xUnit for the parser and data layer.
- **Tooling:** Visual Studio or Rider, Android emulator and a physical phone.

## 5. Architecture

The solution is split into layers so the parser and data logic can be tested without a phone.

1. **App (MAUI project):** Pages, ViewModels, navigation, dependency injection.
2. **Platforms/Android:** SMS reader and receiver, runtime permissions, alarm scheduling and notifications. This is the only code that touches Android APIs.
3. **Core library:** Models, parsing engine, categorization rules, summary calculations.
4. **Data library:** SQLite database, repositories, migrations.
5. **Tests:** xUnit projects for Core and Data.

The Android layer implements mobile service interfaces for SMS import and reminder scheduling, keeping Android API calls out of the Core and Data libraries.

## 6. Data model

- **RawSmsMessage:** SMS id, sender, body, received time, parse result and review status for relevant financial messages. Unrelated messages and OTPs are not retained.
- **LedgerEntry:** Amount, direction, date, merchant, category, payment method, account/card last digits, balance, reference, source and confirmation state.
- **SmsImportState:** Resumable full-import checkpoint; the inbox high-water mark is stored in AppSetting.
- **BillReminder:** Name, optional amount, due date, account/card hint, cadence, status and notification preferences.
- **AppSetting:** Local settings and the inbox high-water mark.

## 7. SMS pipeline

### One-time import

1. Open SMS import and tap the import button; then request READ\_SMS permission.
2. Read the inbox newest-first through ContentResolver.
3. Run each message through the parser. Skip unrelated messages; store financial matches and unclear financial messages as `RawSmsMessage` records.
4. Add clear completed matches automatically; keep unclear financial messages and reversals in Review. Show progress and checkpoint each processed inbox row so an interrupted import can stop safely and resume.
5. Confirm or ignore review items; added transactions count toward spending and can be edited or deleted later.

### Ongoing detection

1. A BroadcastReceiver listens for incoming SMS after the user grants RECEIVE\_SMS.
2. The receiver parses the message and stores only relevant financial text and its result locally.
3. On app open, catch-up sync reads inbox rows newer than the saved high-water mark if READ\_SMS is allowed.
4. Bill alarms are rescheduled after device restart; Android battery behavior can still delay delivery.

### Avoiding duplicates

- Skip any message whose SMS id is already stored and deduplicate near-identical messages by sender, body and receive time.
- Prevent duplicate ledger entries by SMS id, or by matching reference and amount when available.
- A future improvement is to merge separate bank and UPI alerts for a single payment.

## 8. Parser design

The parser runs in stages, so each stage can be tested on its own.

1. **Classify:** Decide whether the message is a transaction, an OTP, a promotion, a bill reminder, a balance alert, or something else. OTPs and promotions are skipped and not stored.
2. **Direction:** Words such as debited, spent, paid, sent, withdrawn, purchase mean expense. Words such as credited, received, refund, deposited mean income.
3. **Amount:** Match patterns for Rs, Rs., INR and ₹, with commas and decimals.
4. **Merchant or payee:** Look for text after "at", "to", "towards", "VPA" or "UPI/", then clean the name.
5. **Instrument:** Detect card, UPI, net banking, ATM or wallet, and the last digits of the account or card.
6. **Balance and reference:** Extract available balance, UPI reference or transaction id when present.
7. **Date:** Use the date in the message when present, otherwise the received time.
8. **Categorize:** Apply built-in keyword rules, then fall back to "Other".

A relevant financial message the parser cannot understand is kept in the local "Needs review" list. Merchant rule learning is future work.

## 9. Screens

- **Home:** This month's confirmed spending, quick actions, review/bill counts and recent transactions.
- **Transactions:** History with search and month/direction filters; tap a transaction to view details, edit or delete it.
- **Needs review:** Confirm or ignore unclear financial messages and reversals.
- **Summary:** Monthly income and spending by category and payment account/card hint.
- **Bills:** Upcoming and overdue bill reminders, recurring subscriptions, add/edit, mark paid and dismiss.
- **Settings:** Permission controls, import access, CSV export, JSON backup/restore and clear all app data.

## 10. Permissions and privacy

- **Permissions:** READ\_SMS is requested by Import SMS; RECEIVE\_SMS and POST\_NOTIFICATIONS are opt-in in Settings or when saving a bill. RECEIVE\_BOOT\_COMPLETED restores alarms after restart.
- **Data stays local:** Release APKs remove INTERNET permission. Debug builds retain it so Visual Studio can connect its local debugger; app features make no network requests.
- **OTPs:** Detected and skipped; never stored in the database.
- **Encryption:** Database encryption is not implemented; backups include stored financial message text and should be kept private.
- **Delete everything:** One action in Settings wipes the database.

## 11. Project structure

```
ExpenseTracker/
  ExpenseTracker.App/          MAUI project (Pages, ViewModels, Platforms/Android)
  ExpenseTracker.Core/         Models, Parser, Categorization, Summaries
  ExpenseTracker.Data/         SQLite, Repositories, Migrations
  ExpenseTracker.Core.Tests/   xUnit parser and logic tests
  ExpenseTracker.Data.Tests/   xUnit data tests
```

## 12. Milestones

| Phase | Deliverable | Outcome | Status |
| --- | --- | --- | --- |
| 1. Foundation | Projects, SQLite, models, dependency injection | App launches | Complete |
| 2. Parser | Parsing engine and corpus evaluation | 172 synthetic examples pass with zero mismatches | Complete for synthetic corpus; real bank validation pending |
| 3. SMS import | Permission flow, inbox reader, progress, stop and resume | Clear matches added; unclear financial messages reviewed | Implemented; first import on personal inbox pending |
| 4. Live detection | SMS receiver, notifications, catch-up sync | Clear matches added; uncertain SMS enter local review | Implemented; enable permission on phone to activate |
| 5. Core screens | Home, transactions, edit, needs review, manual entry | Day-to-day use is possible | Implemented |
| 6. Summaries | Monthly totals, category and account/card views | Spending insights | Implemented |
| 7. Bills and extras | Bill reminders, recurring subscriptions | Payment due dates stay visible | Implemented; real bank bill-message validation pending |
| 8. Polish | Export, backup/restore, local data controls | Personal APK can be managed safely | CSV export, JSON backup/restore, settings and clear-data action implemented; budgets, encryption and app icon remain future work |

## 13. Testing

- **Parser tests:** Current corpus is synthetic, with expected results for bank, card and UPI examples. Real, anonymized messages from your banks and apps are still needed to validate the parser on your inbox. Every parsing bug becomes a new test.
- **Data tests:** Duplicate handling, rule application and summary totals.
- **Device checks:** Run the inbox import and live SMS receiver on the phone after choosing to grant the relevant Android permissions. Background delivery and battery behavior need real-device use.
- **Edge cases:** Failed transactions, reversals, refunds, multiple amounts in one message, messages in other languages.

## 14. Build and install

1. Build a release APK from Visual Studio or with `dotnet publish -f net10.0-android`.
2. Sign it with a keystore you create and keep safe.
3. Copy the APK to your phone and install it, allowing installs from that source.
4. Grant the SMS and notification permissions, then turn off battery optimization for the app.

## 15. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Bank message formats differ and change | General parser plus a review list and learned rules |
| Phone manufacturers delay background delivery | Catch-up sync on app open; test receiver delivery and battery behavior on the phone |
| Duplicate transactions from several alerts | Reference-number matching and merge rules |
| Sensitive data on the device | No network access; OTPs are discarded; backup files include financial message text; encryption is future work |
| Large inboxes make import slow | Batched reading, progress indicator, safe stop and resumable import |
| Android version changes to SMS or background rules | Keep Android code isolated in one layer and test on your actual phone |

## 16. Remaining user-side steps

- In Settings, choose whether to grant inbox access, live SMS detection and notifications.
- Install the updated build, run the first inbox import when ready, and review only uncertain messages on the phone.
- If parser corrections are needed, use a few anonymized bank messages with names and full account numbers removed.
- Use the app on the Motorola Razr 50 Ultra to check screen behavior, notifications and battery handling in everyday use.

## 17. Final recommendation

- **App name:** Pai Pai.
- **Framework:** .NET MAUI, Android only, installed on the Motorola Razr 50 Ultra. Keep layouts flexible for its cover screen.
- **SMS:** One-time inbox import plus live detection of new messages, covering all messages, processed on the device only.
- **Manual entry:** Included for cash, bank transfers, cheques and other payments with no SMS.
- **On-device AI model:** Skipped for now. Rules and the review list do the parsing. Revisit after checking how many real messages need review.
- **Parser tuning:** Review uncertain messages against real bank alerts; do not share OTPs or full account numbers.
- **Next step:** Install the updated build, then decide when to run the inbox import. Tune parsing from anonymized real examples that you choose to share; keep OTPs and full account numbers private.

## 18. Design direction (from the mockup)

The mockup has three screens at phone size: Home, Add expense and Summary. [Open the Pai Pai mockup](https://claude.ai/artifact/1mX8D62myEpH3tPP2riNy1).

- **Look:** Cool off-white background (#F5F6F3), ink-navy text and buttons (#14202B), saffron used only to draw attention, such as the review banner (#FBEBC8).
- **Type:** Plus Jakarta Sans. The monthly total is the largest element on Home.
- **Category colors:** Rent blue (#2E4A7D), Food teal (#1E8C7A), Transport saffron (#E8A317), Shopping plum (#7B4B94), Bills coral (#D9594C). Every color also has a text label.
- **Home:** Monthly total, a segmented category bar, a "messages need review" banner, and recent transactions tagged "from SMS" or "manual".
- **Add expense:** Type chips (Cash, Transfer, Cheque, Other), amount, paid to, category chips, date, note and a Save button.
- **Summary:** Spending by category as bars, and spending by account.
- **Navigation:** Bottom bar with Home, History, Summary, Bills and Settings; Home keeps a prominent Add expense action.
- **Accessibility:** Touch targets of at least 44 px, text contrast of at least 4.5:1, and no information carried by color alone.
- **Implemented screens:** History, Needs review, Bills, Settings and import flows use the off-white and ink-navy visual direction. The first-run permission flow remains explicit and opt-in from Settings or Import SMS.
- **Visual polish remaining:** Compare the running screens to the mockup for the segmented Home category bar, SMS/manual source badges, Plus Jakarta Sans typography and bottom-bar Add treatment. The current Home page uses a prominent Add expense button.
