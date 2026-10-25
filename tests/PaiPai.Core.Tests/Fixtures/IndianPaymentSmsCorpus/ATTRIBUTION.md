# Test fixture attribution

- Corpus: SplitEasy Indian Payment SMS Corpus, version 0.2.0
- Source: https://github.com/skyaara/indian-payment-sms-dataset
- Pinned commit: `ef81c756ddd8c8f5ba09f1b00820700108f9c84c`
- License: CC BY 4.0; see `LICENSE` and `SOURCES.md` for attribution and provenance details.
- Retrieved: 2026-10-25

The corpus contains 58 seed fixtures and 114 synthetic numeric variants. Its message bodies are synthetic, redacted rewrites, not verified real-user SMS. The fixtures are for parser development and regression testing; they do not guarantee coverage of every bank's live message formats.

Initial Pai Pai parser audit (2026-10-25): 90 field or transaction-decision mismatches across 172 rows. The corpus test records this as a non-regression ceiling while parser coverage is improved; it does not claim full corpus compatibility.
