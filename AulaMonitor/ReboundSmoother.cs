namespace AulaMonitor;

/// <summary>
/// Литий-ионные батареи после снятия нагрузки показывают кратковременный "отскок"
/// напряжения вверх (incorrect transient rebound), из-за чего сырой процент заряда
/// может на секунду подскочить на несколько пунктов и тут же упасть обратно. Этот
/// класс — чистая state machine без каких-либо зависимостей от Windows/HID: не
/// пропускает такой скачок в UI, пока он не продержится несколько опросов подряд
/// (тогда это уже не отскок, а реальная зарядка).
/// </summary>
public sealed class ReboundSmoother
{
    private const int DefaultStart = 90;
    private const int RealJumpThreshold = 8;
    private const int ConfirmationsNeeded = 5;

    private int _candidate;
    private int _candidateStreak;

    public int Current { get; private set; } = DefaultStart;

    public ReboundSmoother(int startPercent = DefaultStart)
    {
        Current = InRange(startPercent) ? startPercent : DefaultStart;
    }

    public void Reset(int percent = DefaultStart)
    {
        Current = InRange(percent) ? percent : DefaultStart;
        _candidate = 0;
        _candidateStreak = 0;
    }

    public int Smooth(int rawPercent, ChargeState charge)
    {
        if (!InRange(rawPercent))
            return Current;

        // На зарядке рост — норма, никакой фильтрации не нужно.
        if (charge == ChargeState.Charging)
        {
            Accept(rawPercent);
            return Current;
        }

        // От батареи значение падает или держится — это естественный разряд.
        if (rawPercent <= Current)
        {
            Accept(rawPercent);
            return Current;
        }

        // От батареи значение выросло. Большой скачок (воткнули в зарядку мимо
        // детекта режима, например по USB без переключения питания) — принимаем сразу.
        if (rawPercent - Current >= RealJumpThreshold)
        {
            Accept(rawPercent);
            return Current;
        }

        // Небольшой рост (1-7%) от батареи — похоже на voltage rebound. Ждём, пока
        // одно и то же значение продержится несколько опросов подряд, прежде чем поверить.
        if (_candidate == rawPercent)
        {
            _candidateStreak++;
        }
        else
        {
            _candidate = rawPercent;
            _candidateStreak = 1;
        }

        if (_candidateStreak >= ConfirmationsNeeded)
        {
            Accept(rawPercent);
        }

        return Current;
    }

    private void Accept(int percent)
    {
        Current = percent;
        _candidate = 0;
        _candidateStreak = 0;
    }

    private static bool InRange(int percent) => percent is > 0 and <= 100;
}
