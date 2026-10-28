namespace ModelOS.Model;

/// <summary>
/// Фактический исполнитель одного такта ЦПр
/// </summary>
public sealed record ExecutionEntry(long Tick, int TaskId, int PswIndex);
