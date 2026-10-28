using ModelOS.Model;

namespace ModelOS.Presentation;

/// <summary>
/// Экранные значения модели. 
/// </summary>
public sealed record ModelDisplay(IReadOnlyDictionary<string, string> Values, IReadOnlyList<object[]> ProcessRows)
{
    public static ModelDisplay Create(OsModel model, double measurementSeconds, long measurementTicks)
    {
        bool assigned = model.CurrentProcessIndex >= 0;
        PswEntry? current = assigned ? model.Psw[model.CurrentProcessIndex] : null;
        string commandType = model.LastCommand?.Operation switch
        {
            OperationCode.Add => "Вычисление: сложение",
            OperationCode.Subtract => "Вычисление: вычитание",
            OperationCode.Multiply => "Вычисление: умножение",
            OperationCode.Io => "Запрос ввода-вывода",
            OperationCode.Exit => "Завершение задания",
            _ => "Команд ещё не было"
        };
        string prefix = $"Задание {model.LastCommandTaskId}: ";
        string detail = model.LastCommandText.StartsWith(prefix, StringComparison.Ordinal)
            ? model.LastCommandText[prefix.Length..] : model.LastCommandText;
        if (detail.Length > 0) detail = char.ToUpper(detail[0]) + detail[1..];
        var values = new Dictionary<string, string>
        {
            ["time"] = model.TotalTicks.ToString(),
            ["speed"] = $"{model.Speed:0.###} такт/с",
            ["actualSpeed"] = measurementSeconds > 0
                ? $"{measurementTicks / measurementSeconds:0.##} такт/с за {measurementSeconds:0.#} с" : "Измерение ещё не начато",
            ["processor"] = assigned ? "Процесс назначен" : "Ожидание готового процесса",
            ["assigned"] = current is null ? "Нет" : $"Задание {current.TaskId} (строка {model.CurrentProcessIndex})",
            ["pc"] = current is null ? "–" : model.Pc.ToString(),
            ["quantum"] = current is null ? "–" : $"{model.QuantumRemaining} из {model.QuantumTicks} тактов",
            ["cpuTick"] = model.TotalTicks == 0 ? "Модель ещё не выполняла такты"
                : model.CpuExecutedThisTick ? "Выполнена одна команда" : "ЦПр простаивал",
            ["commandOwner"] = model.LastCommand is null ? "–" : $"Задание {model.LastCommandTaskId}",
            ["commandType"] = commandType,
            ["commandTime"] = model.LastCommand is null ? "–" : $"Такт {model.LastCommandTick}",
            ["commandDetail"] = model.LastCommand is null ? "–" : detail,
            ["completed"] = model.CompletedTaskCount.ToString(),
            ["commands"] = model.ExecutedCommandCount.ToString(),
            ["ioRequests"] = model.IoRequestCount.ToString(),
            ["ioCompleted"] = model.IoCompletedCount.ToString(),
            ["timerInterrupts"] = model.TimerInterruptCount.ToString(),
            ["memoryTotal"] = $"{model.MemSize} ячеек",
            ["memoryUsed"] = $"{model.MemUsed} ячеек",
            ["memoryFree"] = $"{model.FreeMemory} ячеек",
            ["buffer"] = $"Задание {model.PswTask.TaskId}",
            ["bufferSize"] = $"{model.PswTask.TaskSize} ячеек; {model.PswTask.CommandCount} команд",
            ["loading"] = model.CheckFreeMem() ? "Ресурсов достаточно" : "Ожидание свободных ресурсов",
            ["algorithm"] = "Отн. приоритеты; FIFO",
            ["ioParameters"] = $"{model.IoCommandPercent} % I/O; {model.IoTicks} такт.",
            ["processCount"] = $"{model.ProcCount} из {OsModel.PswCapacity}",
            ["lastInterrupt"] = model.LastInterruptText,
            ["queues"] = $"Готовы: {model.Psw.Count(p => p.State == ProcState.Ready)}; " +
                $"активны: {model.Psw.Count(p => p.State == ProcState.Running)}; " +
                $"I/O: {model.Psw.Count(p => p.State == ProcState.BlockedByIo)}",
            ["freeSlots"] = (OsModel.PswCapacity - model.ProcCount).ToString(),
            ["priorityLevels"] = model.PriorityLevels.ToString(),
            ["bufferPriority"] = model.PswTask.Priority.ToString(),
            ["utilization"] = model.TotalTicks > 0
                ? $"{100.0 * model.ExecutedCommandCount / model.TotalTicks:0.##} %" : "–",
            ["throughput"] = model.TotalTicks > 0
                ? $"{(double)model.CompletedTaskCount / model.TotalTicks:0.####}" : "–",
            ["history"] = model.ExecutionHistory.Count == 0 ? "Исполнений ещё нет"
                : string.Join(" → ", model.ExecutionHistory.Select(e => e.TaskId)),
            ["sample"] = $"Завершённые с начала запуска: {model.Statistics.Count}",
            ["avgSize"] = Average(model.Statistics.TotalSize, model.Statistics.Count),
            ["avgCommands"] = Average(model.Statistics.TotalCommands, model.Statistics.Count),
            ["avgPriority"] = Average(model.Statistics.TotalPriority, model.Statistics.Count),
            ["avgCpu"] = Average(model.Statistics.TotalCpuTicks, model.Statistics.Count),
            ["avgReady"] = Average(model.Statistics.TotalReadyTicks, model.Statistics.Count),
            ["avgIo"] = Average(model.Statistics.TotalIoTicks, model.Statistics.Count),
            ["avgSystem"] = Average(model.Statistics.TotalSystemTicks, model.Statistics.Count)
        };
        var reasons = new List<string>();
        if (model.Psw.All(p => !p.IsFree)) reasons.Add("Нет свободной строки PSW");
        if (model.PswTask.TaskSize > model.FreeMemory) reasons.Add("Недостаточно памяти");
        values["loading"] = reasons.Count == 0 ? "Ресурсов достаточно" : string.Join("; ", reasons);
        values["memorySummary"] = $"{model.MemUsed} / {model.MemSize}; свободно {model.FreeMemory}";
        values["interruptCounts"] = $"{model.IoCompletedCount} / {model.TimerInterruptCount}";
        values["commandSummary"] = model.LastCommand is null ? "Команд ещё не было"
            : $"Задание {model.LastCommandTaskId}; такт {model.LastCommandTick}; {detail}";
        values["avgInput"] = $"Размер: {values["avgSize"]} ячеек; команд: {values["avgCommands"]}; приоритет: {values["avgPriority"]}";
        values["avgOutput"] = $"CPU: {values["avgCpu"]}; готов: {values["avgReady"]}; I/O: {values["avgIo"]}; в системе: {values["avgSystem"]}";
        var rows = model.Psw.Select((p, i) => p.IsFree
            ? new object[] { i, "–", "–", "–", "–", "–", "Отсутствует", "–", "–", "–", "–", "–" }
            : new object[] { i, p.TaskId, p.TaskSize, p.CommandCount, p.ProcessPc, p.Priority,
                p.State.ToDisplay(), p.State == ProcState.BlockedByIo ? p.IoTicksRemaining : "–",
                p.CpuTicks, p.ReadyTicks, p.IoBlockedTicks, model.TotalTicks - p.LoadedAtTick }).ToArray();
        return new(values, rows);
    }

    private static string Average(long sum, long count) => count == 0 ? "–" : ((double)sum / count).ToString("0.##");
}
