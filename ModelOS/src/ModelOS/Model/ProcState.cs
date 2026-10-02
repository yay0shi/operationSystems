namespace ModelOS.Model;

public enum ProcState
{
    /// <summary>
    /// Отсутствует - строка PSW свободна
    /// </summary>
    Absent = 0,

    /// <summary>
    /// Готов - процесс загружен и ждёт процессора
    /// </summary>
    Ready = 1,
    /// <summary>
    /// Выполняется на центральном процессоре
    /// </summary>
    Running = 2,
}

public static class ProcStateNames
{
    public static string ToDisplay(this ProcState state) => state switch
    {
        ProcState.Absent => "Отсутствует",
        ProcState.Ready => "Готов",
        ProcState.Running => "Выполняется",
        _ => "?",
    };
}
