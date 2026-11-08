namespace ModelOS.Model;

/// <summary>
/// Начальные параметры генерации и загрузки заданий
/// </summary>
public sealed record ModelParameters(
    int MemorySize = OsModel.DefaultMemSize,
    int TaskSize = OsModel.DefaultTaskSize,
    int TaskCommands = OsModel.DefaultTaskCommands,
    int QuantumTicks = OsModel.DefaultQuantumTicks,
    int PriorityLevels = OsModel.DefaultPriorityLevels,
    int IoCommandPercent = 20,
    int IoTicks = 5,
    int LoadTicks = 1,
    int DispatchTicks = 1,
    int IoInitializationTicks = 1,
    int IoInterruptTicks = 1,
    int UnloadTicks = 0,
    int? TaskLimit = null)
{
    public void Validate()
    {
        if (LoadTicks < 0 || DispatchTicks < 0 || IoInitializationTicks < 0 || IoInterruptTicks < 0 || UnloadTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(LoadTicks), "Системные длительности неотрицательны");
        if (TaskLimit is <= 0) throw new ArgumentOutOfRangeException(nameof(TaskLimit));
        if (MemorySize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MemorySize));
        if (TaskSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(TaskSize));
        if (TaskCommands <= 0)
            throw new ArgumentOutOfRangeException(nameof(TaskCommands));
        if (QuantumTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(QuantumTicks));
        if (PriorityLevels <= 0)
            throw new ArgumentOutOfRangeException(nameof(PriorityLevels));
        if (IoCommandPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(IoCommandPercent));
        if (IoTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(IoTicks));
    }
}
