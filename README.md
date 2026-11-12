# PortMaster RG353V Addons

Дополнения для **Stardew Valley mainline + SMAPI** на **Anbernic RG353V / UnofficialOS**:

- **PortMaster Touch 0.1.1** — касание как левый клик, удержание и движение как перетаскивание. Стики остаются доступны. Это управление мышью, без мобильного интерфейса Android и жестов несколькими пальцами.
- **PortMaster Rumble 0.2.2** — вибрация по событиям игры: инструменты, поклёвка, урон и другие события. Поддерживает Generic Mod Config Menu.
- **PWM rumble-helper** — управление мотором через sysfs PWM и FIFO, с восстановлением idle при завершении.
- **DMC performance** — штатная максимальная частота памяти во время игры, с восстановлением прежнего режима при выходе. Убирает подтверждённые на тестовом устройстве фризы без движения стиков, особенно при рыбалке.

Проверено на RG353V, UnofficialOS `20260624`, kernel `4.19.172`, SMAPI `4.5.2`, Stardew Valley `1.6.15`. Пользователь подтвердил работу вибрации, тачскрина и устранение фризов. На других прошивках и устройствах совместимость не проверена. PWM-параметры относятся именно к RG353V; не используйте их вслепую на другом оборудовании.

## Установка

Нужен установленный [Stardew Valley mainline port](https://github.com/Producdevity/portmaster-stardew-valley-mainline) с SMAPI и вашей копией игры. Python 3 и Bash должны быть доступны на приставке.

Скачайте ZIP этого репозитория (Code → Download ZIP) или архив релиза и распакуйте на приставке. Выйдите из игры и запустите из распакованной папки:

```sh
python3 install.py /roms/ports/StardewValleySMAPI.sh --check
python3 install.py /roms/ports/StardewValleySMAPI.sh
```

Укажите реальное имя launcher: в новых сборках это может быть `StardewValleyMainline.sh`. Рядом должна находиться папка `stardewvalleymainline`.

Установщик добавляет обе папки модов, helper и интеграцию launcher. Существующие `config.json` и сохранения не заменяются. Резервные копии заменяемых файлов находятся в `stardewvalleymainline/addon-backups/`. Повторная установка не дублирует интеграцию; поддерживается перенос с прежних rumble/DMC-вставок из этой разработки. При неизвестной структуре launcher или сторонних signal traps установщик остановится до изменений.

После установки полностью перезапустите игру. После обновления порта, заменившего launcher, повторите установку.

## Проверка

В `stardewvalleymainline/log.txt` должны появиться:

```text
RG353V addons: DMC performance enabled (previous: dmc_ondemand).
rumble-helper: button rumble disabled; FIFO-only mode
[PortMaster Touch] Touch enabled: /dev/input/event1, X=0..639, Y=0..479.
```

Номер `event` может отличаться. Вибрация идёт по событиям игры, а не от каждого нажатия кнопки. Проверьте касания меню, перетаскивание и переключение обратно на стики. Для настройки вибрации используйте Generic Mod Config Menu или `Mods/PortMasterRumble/config.json`.

## Откат

Выйдите из игры. Восстановите launcher из нужного `addon-backups/<дата>/` и прежние файлы модов, если они были заменены. Для полного отключения дополнений переместите папки `Mods/PortMasterTouch` и `Mods/PortMasterRumble` за пределы `Mods`. Папку `tools/rg353v-addons` можно удалить после восстановления launcher. Сохранения не затрагиваются.

Восстановление частоты памяти и PWM рассчитано на обычное завершение, SIGINT и SIGTERM. SIGKILL не позволяет выполнить обработчики выхода; перезагрузка восстанавливает настройки прошивки.

## Исходники и сборка

Готовые DLL включены в `mods/`; игровые DLL, SDK и сохранения не распространяются.
Их контрольные суммы в `SHA256SUMS` совпадают с рабочими DLL, проверенными на приставке.

При наличии .NET 6 SDK и установленной игры:

```sh
dotnet build src/PortMasterTouch/PortMasterTouch.csproj -c Release -p:GamePath=/path/to/stardewvalleymainline/gamedata
dotnet build src/PortMasterRumble/PortMasterRumble.csproj -c Release -p:GamePath=/path/to/stardewvalleymainline/gamedata
python3 -m unittest discover -s tests -v
```

## Происхождение

Объединяет [portmaster-rg353v-rumble-module](https://github.com/garik816/portmaster-rg353v-rumble-module) и [portmaster-rumble](https://github.com/garik816/portmaster-rumble), добавляет PortMaster Touch и интеграцию DMC. Старые репозитории остаются доступны; актуальные дополнения собраны здесь. Исходные авторство и идентификатор rumble-мода сохранены для совместимости с конфигами и GMCM.

Сам порт Stardew Valley остаётся отдельным upstream-проектом; этот репозиторий содержит только дополнения.
