namespace AulaMonitor;

/// <summary>Источник питания клавиатуры в момент снимка.</summary>
public enum ChargeState
{
    OnBattery,
    Charging
}

/// <summary>Состояние связи с клавиатурой.</summary>
public enum LinkState
{
    /// <summary>Клавиатура отвечает на опрос прямо сейчас.</summary>
    Active,
    /// <summary>Клавиатура не ответила несколько опросов подряд — скорее всего спит.</summary>
    Idle,
    /// <summary>Канал связи не найден вовсе (донгл вынут, провод отключён и т.д.).</summary>
    Offline
}

/// <summary>Физический канал, по которому сейчас читаются данные.</summary>
public enum LinkChannel
{
    Usb,
    Wireless24G,
    Bluetooth,
    None
}

/// <summary>Снимок состояния клавиатуры на момент одного опроса.</summary>
public readonly record struct BatterySnapshot(
    int Percent,
    ChargeState Charge,
    LinkState Link,
    LinkChannel Channel,
    string KeyboardName)
{
    public static BatterySnapshot Offline(string keyboardName) =>
        new(0, ChargeState.OnBattery, LinkState.Offline, LinkChannel.None, keyboardName);
}
