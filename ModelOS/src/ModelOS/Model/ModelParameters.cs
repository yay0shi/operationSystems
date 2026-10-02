namespace ModelOS.Model;

/// <summary>Начальные параметры генерации и загрузки заданий.</summary>
public sealed record ModelParameters(
    int MemorySize = OsModel.DefaultMemSize,
    int TaskSize = OsModel.DefaultTaskSize,
    int TaskCommands = OsModel.DefaultTaskCommands)
{
    public void Validate()
    {
        if (MemorySize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MemorySize));
        if (TaskSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(TaskSize));
        if (TaskCommands <= 0)
            throw new ArgumentOutOfRangeException(nameof(TaskCommands));
    }
}
