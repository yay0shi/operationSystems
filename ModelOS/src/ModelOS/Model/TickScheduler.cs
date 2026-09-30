namespace ModelOS.Model;

/// <summary>
/// сколько тактов пора выполнить по прошедшему времени
/// </summary>
public sealed class TickScheduler
{
    private double _fractionalTicks;

    public int Advance(TimeSpan elapsed, double speed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (speed <= 0 || !double.IsFinite(speed))
            throw new ArgumentOutOfRangeException(nameof(speed));

        _fractionalTicks += elapsed.TotalSeconds * speed;
        int wholeTicks = (int)Math.Floor(_fractionalTicks);
        _fractionalTicks -= wholeTicks;
        return wholeTicks;
    }

    public void Reset() => _fractionalTicks = 0;
}
