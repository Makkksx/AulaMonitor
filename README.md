# Aula Monitor

[Русская версия](README.ru.md)

A small Windows tray app that shows the battery level of **AULA wireless keyboards**.
The official AULA software does not display it for many models.

- Battery-shaped tray icon filled by charge level (green > 70%, yellow 30–70%, red < 30%), bolt while charging
- Click the icon for a status card: model, percentage, connection (USB / 2.4G / Bluetooth), awake / asleep / offline
- Low-battery warning (≤ 20%) as an always-on-top window in the screen corner: plays one beep, never steals focus from the window you are typing in, click anywhere on it to dismiss. It is a regular window, not a Windows notification, so Do Not Disturb / Focus Assist does not hide it
- The keyboard model is detected automatically when it connects
- Adjustable polling interval, optional start with Windows

## Install

1. Install the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (Windows x64).
2. Download `AulaMonitor-win-x64.zip` from [Releases](../../releases) and unzip it anywhere.
3. Run `AulaMonitor.exe`. The icon appears in the tray (it may be in the hidden-icons overflow).

Right-click the icon for the menu: **Статус клавиатуры** (status card), **Обновить статус** (refresh now),
**Частота опроса** (polling interval), **Запускать с Windows** (autostart, off by default), **Выход** (exit).

## Supported keyboards

Developed and tested on **AULA AU75** over the 2.4G dongle. Models in the built-in catalog share the same
2.4G dongle chipset (Compx, USB VID `0x3554`) and should work the same way, but have not all been tested:

AULA AU75 · F75 · F87 · F99 · F68

Any other AULA keyboard with a `0x3554` dongle is still polled and shows up as "AULA Wireless Keyboard".
Wired USB (VID `0x258A`) and Bluetooth LE are supported on a best-effort basis.

### Add your model

Find the dongle's PID: Device Manager → *Human Interface Devices* → the dongle's HID entry → *Details* →
*Hardware Ids* (e.g. `HID\VID_3554&PID_FA09&Col01`). Then create `keyboards.json` next to `AulaMonitor.exe`:

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

Restart the app.

## Troubleshooting

- **Keyboard no longer goes to sleep while the app runs.** Every poll is traffic to the dongle and resets the
  keyboard's own sleep timer. Pick a polling interval longer than your keyboard's sleep timeout
  (tray menu → *Частота опроса*; the default is 45 s).
- **Shows "нет связи" with the dongle plugged in.** The keyboard may be off or the dongle uses a PID/VID not
  covered above. Check the IDs as described in *Add your model*.
- **Crash:** the app writes `crash.log` next to the exe with the error details.

## Build

Requires Windows and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```
dotnet publish AulaMonitor/AulaMonitor.csproj -c Release -r win-x64 --self-contained false -o dist
```

Pushing a tag like `v1.0.0` makes the GitHub Actions workflow build and attach the zip to a release.

## How it works

The HID commands used to read the battery (an `0x13 0x4A` request through the 2.4G dongle, a feature report over
USB) are not documented by AULA; they were reverse-engineered. Layout: `Hid/` talks to the device (one
channel class per connection type), `ReboundSmoother` filters false upward jumps in the reading, `Services/`
holds polling, settings and notifications, `UI/` draws the tray icon and cards.

## Credits

The battery-reading protocol (command bytes, dongle VID/PID values) was originally reverse-engineered and
published by [brhnshn/AulaTray](https://github.com/brhnshn/AulaTray). The code in this repository is a
from-scratch rewrite with a different architecture and UI.

## Disclaimer

Unofficial; not affiliated with or endorsed by AULA. AULA is a trademark of its owner.
The software is provided as is — see [LICENSE](LICENSE).

## License

MIT
