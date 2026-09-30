namespace ModelOS.Model;

public sealed class OsModel
{
    // значения по умолчанию
    public const double MinSpeed = 0.1;      
    public const double MaxSpeed = 1000.0;   
    public const double SpeedStepUp = 1.1;   
    public const double SpeedStepDown = 0.9; 
    public const int PswCapacity = 16; // мест в таблице PSW 

    public const double DefaultSpeed = 10.0; 
    public const int DefaultMemSize = 1024;  // V_озу
    public const int DefaultTaskSize = 100;  // V0
    public const int DefaultTaskCommands = 500; // N0

    /// <summary>
    /// Аппаратный счётчик команд
    /// </summary>
    public long Pc { get; private set; }

    /// <summary>
    /// Скорость модели
    /// </summary>
    public double Speed { get; private set; } = DefaultSpeed;

    /// <summary>
    /// Флаг завершения моделирования
    /// </summary>
    public bool Finish { get; private set; }

    /// <summary>
    /// Объём памяти модели
    /// </summary>
    public int MemSize { get; private set; } = DefaultMemSize;

    /// <summary>
    /// Занято памяти
    /// </summary>
    public int MemUsed { get; private set; }

    /// <summary>
    /// Число загруженных заданий
    /// </summary>
    public int ProcCount { get; private set; }

    /// <summary>
    /// Таблица слов состояний процессов
    /// </summary>
    public PswEntry[] Psw { get; } = new PswEntry[PswCapacity];

    /// <summary>
    /// Буфер нового задания
    /// </summary>
    public PswEntry PswTask { get; } = new PswEntry();

    /// <summary>
    ///  Task_Id
    /// </summary>
    private int _taskSeq;

    public OsModel()
    {
        for (int i = 0; i < Psw.Length; i++)
            Psw[i] = new PswEntry();
    }

    /// <summary>
    /// Свободная память: V_free = V_озу − V_занято
    /// </summary>
    public int FreeMemory => MemSize - MemUsed;

    public void InitModel()
    {
        Pc = 0;
        Speed = DefaultSpeed;
        Finish = false;
        MemSize = DefaultMemSize;
        MemUsed = 0;
        ProcCount = 0;
        _taskSeq = 0;
        for (int i = 0; i < Psw.Length; i++)
            Psw[i].Clear();
        GenNewTask(); // первое задание в буфере для LoadCycle
    }
    
    public void GenNewTask()
    {
        _taskSeq++;
        PswTask.TaskId = _taskSeq;
        PswTask.TaskSize = DefaultTaskSize;
        PswTask.CommandCount = DefaultTaskCommands;
        PswTask.State = ProcState.Ready;
        PswTask.ProcessPc = 0;
    }

    /// <summary>
    /// Есть свободный индекс i в PSW
    /// </summary>
    /// <returns></returns>
    private int FindFreePsw()
    {
        for (int i = 0; i < Psw.Length; i++)
            if (Psw[i].IsFree)
                return i;
        return -1;
    }

    public bool CheckFreeMem()
    {
        if (FindFreePsw() < 0)
            return false;
        return PswTask.TaskSize <= FreeMemory; 
    }

    /// <summary>
    /// Возвращает true, если задание загружено
    /// </summary>
    /// <returns></returns>
    public bool LoadTask()
    {
        if (!CheckFreeMem())
            return false;
        int i = FindFreePsw();
        Psw[i] = PswTask.Clone();   
        MemUsed += PswTask.TaskSize;
        ProcCount++;
        GenNewTask();    
        return true;
    }
    
    public void LoadCycle()
    {
        while (CheckFreeMem())
            LoadTask();
    }
    
    public void DoTick()
    {
        int active = FirstReadyIndex; 
        if (active < 0)
            return; // нет активного процесса – пропуск такта
        Pc++;
        Psw[active].ProcessPc++;
    }

    /// <summary>
    /// Индекс первого готового процесса (заглушка планировщика lab1)
    /// </summary>
    public int FirstReadyIndex
    {
        get
        {
            for (int i = 0; i < Psw.Length; i++)
                if (Psw[i].State == ProcState.Ready)
                    return i;
            return -1;
        }
    }
    
    /// <summary>
    /// задержка T = 1 / Speed
    /// </summary>
    public TimeSpan Delay => TimeSpan.FromSeconds(1.0 / Speed);

    public void DoDirective(Directive dir)
    {
        switch (dir)
        {
            case Directive.Finish:
                Finish = true;
                break;
            case Directive.SpeedUp:
                Speed = Math.Min(MaxSpeed, Speed * SpeedStepUp);
                break;
            case Directive.SlowDown:
                Speed = Math.Max(MinSpeed, Speed * SpeedStepDown);
                break;
            case Directive.Help:
            case Directive.None:
            default:
                break;
        }
    }

    public static string ShowHelpText() =>
        "вариант 27: один ЦПр; в lab1 выбор первого готового процесса" + Environment.NewLine +
        Environment.NewLine +
        "НАЗНАЧЕНИЕ" + Environment.NewLine +
        "  Каждый такт: счетчик команд PC увеличивается на единицу." + Environment.NewLine +
        Environment.NewLine +
        "КОМАНДЫ ОПЕРАТОРА" + Environment.NewLine +
        "  ?       вывести эту справку" + Environment.NewLine +
        "  +       ускорить моделирование на 10 %" + Environment.NewLine +
        "  −       замедлить моделирование на 10 %" + Environment.NewLine +
        "  выход   завершить моделирование" + Environment.NewLine +
        Environment.NewLine +
        "ОКНО МОДЕЛИ" + Environment.NewLine +
        "  Кнопки  четыре директивы оператора" + Environment.NewLine +
        "  PC и Speed – индикаторы справа от кнопок" + Environment.NewLine +
        "  Факт – измеренная скорость с последнего изменения Speed" + Environment.NewLine +
        "  Таблица PSW – задания, счётчики, состояния" + Environment.NewLine +
        "  Строка ввода и журнал – команды оператора" + Environment.NewLine +
        Environment.NewLine +
        "ДИАПАЗОНЫ" + Environment.NewLine +
        $"  Speed   {MinSpeed} ... {MaxSpeed} такт/с (задержка T = 1 / Speed: 10 с ... 1 мс)" + Environment.NewLine +
        $"  PSW     {PswCapacity} строк (процессы 0–{PswCapacity - 1})" + Environment.NewLine +
        $"  Старт   Speed = {DefaultSpeed} такт/с, память {DefaultMemSize}," + Environment.NewLine +
        $"          задание {DefaultTaskSize} / {DefaultTaskCommands} команд" + Environment.NewLine +
        Environment.NewLine +
        "СПРАВКА ИЗ КОМАНДНОЙ СТРОКИ" + Environment.NewLine +
        "  ModelOS.exe /?   показать справку и выйти";
}
