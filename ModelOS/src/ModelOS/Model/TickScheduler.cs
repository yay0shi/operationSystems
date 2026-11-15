namespace ModelOS.Model;

/// <summary>
/// сколько тактов пора выполнить по прошедшему времени
/// </summary>
public sealed class TickScheduler
{
    private double _fractionalTicks;

    public int Advance(TimeSpan elapsed, double speed, int maxTicks = int.MaxValue)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (speed <= 0 || !double.IsFinite(speed))
            throw new ArgumentOutOfRangeException(nameof(speed));

        if (maxTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTicks));

        _fractionalTicks += elapsed.TotalSeconds * speed;
        int wholeTicks = (int)Math.Min(Math.Floor(_fractionalTicks), maxTicks);
        _fractionalTicks -= wholeTicks;
        return wholeTicks;
    }

    public void Reset() => _fractionalTicks = 0;
}
