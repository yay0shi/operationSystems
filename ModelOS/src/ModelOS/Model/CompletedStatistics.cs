namespace ModelOS.Model;

/// <summary>
/// Суммы по завершённым заданиям с начала запуска модели
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
    public long TotalSystemTicks { get; private set; }

    public void Add(PswEntry process, long finishedAtTick)
    {
        Count++;
        TotalSize += process.TaskSize;
        TotalCommands += process.CommandCount;
        TotalPriority += process.Priority;
        TotalCpuTicks += process.CpuTicks;
        TotalReadyTicks += process.ReadyTicks;
        TotalIoTicks += process.IoBlockedTicks;
        TotalSystemTicks += finishedAtTick - process.LoadedAtTick;
    }

    public void Clear()
    {
        Count = TotalSize = TotalCommands = TotalPriority = 0;
        TotalCpuTicks = TotalReadyTicks = TotalIoTicks = TotalSystemTicks = 0;
    }
}
