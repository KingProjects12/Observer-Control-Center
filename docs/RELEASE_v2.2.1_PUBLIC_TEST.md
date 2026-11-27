# Observer 2.2.1 — Full-width dashboard
- Home contains five colored modules, session results and device/network reports. No sidebar or command console on Home.
- Each module opens inside the shared workspace, can return to Home without discarding its state, and displays the larger color console. Theme settings move to the top menu.
- NAT and traffic report views are retained independently; the current NAT CSV is passed automatically to the shared access/TCP analyzer. Reopening the same input does not reset analysis. Reports are saved by the existing collector; active in-memory work is retained while the application remains open.
- Both themes share the layout. Network core and formats unchanged; no invented measurements.
# Observer 2.2.1 — Главная и рабочие разделы
- Главная: пять цветных карточек, сводка, топ устройств и результаты сети. Без бокового меню и консоли.
- Внутри разделов — рабочие панели и увеличенный цветной журнал. Сворачивание не сбрасывает состояние. Настройки и темы в верхнем меню.
- NAT и трафик сохраняют отдельные отчёты. CSV NAT автоматически поступает в анализатор; TCP использует тот же результат. Отчёты остаются на диске, незавершённые операции сохраняются при переключении в рамках открытой программы, не после перезапуска.
- Сетевое ядро и форматы не изменены.
