namespace ModelOS.Model;

/// <summary>
/// Системная операция занимает ЦПр, но не останавливает устройства I/O
/// </summary>
internal sealed class SystemWork(string name, int ticks, int owner, Action complete)
{
    public string Name { get; } = name;
    public int Remaining { get; set; } = ticks;
    public int Owner { get; } = owner;
    public Action Complete { get; } = complete;
}

/// <summary>
/// Итоги задания сохраняются до очистки его строки PSW
/// </summary>
public sealed record CompletedTask(int TaskId, long MonoTicks, long MultiTicks,
    long CpuTicks, long ReadyTicks, long IoTicks, long SystemTicks)
{
    public double ExecutionIncreasePercent => 100.0 * (MultiTicks - MonoTicks) / MonoTicks;
    public double ReadyPercent => 100.0 * ReadyTicks / MultiTicks;
}
