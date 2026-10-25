## Indian payment SMS corpus source ledger

This document credits the public open-source projects and package documentation used to design [`data/indian-payment-sms-dataset.json`](data/indian-payment-sms-dataset.json).

### What this dataset is

The repository is a parser corpus, not a survey of Indian spending. Version 0.2.0 contains 58 seed fixtures and 114 synthetic numeric variants, for 172 rows total. Most rows are synthetic variants grounded in genuine public OSS examples, test fixtures, and documented parser edge cases. Every SMS body is a synthetic, redacted rewrite—not a genuine private inbox message. Values, names, account/card suffixes, dates, references, sender headers, and URLs are invented or deliberately generic.

The corpus contains completed debits, credits, refunds, reversals, and hard negatives. Hard negatives matter because a balance, card bill, OTP, failed payment, refund notice, EMI reminder, or promotion can contain a rupee amount without representing a new expense.

Rows carry `source_ids` so a reviewer can trace the grammar or edge case that motivated each fixture. The rows do not copy private inbox data. The source projects remain separately licensed, and this file does not relicense their code.

Rows with `variant_of` are deterministic variants of a seed row. They preserve the message grammar while changing monetary values and account, card, reference, or other numeric identifiers. They are augmentation data, not additional real-world observations.

The accurate short description is: “Most examples are synthetic variants based on genuine public OSS examples; none should be presented as a verified real-user SMS.”

### Source ledger

| ID | Source | License | Revision or version | What informed the corpus |
| --- | --- | --- | --- | --- |
| `omoi_mit` | [abhirajsinha/omoi-sms-parser](https://github.com/abhirajsinha/omoi-sms-parser) | MIT | `7a865eeae0f23abc826bdb783fc7ccc48ca95915` | Indian bank/UPI debit fixtures and adversarial categories for OTPs, promotions, balances, declined payments, reversals, refunds, card bills, and upcoming debits. |
| `transaction_sms_parser_mit` | [MabudAlam/transaction_sms_parser](https://github.com/MabudAlam/transaction_sms_parser) | MIT | `a4f99c3279158d60674e20a36e7b7f3e82e657e0` | Account, card, wallet, UPI, merchant, balance, and reference-number field shapes. |
| `upi_sms_parser_mit` | [`upi_sms_parser` 0.1.0](https://pub.dev/packages/upi_sms_parser) | MIT | `0.1.0` | SBI, HDFC/ICICI/Axis, PhonePe/GPay/Paytm, Swiggy, sender-gate, date, reference, and non-transaction filter shapes. |
| `transaction_sms_parser_test_corpus_mit` | [saurabhgupta050890/transaction-sms-parser](https://github.com/saurabhgupta050890/transaction-sms-parser) | MIT | `9626d168970dc1e5175d8e4682e8cd2cb8e198a4` | Public workbook schema and account debit plus available-balance wording; represented by a rewritten seed fixture and its numeric variants. |
| `pennywise_agpl_pattern` | [sarim2000/pennywiseai-tracker](https://github.com/sarim2000/pennywiseai-tracker) | AGPL-3.0 | `6a44345c4f85a19e726e7c019e4436916350374b` | Bank-specific card, UPI, balance, mandate, refund, reversal, and card-bill edge cases. These rows are independent rewrites, not copied AGPL fixture text. |
| `auto_expense_gpl_pattern` | [wealth-wave/Auto-Expense-Tracker](https://github.com/wealth-wave/Auto-Expense-Tracker) | GPL-3.0 | `8d7ddfbbe102fa29815be81418e22a3cb1a836cf` | Deducted-amount, net-banking, debit-card, NEFT/IMPS, annual-fee, balance, and card-payment wording. These rows are independent rewrites, not copied GPL fixture text. |

### Sources reviewed but not copied

These sources were useful for discovery but were not treated as redistributable OSS data:

- [anuragjain-git/text-classification](https://github.com/anuragjain-git/text-classification): public examples, but the repository metadata did not declare a license.
- [AbelBoby/Bank-Sms-Reader](https://github.com/AbelBoby/Bank-Sms-Reader): public examples, but the repository metadata did not declare a license.
- [qtw4c7phzx-alt/Moneyprism](https://github.com/qtw4c7phzx-alt/Moneyprism): relevant India-first parser project, but the repository metadata did not declare a license.
- [Ritex12in/ExpenseTracker](https://github.com/Ritex12in/ExpenseTracker): relevant SMS expense-tracker project, but the repository metadata did not declare a license.
- [Jio SMS Rules JSON gist](https://gist.github.com/vs4vijay/925ebb800407535ad412982033771faa): useful historical regex rules, but no explicit reuse license was found.
- [Notitrack dataset paper](https://journals.mriindia.com/index.php/ijacte/article/download/2996/2870/6319): reports a manually collected dataset, but the paper did not provide a clearly linked downloadable CSV that could be verified and redistributed here.

Public availability is not the same as an OSS redistribution license. Attribution is good practice, but it does not cure a missing license.

This is not a claim that every SMS project or web page has been scraped. It includes every source admitted after the license and privacy review for this release; sources with unclear redistribution rights are listed for transparency but are not copied.

### Privacy and release rules

- Do not add a real SMS body until the contributor has permission to share it.
- Redact names, phone numbers, account numbers, card numbers, UPI IDs, reference numbers, support numbers, URLs, and order IDs. Keep only the formatting needed for the parser test.
- Use `source_ids` and this ledger for attribution.
- Keep the expected `bookable` result explicit. A parser must not turn a balance, limit, bill, OTP, failed payment, reversal, or future debit into a new expense.
- If a future contribution includes verbatim third-party fixture text, record its exact file, revision, license, and any required license notice before merging it.

### Dataset licensing

The synthetic rewrites authored for this repository may be reused under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), with attribution to SplitEasy contributors and this repository. This statement does not grant rights to any third-party source code or text. Follow the upstream license for anything copied directly from an upstream project.
