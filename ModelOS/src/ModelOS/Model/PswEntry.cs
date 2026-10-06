namespace ModelOS.Model;

public sealed class PswEntry
{
    /// <summary>
    /// Идентификатор задания, 0 - нет задания
    /// </summary>
    public int TaskId { get; set; }

    /// <summary>
    /// Размер задания
    /// </summary>
    public int TaskSize { get; set; }

    /// <summary>
    /// Число команд задания
    /// </summary>
    public int CommandCount { get; set; }

    /// <summary>
    /// Счётчик команд процесса
    /// </summary>
    public long ProcessPc { get; set; }

    /// <summary>
    /// Относительный приоритет
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Порядок попадания в очередь готовых процессов
    /// </summary>
    public long ReadyOrder { get; set; }

    /// <summary>
    /// Оставшаяся длительность операции ввода-вывода в модельных тактах
    /// </summary>
    public int IoTicksRemaining { get; set; }

    /// <summary>
    /// Данные процесса, адреса математические: от 0 до TaskSize - 1
    /// </summary>
    public double[] OperandMemory { get; set; } = [];

    /// <summary>
    /// Состояние процесса
    /// </summary>
    public ProcState State { get; set; } = ProcState.Absent;

    /// <summary>
    /// Свободна ли строка таблицы
    /// </summary>
    public bool IsFree => State == ProcState.Absent;

    /// <summary>
    /// Сброс строки в Отсутствует
    /// </summary>
    public void Clear()
    {
        TaskId = 0;
        TaskSize = 0;
        CommandCount = 0;
        ProcessPc = 0;
        Priority = 0;
        ReadyOrder = 0;
        IoTicksRemaining = 0;
        OperandMemory = [];
        State = ProcState.Absent;
    }

    /// <summary>
    /// Копия для PSW[i] = PSW_Task
    /// </summary>
    public PswEntry Clone()
    {
        var copy = (PswEntry)MemberwiseClone();
        copy.OperandMemory = (double[])OperandMemory.Clone();
        return copy;
    }
}
