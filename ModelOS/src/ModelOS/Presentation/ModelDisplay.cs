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
            ["algorithm"] = "Относительные приоритеты; FIFO",
            ["ioParameters"] = $"{model.IoCommandPercent} % команд; {model.IoTicks} тактов на I/O",
            ["processCount"] = $"{model.ProcCount} из {OsModel.PswCapacity}",
            ["lastInterrupt"] = model.LastInterruptText
        };
        var rows = model.Psw.Select((p, i) => p.IsFree
            ? new object[] { i, "–", "–", "–", "–", "–", "Отсутствует", "–" }
            : new object[] { i, p.TaskId, p.TaskSize, p.CommandCount, p.ProcessPc, p.Priority,
                p.State.ToDisplay(), p.State == ProcState.BlockedByIo ? p.IoTicksRemaining : "–" }).ToArray();
        return new(values, rows);
    }
}
