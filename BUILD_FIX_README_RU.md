# Observer Control Center v2.0.3 — BUILD FIX

Исправлены ошибки, обнаруженные в реальном журнале сборки v2.0.2 на Windows 10 / Visual Studio 2026.

1. Добавлен `GlobalUsings.cs` с `global using System.IO;`: устраняет 35 ошибок CS0103/CS0246 с `Path`, `File`, `Directory`, `FileInfo`, `IOException` и `InvalidDataException` в WPF-проекте.
2. Исправлен `ReleaseVersion.TryParse`: стабильная версия `v2.0.1` больше не отклоняется как ошибочная из-за пустой строки prerelease.
3. Добавлены тесты стабильных версий и build metadata.
4. Обновлена версия сборки до `2.0.3-preview.1`.
5. `Publish-SingleExe.ps1` теперь автоматически запускает тесты, сборку и публикацию, останавливаясь при ошибке.

## Выполнение

Выполнить в Проводнике: распаковать архив; запустить `Publish-SingleExe.cmd`.
Или в обычном PowerShell из папки с проектом:

```powershell
dotnet --list-sdks
dotnet run --project .\Tests\Observer.UpdateTests.csproj -c Release
dotnet build .\Observer.ControlCenter.csproj -c Release
.\Publish-SingleExe.cmd
```

Готовый файл при успешной публикации: `dist\Observer.ControlCenter.exe`.

**Тесты/Windows WPF:** в текущей среде отсутствует Windows SDK, поэтому реальная компиляция и запуск на Windows не подтверждены. Исправления адресуют ошибки из предоставленного лога; возможны другие ошибки после повторного запуска.

**Функции:** WPF-прототип с просмотром локальных отчётов и уведомлениями GitHub Releases. Прямой сбор NAT и TOP TRAFFIC остаётся в Observer v1.9.1.

**Безопасность:** архив не содержит пароль, данные торговых точек и файлы сетевых выгрузок. Перед публичным GitHub-релизом нужна дополнительная проверка.
