<!-- observer:en -->
# Observer 2.1.0 — Access Analyzer
- Access analyzer: import original NAT CSV reports, normalize original destinations, separate reverse/outside/internal and malformed rows, deduplicate repeat imports, group endpoint + protocol and source-to-office relationships, optional private observed TCP checks (no payload), export CSV/XLSX.
- Compare earlier and later periods by endpoint and source; non-observation does not prove a connection is unnecessary. Original reports remain unchanged.
- Multi-router NAT: per-router progress, colored live log, accurate timeout/authorization/cancellation status, session-only credentials for individual routers, retry incomplete routers in the current collector and cumulatively retain earlier observations.
- Search the connections grid, colored main and update status. Existing NAT/TOP Traffic and local reports retained. No firewall changes, no password storage, no automatic service/allowlist decisions.
- Live checks depend on authorized network routes. UDP remains observed, not validated. Read the manual test guide.
<!-- observer:ru -->
# Observer 2.1.0 — анализ сетевых доступов
- «Анализ доступов»: импорт исходных CSV NAT, фильтрация по исходному назначению, отделение обратных/вне сети/внутренних и некорректных записей, исключение повторного импорта, группировка IP:порт:протокол и связей источник → офис, отдельная проверка наблюдаемых частных TCP-назначений без прикладных данных, экспорт CSV/XLSX.
- Сравнение раннего и позднего периода по назначениям и источникам; отсутствие наблюдения не означает ненужность доступа. Исходники не изменяются.
- NAT: прогресс по роутерам, цветной журнал, отдельные тайм-ауты, авторизация и отмена, индивидуальные учётные данные только на сессию, повтор проблемных в текущем окне с сохранением ранее полученных наблюдений.
- Поиск в соединениях, цветные статусы основного окна и обновлений. NAT/TOP Traffic сохранены. Никаких изменений правил, хранения паролей или автоматического разрешения/запрета.
- UDP остаётся наблюдаемым, но не проверенным. Реальные подключения требуют разрешённого маршрута. См. инструкцию тестирования.
