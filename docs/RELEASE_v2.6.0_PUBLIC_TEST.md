# Observer 2.6.0

Релиз для Windows x64. В пяти рабочих модулях сохранены цветовая навигация и общая сессия. Добавлена кнопка возврата к выбору модулей.

- В таблице роутеров при обновлении статуса сохраняются сортировка и выбранный роутер. Причины незавершённого сбора сгруппированы в разделе «Проблемы».
- Заголовки таблиц анализа и результаты TCP переведены на язык интерфейса. Ошибка повторного анализа сохраняет предыдущий успешный результат.
- В журнале время, категория и сообщение занимают отдельные колонки. События текущего сеанса можно скопировать полностью.
- Островок показывает выполняемую операцию, прогресс NAT и результат. Все 26 тем доступны в настройках.
- Обновлятор проверяет имя EXE до загрузки, учитывает активный сбор или анализ перед перезапуском и проверяет SHA-256. Автоматическая проверка выполняется при запуске и затем каждые шесть часов.

Переносная версия для встроенного обновления должна называться `Observer.ControlCenter.exe`. Для установки выберите Windows x64 Setup.

## English

This Windows x64 release improves module navigation, router table selection, analysis results, TCP status labels, the session log and the operation island. The updater checks the executable name before downloading, protects active operations and resumes six-hour automatic polling. Keep the original portable executable name to use in-app updates.
