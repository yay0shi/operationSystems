namespace ModelOS.Model;

/// <summary>
/// Итоговая выборка содержит только завершенные задания текущего запуска
/// </summary>
public sealed class CompletedStatistics
{
    public long Count { get; private set; }
    public long TotalSize { get; private set; }
    public long TotalCommands { get; private set; }
    public long TotalPriority { get; private set; }
    public long TotalCpuTicks { get; private set; }
    public long TotalReadyTicks { get; private set; }
    public long TotalIoTicks { get; private set; }
    public long TotalTurnaroundTicks { get; private set; }
    public long TotalMonoTicks { get; private set; }
    public long TotalOwnSystemTicks { get; private set; }
    public double TotalIncreasePercent { get; private set; }
    public double TotalReadyPercent { get; private set; }
    public CompletedTask? LastCompleted { get; private set; }

    public void Add(PswEntry process, long finishedAtTick)
    {
        var result = new CompletedTask(process.TaskId, process.MonoTicks,
            finishedAtTick - process.LoadedAtTick, process.CpuTicks,
            process.ReadyTicks, process.IoBlockedTicks, process.SystemOutsideReadyTicks);
        Count++;
        TotalSize += process.TaskSize;
        TotalCommands += process.CommandCount;
        TotalPriority += process.Priority;
        TotalCpuTicks += result.CpuTicks;
        TotalReadyTicks += result.ReadyTicks;
        TotalIoTicks += result.IoTicks;
        TotalTurnaroundTicks += result.MultiTicks;
        TotalMonoTicks += result.MonoTicks;
        TotalOwnSystemTicks += result.SystemTicks;
        TotalIncreasePercent += result.ExecutionIncreasePercent;
        TotalReadyPercent += result.ReadyPercent;
        LastCompleted = result;
    }

    public void Clear()
    {
        Count = TotalSize = TotalCommands = TotalPriority = 0;
        TotalCpuTicks = TotalReadyTicks = TotalIoTicks = TotalTurnaroundTicks = 0;
        TotalMonoTicks = TotalOwnSystemTicks = 0;
        TotalIncreasePercent = TotalReadyPercent = 0;
        LastCompleted = null;
    }
}
