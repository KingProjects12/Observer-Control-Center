<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/media/signal-hero-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/media/signal-hero-light.svg">
  <img src="docs/media/signal-hero-dark.svg" alt="Observer Control Center — Turn connections into evidence" width="100%">
</picture>

[![Release](https://img.shields.io/github/v/release/KingProjects12/Observer-Control-Center?style=flat-square&label=release&color=6955bd)](https://github.com/KingProjects12/Observer-Control-Center/releases/latest)
[![Build](https://img.shields.io/github/actions/workflow/status/KingProjects12/Observer-Control-Center/windows-build.yml?branch=main&style=flat-square&label=Windows%20build)](https://github.com/KingProjects12/Observer-Control-Center/actions/workflows/windows-build.yml)
![Windows x64](https://img.shields.io/badge/Windows-10%20%2F%2011%20x64-27364b?style=flat-square)
![.NET 10](https://img.shields.io/badge/.NET-10-6955bd?style=flat-square)
[![Downloads](https://img.shields.io/github/downloads/KingProjects12/Observer-Control-Center/total?style=flat-square&color=146b9a)](https://github.com/KingProjects12/Observer-Control-Center/releases)

[![Download](https://img.shields.io/badge/OBSERVER-DOWNLOAD-146b9a?style=for-the-badge&logo=github&logoColor=white)](https://github.com/KingProjects12/Observer-Control-Center/releases/latest)

[Portable EXE](https://github.com/KingProjects12/Observer-Control-Center/releases/latest/download/Observer.ControlCenter.exe) · [Документация](docs/WORKSPACE_GUIDE_RU.md) · [English](README.md) · **Русский**

</div>

<a id="как-работать-в-observer-232"></a>

![Как работать в Observer 2.3.2. Выберите раздел на стартовом экране. Пять кнопок выстроятся в ряд над рабочей панелью. Повторный щелчок по активному разделу сворачивает или раскрывает его панель. - Роутеры собирает снимки NAT с указанных вами роутеров Keenetic. - Соединения анализирует сохранённые CSV с соединениями. Здесь можно сгруппировать назначения, сравнить два периода и экспортировать отчёт в CSV или Excel. - TCP формирует список назначений из наблюдений и проверяет их доступность по TCP с компьютера, на котором запущен Observer. - Трафик собирает статистику трафика по устройствам. - Проблемы показывает роутеры, на которых сбор не завершился. Кнопка повтора открывает «Роутеры» и повторяет сбор для этих устройств. Если у вас уже есть отчёт, откройте «Соединения», выберите CSV-файлы и нажмите «Анализировать CSV». Запускать новый сбор для этого не требуется. Чтобы установить программу на Windows x64, выберите установщик на странице выпуска 2.3.2. Для переносного запуска скачайте Observer.ControlCenter.exe. Рядом с каждым файлом опубликована его контрольная сумма SHA-256.](docs/media/cards/ru/workspace-232.svg)

[Файлы выпуска 2.3.2](https://github.com/KingProjects12/Observer-Control-Center/releases/tag/v2.3.2)

![Что такое Observer? — Видимость сети: Приложение Windows для уполномоченных сетевых администраторов. Сбор NAT с управляемых Keenetic в режиме чтения и обработка существующих данных.; Только чтение: Без изменения настроек роутеров и firewall. Отчёты и исходные снимки остаются локально.; Данные, а не догадки: Анализ доступов по CSV/XLSX, опциональная TCP-проверка и ручное согласование.](docs/media/cards/ru/overview.svg)

![Сбор и анализ — Network Discovery: Снимки NAT Keenetic в режиме чтения — до 100 явно заданных IPv4-адресов.; VPN Remote: Существующий разрешённый VPN-маршрут, прогресс по роутерам, цветной журнал и повторный сбор. Индивидуальные данные входа на сессию, накопительные отчёты.; Access Analyzer: Нормализация и удаление дублей. Группировка по IP, порту, протоколу и связям источника с назначением.](docs/media/cards/ru/capabilities-1.svg)

![Диагностика и история — TCP Diagnostics: Проверка наблюдаемых частных TCP-назначений: открыто, отказ или тайм-аут. Наблюдения UDP сохраняются без проверки сервиса.; Connection History / TOP Traffic: Сравнение двух CSV-периодов, изменения назначений и источников. Четыре периода мониторинга, рейтинг устройств и локальные CSV/XLSX.; Рабочая среда: Импорт CSV центрального шлюза; двуязычный WPF Black Liquid Glass (прозрачность, не Acrylic), мини-островок, бирюзовый/лавандовый акцент. Самообновление EXE с проверкой, копией и перезапуском.](docs/media/cards/ru/capabilities-2.svg)

![От подключения к доказательствам — 01 / Сбор: Сбор разрешённых наблюдений NAT или импорт CSV центрального шлюза.; 02 / Очистка: Проверка IPv4, направления и портов; исключение повторного импорта без изменения оригиналов.; 03 / Проверка: Опциональная проверка наблюдаемых частных TCP-назначений без прикладных данных.; 04 / Анализ: Экспорт доказательств, сравнение периодов и проверка назначения сервисов перед согласованием правил.](docs/media/cards/ru/workflow.svg)

![Локальные отчёты — Office_Connections_All_Routers.xlsx: Общий NAT-отчёт по роутерам.; 07_Office_To_Connections.csv: Исходные наблюдения в офис.; 00_Router_Status.csv: Охват и ошибки сбора.; Office_Access_Analysis.xlsx: Полный анализ доступов.; Office_IP_Port.csv: Уникальные назначения с протоколом.; Office_Source_Rules.csv: Связи источников с офисом.; Import_Audit.csv: Аудит импорта и очистки.; Office_Changes.csv / Office_Source_Changes.csv: Сравнение периодов. Повторный сбор в текущем окне сборщика; история по сохранённым CSV.; %LOCALAPPDATA%\\KeeneticObserver\\Results\\: Локальная папка.](docs/media/cards/ru/reports.svg)

![Быстрый старт — Установка или запуск: Windows 10/11 x64; самодостаточный установщик с ярлыком и выбором языка или portable EXE без установки.; 01 / Подключение: Откройте «Подключение / NAT» или VPN REMOTE при уже подключённом разрешённом VPN.; 02 / Входные данные: Введите IPv4 роутеров по одному на строку, данные входа и сеть офиса IPv4 /24. Предпочтителен HTTPS; HTTP только в доверенной разрешённой сети.; 03 / Сбор NAT: Проверьте статус каждого роутера; при необходимости повторите сбор.; 04 / Анализ и экспорт: Выберите исходные CSV в «АНАЛИЗ ДОСТУПОВ». Анализ, экспорт, опционально TCP-проверка и сравнение периодов.](docs/media/cards/ru/quickstart.svg)

![История версий — 2.1.0 / Access Analyzer: Сбор, очистка, TCP-диагностика, повторный сбор и сравнение периодов. Этапы 2.0.17–2.0.19 вошли в релиз и отдельно не публиковались.; 2.0.16: Массовый сбор NAT и общие отчёты.; 2.0.15: Рабочая область, интерфейс и установщик.; 2.0.12: Интеграция оригинального сборщика в WPF.; Статус проверки: В документации указаны успешные Windows-сборки и офлайн-тесты. Нужна проверка 2.1.0 на разрешённых реальных сетях. Реализация не означает совместимость со всеми роутерами.](docs/media/cards/ru/release.svg)

![Безопасность и ограничения — Только разрешённые сети: Только сети, которыми вы владеете или управляете с разрешения. VPN REMOTE не создаёт туннель. x-ndw4 не поддерживается.; Интерпретация результатов: NAT может не показать часть LAN/VPN-потоков; отсутствие записи не повод блокировать доступ. Открытый TCP-порт не доказывает назначение сервиса. UDP только наблюдается.; Конфиденциальность: Пароли не сохраняются на диске. Не публикуйте внутренние адреса, дампы и отчёты клиентов.; Защита Windows и обновления: Доверенной Authenticode-подписи пока нет. Не отключайте защиту. SHA-256 подтверждает целостность, не издателя. Перед «Инструменты → Обновления» сохраните отчёты; язык установщика сохраняется. Артефакты Actions не являются релизами.; Лицензия и авторство: Интерфейс разработан самостоятельно с визуальным вдохновением от Atoll. При использовании его кода сохраняйте применимые уведомления MIT. Лицензия Observer не выбрана; публичность исходников не даёт права распространения.; Требования для сборки: Windows 10/11 x64; .NET 10 SDK или Visual Studio с разработкой классических приложений .NET.](docs/media/cards/ru/limitations.svg)

## Команды

```powershell
git clone https://github.com/KingProjects12/Observer-Control-Center.git
cd Observer-Control-Center
dotnet run --project Tests/Observer.UpdateTests.csproj -c Release
dotnet run --project LegacyCollectorTests/LegacyCollectorTests.csproj -c Release
dotnet build Observer.ControlCenter.csproj -c Release
.\Publish-SingleExe.cmd
```

```text
Output: dist\Observer.ControlCenter.exe
```

---

<div align="center">

[Скачать](https://github.com/KingProjects12/Observer-Control-Center/releases/latest) · [Документация](docs/WORKSPACE_GUIDE_RU.md) · [Changelog](CHANGELOG.md) · [Roadmap](ROADMAP.md) · [Тестирование](docs/TEST_2.1.0_RU.md) · [Issues](https://github.com/KingProjects12/Observer-Control-Center/issues) · [Atoll](https://github.com/Fuwni/dynamic-island-windows)

</div>
