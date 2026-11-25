<!-- observer:en -->
# Observer 2.0.12 — collector integration (public test)

- Original v1.9.1 collector core integrated into the new WPF interface: NAT snapshots, clients, source/destination routes, central flow CSV import, VPN REMOTE diagnostics, TOP Traffic (four windows), raw evidence and CSV/XLSX reports.
- Select the office destination /24 locally. The setting is remembered on this PC; router credentials are not persisted.
- Use Connect / NAT, VPN REMOTE or Collect traffic. Secondary controls are in Tools.
- Preserves Black Liquid Glass, English/Russian installer choice, logs, icon, mini island and EXE self-update.
- Public test release: offline tests are not a substitute for real Keenetic and Windows UI testing. Original collector diagnostic/report strings can still be Russian. VPN REMOTE uses an existing authorized route, not a tunnel creator. NAT and Traffic Monitor may omit LAN/VPN flows; no matches do not prove absence. x-ndw4 is not supported. No router configuration writes; HTTPS recommended. Binaries are unsigned.

<!-- observer:ru -->
# Observer 2.0.12 — интеграция сборщика (публичный тест)

- Ядро v1.9.1 интегрировано в новый WPF: NAT, клиенты, направления, импорт CSV центрального шлюза, VPN REMOTE, TOP Traffic за четыре периода, сырые данные и CSV/XLSX.
- Укажите целевую сеть /24 локально. Она запоминается на ПК, пароль не сохраняется.
- Основные кнопки: «Подключение / NAT», VPN REMOTE, «Собрать трафик». Остальное в меню «Инструменты».
- Сохранены Black Liquid Glass, английский и русский интерфейс, иконка, журнал, островок и обновление EXE.
- Публичная тестовая версия: нужен реальный тест Keenetic и интерфейса Windows. Часть старых диагностических сообщений и отчётов пока на русском. VPN REMOTE использует существующий разрешённый маршрут, а не создаёт VPN. NAT/Traffic Monitor могут не видеть все VPN/LAN-потоки. x-ndw4 не поддерживается. Настройки роутера не изменяются. Рекомендуется HTTPS. Цифровой подписи пока нет.
