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
    /// Активен на центральном процессоре
    /// </summary>
    Running = 2,

    /// <summary
    /// >Новое задание загружается в память
    /// </summary>
    Loading = 3,

    /// <summary>
    /// Выполняется инициализация операции ввода-вывода
    /// </summary>
    IoInitializing = 4,

    /// <summary>
    /// Выполнение операции ввода-вывода завершено
    /// </summary>
    IoCompleted = 5,

    /// <summary>
    /// Процесс ожидает освобождения или выделения памяти
    /// </summary>
    BlockedByMemory = 6,

    /// <summary>
    /// Процесс ожидает завершения операции ввода-вывода
    /// </summary>
    BlockedByIo = 7,

    /// <summary>
    /// Выполнение процесса приостановлено
    /// </summary>
    Suspended = 8,
}

public static class ProcStateNames
{
    public static string ToDisplay(this ProcState state) => state switch
    {
        ProcState.Absent => "Отсутствует",
        ProcState.Ready => "Готов",
        ProcState.Running => "Активен",
        ProcState.Loading => "Загружается",
        ProcState.IoInitializing => "Инициализация ввода вывода",
        ProcState.IoCompleted => "Конец ввода (вывода)",
        ProcState.BlockedByMemory => "Блокирован по обращению к памяти",
        ProcState.BlockedByIo => "Блокирован по выполнению ввода-вывода",
        ProcState.Suspended => "Приостановлен",
        _ => "?",
    };
}
