namespace ModelOS.Model;

/// <summary>Причины переходов, обрабатываемые регулировщиком.</summary>
public enum ProcessEvent
{
    Loaded,
    Assigned,
    QuantumExpired,
    IoRequested,
    IoCompleted,
    Finished
}
