# Aula Monitor

**English** · [Русский](#русская-версия)

A small Windows tray app that shows the battery level of **AULA wireless keyboards**.
The official AULA software does not show it for many models.

- Battery icon in the tray, filled by charge level: green above 70%, yellow 30–70%, red below 30%, bolt while charging
- Click the icon for a status card: model, charge, connection (USB / 2.4G / Bluetooth), awake / asleep / offline
- Low-battery warning (20% and below): a small window in the screen corner with a single beep. It does not take focus from the window you are typing in, closes with a click anywhere on it. It is a regular window, not a Windows notification, so Do Not Disturb / Focus Assist does not hide it
- The keyboard model is detected automatically
- Adjustable polling interval, optional start with Windows

## Install

1. Install the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (Windows x64).
2. Download `AulaMonitor.exe` from [Releases](../../releases) and put it anywhere.
3. Run `AulaMonitor.exe`. The icon appears in the tray (it may be in the hidden icons).

The interface is in Russian. Right-click the icon for the menu: **Статус клавиатуры** (status card), **Обновить статус** (refresh), **Частота опроса** (polling interval), **Запускать с Windows** (start with Windows, off by default), **Выход** (exit).

## Supported keyboards

Tested on **AULA AU75** with the 2.4G dongle. The built-in list also has AULA F75, F87, F99 and F68. They use the same 2.4G dongle chipset (Compx, USB VID `0x3554`) and should work the same way, but have not all been tested. Any other AULA keyboard with a `0x3554` dongle is still polled and shown as "AULA Wireless Keyboard". Wired USB (VID `0x258A`) and Bluetooth LE work on a best-effort basis.

### Add your model

Find the dongle's IDs: Device Manager → *Human Interface Devices* → the dongle's HID entry → *Details* → *Hardware Ids* (for example `HID\VID_3554&PID_FA09&Col01`). Create `keyboards.json` next to `AulaMonitor.exe` and restart the app:

```json
[
  {
    "name": "AULA F87 Pro",
    "dongleVid": "0x3554",
    "donglePids": ["0xFA20"],
    "usbVid": "0x258A",
    "usbPids": ["0x0120"],
    "bluetoothNameHints": ["F87 Pro"]
  }
]
```

## Troubleshooting

- **The keyboard no longer goes to sleep while the app runs.** Every poll resets the keyboard's own sleep timer. Choose a polling interval longer than the keyboard's sleep timeout (tray menu → *Частота опроса*, default 45 s).
- **"нет связи" with the dongle plugged in.** The keyboard may be off, or the dongle has different IDs. Check them as described in *Add your model*.
- **The app crashed.** Details are written to `crash.log` next to the exe.

## How it works

The HID commands used to read the battery (an `0x13 0x4A` request through the 2.4G dongle, a feature report over USB) are not documented by AULA; they were reverse-engineered. Layout: `Hid/` talks to the device (one channel class per connection type), `ReboundSmoother` filters false upward jumps in the reading, `Services/` holds polling, settings and notifications, `UI/` draws the tray icon and cards.

## Credits

The battery-reading protocol was originally reverse-engineered by [brhnshn/AulaTray](https://github.com/brhnshn/AulaTray). The code in this repository is a from-scratch rewrite.

## Disclaimer

Unofficial; not affiliated with or endorsed by AULA. Provided as is.

## License

MIT

---

## Русская версия

[English](#aula-monitor)

Небольшая программа для трея Windows, которая показывает заряд беспроводных клавиатур **AULA**.
Официальный софт Aula для многих моделей заряд не показывает.

- Иконка-батарейка в трее, заливка по заряду: зелёный выше 70%, жёлтый 30–70%, красный ниже 30%, молния при зарядке
- Клик по иконке открывает карточку статуса: модель, заряд, подключение (USB / 2.4G / Bluetooth), активна / спит / отключена
- Предупреждение о низком заряде (20% и ниже): небольшое окно в углу экрана с одним звуковым сигналом. Не забирает фокус у окна, в котором вы печатаете, закрывается кликом в любом месте. Это обычное окно, а не уведомление Windows, поэтому режим «Не беспокоить» / Фокусировка внимания его не скрывает
- Модель клавиатуры определяется автоматически
- Настраиваемая частота опроса, автозапуск с Windows по желанию

### Установка

1. Установите [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (Windows x64).
2. Скачайте `AulaMonitor.exe` со страницы [Releases](../../releases) и положите куда угодно.
3. Запустите `AulaMonitor.exe`. Иконка появится в трее (возможно, среди скрытых значков).

Меню по правому клику на иконке: **Статус клавиатуры**, **Обновить статус**, **Частота опроса**, **Запускать с Windows** (по умолчанию выключено), **Выход**.

### Поддерживаемые клавиатуры

Проверено на **AULA AU75** через 2.4G-донгл. Во встроенном списке также AULA F75, F87, F99 и F68. Они используют тот же чипсет 2.4G-донгла (Compx, USB VID `0x3554`) и должны работать так же, но проверены не все. Любая другая клавиатура AULA с донглом `0x3554` всё равно опрашивается и отображается как «AULA Wireless Keyboard». Проводной USB (VID `0x258A`) и Bluetooth LE работают по принципу «как получится».

#### Добавить свою модель

Найдите ID донгла: Диспетчер устройств → *Устройства HID* → HID-устройство донгла → *Сведения* → *ИД оборудования* (например, `HID\VID_3554&PID_FA09&Col01`). Создайте `keyboards.json` рядом с `AulaMonitor.exe` и перезапустите программу:

```json
[
  {
    "name": "AULA F87 Pro",
    "dongleVid": "0x3554",
    "donglePids": ["0xFA20"],
    "usbVid": "0x258A",
    "usbPids": ["0x0120"],
    "bluetoothNameHints": ["F87 Pro"]
  }
]
```

### Если что-то не так

- **Клавиатура перестала засыпать при запущенной программе.** Каждый опрос сбрасывает собственный таймер сна клавиатуры. Выберите интервал опроса больше таймаута сна клавиатуры (меню трея → *Частота опроса*, по умолчанию 45 сек).
- **«нет связи» при вставленном донгле.** Клавиатура может быть выключена, либо у донгла другие ID. Проверьте их, как описано в разделе «Добавить свою модель».
- **Программа упала.** Подробности записываются в `crash.log` рядом с exe.

### Как устроено

HID-команды чтения заряда (запрос `0x13 0x4A` через 2.4G-донгл, feature-репорт по USB) AULA не документирует, они получены реверс-инжинирингом. Структура: `Hid/` — работа с устройством (по классу на тип подключения), `ReboundSmoother` — фильтр ложных скачков заряда вверх, `Services/` — опрос, настройки и уведомления, `UI/` — иконка трея и карточки.

### Благодарности

Протокол чтения заряда изначально реверс-инжинирен в [brhnshn/AulaTray](https://github.com/brhnshn/AulaTray). Код этого репозитория написан с нуля.

### Отказ от ответственности

Неофициальный проект, не связан с AULA и не одобрен ею. Предоставляется «как есть».

### Лицензия

MIT
