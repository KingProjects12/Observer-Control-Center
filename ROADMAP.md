# Development roadmap / Дорожная карта

## Shipped / Опубликовано

| Milestone | Scope | State |
|---|---|---|
| 2.0.17 | Access cleanup and TCP diagnostics / очистка и TCP | Integrated into 2.1.0 |
| 2.0.18 | Retry, session credentials, manual router address verification / повторный сбор, индивидуальные данные входа | Integrated into 2.1.0 |
| 2.0.19 | Saved CSV history and comparison / сравнение CSV | Integrated into 2.1.0 |
| 2.1.0 | Access Analyzer, colored collection status, exports | [Released](https://github.com/KingProjects12/Observer-Control-Center/releases/tag/v2.1.0) |

The intermediate versions were not published separately. / Промежуточные версии отдельно не выпускались.

## Next / Далее

- [ ] Live regression of 2.1.0 on authorized routers and previous reports.
- [ ] Resume retries across sessions; local site/address registry and manual confirmation of router identity.
- [ ] Improved service review, provenance and coverage warnings; no automatic allow/deny decisions.
- [ ] Safe UDP service-specific validation where authorized and meaningful.
- [ ] Sanitized screenshots and real application demo GIFs.
- [ ] Trusted code signing, additional Windows compatibility and accessibility checks.

These are proposals, not promised dates. / Это планы, а не обещанные сроки.

[Manual test checklist](docs/TEST_2.1.0_RU.md) · [Changelog](CHANGELOG.md)
