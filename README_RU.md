<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/media/signal-hero-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/media/signal-hero-light.svg">
  <img src="docs/media/signal-hero-dark.svg" alt="Observer Control Center — Turn connections into evidence" width="100%">
</picture>

[![Latest release](https://img.shields.io/github/v/release/KingProjects12/Observer-Control-Center?style=flat-square&label=release&color=6955bd)](https://github.com/KingProjects12/Observer-Control-Center/releases/latest)
[![Windows build](https://img.shields.io/github/actions/workflow/status/KingProjects12/Observer-Control-Center/windows-build.yml?branch=main&style=flat-square&label=Windows%20build)](https://github.com/KingProjects12/Observer-Control-Center/actions/workflows/windows-build.yml)
![Windows x64](https://img.shields.io/badge/Windows-10%20%2F%2011%20x64-27364b?style=flat-square)
![.NET 10](https://img.shields.io/badge/.NET-10-6955bd?style=flat-square)
[![Downloads](https://img.shields.io/github/downloads/KingProjects12/Observer-Control-Center/total?style=flat-square&color=146b9a)](https://github.com/KingProjects12/Observer-Control-Center/releases)

**Поймите сетевые подключения прежде, чем менять правила доступа.**

[![Скачать Observer](https://img.shields.io/badge/OBSERVER-DOWNLOAD-146b9a?style=for-the-badge&logo=github&logoColor=white)](https://github.com/KingProjects12/Observer-Control-Center/releases/latest)

[Portable EXE](https://github.com/KingProjects12/Observer-Control-Center/releases/latest/download/Observer.ControlCenter.exe) · [Документация](docs/WORKSPACE_GUIDE_RU.md) · [Все релизы](https://github.com/KingProjects12/Observer-Control-Center/releases)

[English](README.md) · **Русский**

[Возможности](#01--возможности) · [Как это работает](#02--как-это-работает) · [Быстрый старт](#03--быстрый-старт) · [Версии](#04--версии)

</div>

---

## Видимость сети без изменения конфигурации

Observer Control Center — приложение Windows для уполномоченных сетевых администраторов. Оно собирает данные с управляемых Keenetic в режиме чтения, обрабатывает существующие наблюдения и помогает подготовить доказательства для согласования доступов. Отчёты остаются локально, конфигурация сети не меняется.

| Сбор только на чтение | Локальные отчёты | Данные, а не догадки |
| :--- | :--- | :--- |
| Без изменения настроек роутера и firewall. | Исходные снимки и экспорт CSV/XLSX. | Наблюдение, опциональная проверка TCP, ручное согласование. |

## 01 / Возможности

### 01 · Network Discovery

Снимки NAT Keenetic в режиме чтения — до 100 явно заданных IPv4-адресов.

### 02 · VPN Remote

Сбор через существующий разрешённый VPN-маршрут, прогресс по роутерам, цветной журнал и повторный сбор.

### 03 · Access Analyzer

Нормализация и устранение дублей; группировка по IP, порту, протоколу и связям источника с назначением.

### 04 · TCP Diagnostics

Дополнительная проверка наблюдаемых частных TCP-назначений: открыто, отказ или тайм-аут.

### 05 · Connection History

Сравнение двух сохранённых CSV-периодов: изменения назначений и источников.

### 06 · TOP Traffic

Четыре периода мониторинга, рейтинг устройств и локальные отчёты CSV/XLSX.

Также доступны: индивидуальные данные входа на сессию для повторного сбора, накопительные отчёты, импорт CSV центрального шлюза, русский/английский интерфейс WPF, Black Liquid Glass (прозрачность WPF, не Acrylic), мини-островок, бирюзовый/лавандовый акценты и самообновление EXE с проверкой, резервной копией и перезапуском.

Наблюдения UDP сохраняются без проверки сервиса. Описание относится к реализованным функциям, а не подтверждённой работе на каждом роутере.

## 02 / Как это работает

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/media/signal-flow-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/media/signal-flow-light.svg">
  <img src="docs/media/signal-flow-dark.svg" alt="Collect → Normalize → Verify → Review" width="100%">
</picture>

| Этап | Что происходит |
| :--- | :--- |
| **01 / Collect** | Сбор разрешённых наблюдений NAT или импорт существующих CSV центрального шлюза. |
| **02 / Normalize** | Проверка IPv4, направления и портов. Исключение повторного импорта без изменения оригиналов. |
| **03 / Verify** | Опциональная проверка наблюдаемых частных TCP-назначений без прикладных данных. |
| **04 / Review** | Экспорт доказательств, сравнение периодов и проверка назначения сервисов перед согласованием правил. |

<details>
<summary><strong>Файлы отчётов и локальное хранение</strong></summary>

| Файл | Назначение |
| :--- | :--- |
| `Office_Connections_All_Routers.xlsx` | Общий NAT-отчёт по роутерам |
| `07_Office_To_Connections.csv` | Исходные наблюдения в офис |
| `00_Router_Status.csv` | Охват и ошибки сбора |
| `Office_Access_Analysis.xlsx` | Полный анализ доступов |
| `Office_IP_Port.csv` | Уникальные назначения с протоколом |
| `Office_Source_Rules.csv` | Связи источников с офисом |
| `Import_Audit.csv` | Аудит импорта и очистки |
| `Office_Changes.csv / Office_Source_Changes.csv` | Изменения между периодами |

Локальная папка: `%LOCALAPPDATA%\KeeneticObserver\Results\`.

Повторный сбор относится к текущему окну сборщика; история основана на сохранённых CSV.

</details>

## 03 / Быстрый старт

| Дистрибутив | Для кого |
| :--- | :--- |
| [Установщик Windows](https://github.com/KingProjects12/Observer-Control-Center/releases/latest) | Повседневное использование, ярлык и выбор языка интерфейса. |
| [Portable EXE](https://github.com/KingProjects12/Observer-Control-Center/releases/latest/download/Observer.ControlCenter.exe) | Запуск без установки. |

Обе сборки самодостаточны для Windows 10/11 x64.

1. Откройте «Подключение / NAT» или **VPN REMOTE**, если разрешённый VPN уже подключён.
2. Введите IPv4 управляемых роутеров по одному на строку, данные входа и сеть офиса IPv4 /24. Предпочтителен HTTPS; HTTP — только в доверенной разрешённой сети.
3. Соберите NAT и проверьте статус каждого роутера. При необходимости повторите сбор.
4. Откройте «АНАЛИЗ ДОСТУПОВ», выберите исходные CSV, проанализируйте и экспортируйте. Проверка TCP и сравнение периодов — по необходимости.

**[Открыть инструкцию →](docs/WORKSPACE_GUIDE_RU.md)**

> [!IMPORTANT]
> У сборок пока нет доверенной Authenticode-подписи. Не отключайте защиту Windows. SHA-256 подтверждает целостность файла, а не личность издателя.

## 04 / Версии

### [2.1.0 — Access Analyzer](https://github.com/KingProjects12/Observer-Control-Center/releases/tag/v2.1.0)

Единый процесс анализа доступов объединяет сбор, очистку, диагностику TCP, повторный сбор и сравнение периодов. Этапы разработки 2.0.17–2.0.19 вошли в 2.1.0 и отдельно не публиковались.

| Версия | Основное изменение |
| :--- | :--- |
| 2.1.0 | Access Analyzer, диагностика и история подключений |
| 2.0.16 | Массовый сбор NAT и общие отчёты |
| 2.0.15 | Рабочая область, интерфейс и установщик |
| 2.0.12 | Интеграция оригинального сборщика в WPF |

В документации релиза указано, что Windows-сборки и офлайн-тесты прошли; 2.1.0 всё ещё требует проверки на разрешённых реальных сетях.

[Полная история версий](CHANGELOG.md) · [Дорожная карта](ROADMAP.md) · [Чек-лист тестирования](docs/TEST_2.1.0_RU.md)

<details>
<summary><strong>Сборка из исходников</strong></summary>

Windows 10/11 x64 и .NET 10 SDK или Visual Studio с разработкой классических приложений .NET.

```powershell
git clone https://github.com/KingProjects12/Observer-Control-Center.git
cd Observer-Control-Center
dotnet run --project Tests/Observer.UpdateTests.csproj -c Release
dotnet run --project LegacyCollectorTests/LegacyCollectorTests.csproj -c Release
dotnet build Observer.ControlCenter.csproj -c Release
.\Publish-SingleExe.cmd
```

Результат: `dist\Observer.ControlCenter.exe`. Артефакт GitHub Actions не является релизом. Для самообновления используйте «Инструменты → Обновления» и сохраните отчёты перед перезапуском; язык установщика сохраняется.

</details>

## Ответственное использование

Observer предназначен только для сетей, которыми вы владеете или управляете с разрешения. VPN REMOTE не создаёт туннель. Снимки NAT могут не показать часть LAN/VPN-потоков; отсутствие записи не повод блокировать доступ. Открытый TCP-порт не доказывает назначение сервиса. UDP наблюдается, но не проверяется. x-ndw4 не поддерживается. Пароли не сохраняются на диске; не публикуйте внутренние адреса, дампы и отчёты клиентов.

Интерфейс разработан самостоятельно с визуальным вдохновением от [Atoll](https://github.com/Fuwni/dynamic-island-windows). При использовании кода Atoll сохраняйте применимые уведомления MIT. Лицензия Observer пока не выбрана; публичность исходников сама по себе не предоставляет права распространения.

---

<div align="center">

**OBSERVER / CONTROL CENTER**

Ясность прежде конфигурации.

[Скачать](https://github.com/KingProjects12/Observer-Control-Center/releases/latest) · [Сообщить о проблеме](https://github.com/KingProjects12/Observer-Control-Center/issues) · [Документация](docs/WORKSPACE_GUIDE_RU.md)

</div>
