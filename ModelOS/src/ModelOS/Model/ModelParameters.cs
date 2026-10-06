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
    int? RandomSeed = null)
{
    public void Validate()
    {
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
