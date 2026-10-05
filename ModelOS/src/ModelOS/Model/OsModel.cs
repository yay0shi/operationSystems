namespace ModelOS.Model;

public sealed class OsModel
{
    private readonly ModelParameters _parameters;

    // Назначение процесса и диспетчеризация
    public int CurrentProcessIndex { get; private set; } = -1;
    public string ProcessorState { get; private set; } = "Ожидание";
    // значения по умолчанию
    public const double MinSpeed = 0.1;      
    public const double MaxSpeed = 1000.0;   
    public const double SpeedStepUp = 1.1;   
    public const double SpeedStepDown = 0.9; 
    public const int PswCapacity = 16; // мест в таблице PSW 
    public const int DefaultQuantumTicks = 5;
    public const int DefaultPriorityLevels = 3;

    public const double DefaultSpeed = 10.0; 
    public const int DefaultMemSize = 1024;  // V_озу
    public const int DefaultTaskSize = 100;  // V0
    public const int DefaultTaskCommands = 500; // N0

    /// <summary>
    /// Аппаратный счётчик команд
    /// </summary>
    public long Pc { get; private set; }

    /// <summary>
    /// Общее число исполненных модельных команд; монотонный счётчик для измерения скорости
    /// </summary>
    public long TotalTicks { get; private set; }

    public int QuantumTicks => _parameters.QuantumTicks;
    public int QuantumRemaining { get; private set; }

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
    private long _readySequence;
    private bool _deferDispatch;

    public OsModel(ModelParameters? parameters = null)
    {
        _parameters = parameters ?? new ModelParameters();
        _parameters.Validate();
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
        TotalTicks = 0;
        Speed = DefaultSpeed;
        Finish = false;
        MemSize = _parameters.MemorySize;
        CurrentProcessIndex = -1;
        ProcessorState = "Ожидание";
        QuantumRemaining = 0;
        MemUsed = 0;
        ProcCount = 0;
        _taskSeq = 0;
        _readySequence = 0;
        for (int i = 0; i < Psw.Length; i++)
            Psw[i].Clear();
        GenNewTask(); // первое задание в буфере для LoadCycle
    }
    
    public void GenNewTask()
    {
        _taskSeq++;
        PswTask.TaskId = _taskSeq;
        PswTask.TaskSize = _parameters.TaskSize;
        PswTask.CommandCount = _parameters.TaskCommands;
        // приоритеты распределяются циклически от 1 до PriorityLevels
        PswTask.Priority = ((_taskSeq - 1) % _parameters.PriorityLevels) + 1;
        PswTask.State = ProcState.Ready;
        PswTask.ProcessPc = 0;
        PswTask.ReadyOrder = 0;
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
        Psw[i].ReadyOrder = ++_readySequence;
        MemUsed += PswTask.TaskSize;
        ProcCount++;
        GenNewTask();    
        if (CurrentProcessIndex < 0 && !_deferDispatch)
            DispatchNextProcess();
        return true;
    }
    
    public void LoadCycle()
    {
        _deferDispatch = true;
        try
        {
            while (CheckFreeMem())
                LoadTask();
        }
        finally
        {
            _deferDispatch = false;
        }

        if (CurrentProcessIndex < 0)
            DispatchNextProcess();
    }
    
    public void DoTick()
    {
        if (CurrentProcessIndex < 0)
            DispatchNextProcess();
        if (CurrentProcessIndex < 0)
            return;

        int active = CurrentProcessIndex;
        Pc++;
        TotalTicks++;
        Psw[active].ProcessPc = Pc;
        QuantumRemaining--;

        if (QuantumRemaining == 0)
            DispatchNextProcess();
    }

    /// <summary>
    /// Сохраняет аппаратный счётчик команд в слове состояния активного процесса.
    /// </summary>
    public void SaveProcessState(int processIndex)
    {
        if ((uint)processIndex >= Psw.Length || Psw[processIndex].IsFree)
            throw new ArgumentOutOfRangeException(nameof(processIndex));
        Psw[processIndex].ProcessPc = Pc;
    }

    /// <summary>Восстанавливает аппаратный счётчик команд из слова состояния процесса.</summary>
    public void RestoreProcessState(int processIndex)
    {
        if ((uint)processIndex >= Psw.Length || Psw[processIndex].IsFree)
            throw new ArgumentOutOfRangeException(nameof(processIndex));
        Pc = Psw[processIndex].ProcessPc;
    }

    /// <summary>
    /// Выбирает процесс с максимальным приоритетом, при равенстве – раньше вставленный в очередь
    /// </summary>
    public int GetNextProcessIndex()
    {
        int best = -1;
        for (int i = 0; i < Psw.Length; i++)
        {
            PswEntry candidate = Psw[i];
            if (candidate.State != ProcState.Ready)
                continue;
            if (best < 0 || candidate.Priority > Psw[best].Priority ||
                (candidate.Priority == Psw[best].Priority && candidate.ReadyOrder < Psw[best].ReadyOrder))
                best = i;
        }
        return best;
    }

    /// <summary>
    /// Сохраняет прежний контекст и назначает ЦПр следующему готовому процессу
    /// </summary>
    public void DispatchNextProcess()
    {
        if (CurrentProcessIndex >= 0)
        {
            int previous = CurrentProcessIndex;
            SaveProcessState(previous);
            Psw[previous].State = ProcState.Ready;
            Psw[previous].ReadyOrder = ++_readySequence;
            CurrentProcessIndex = -1;
        }

        int next = GetNextProcessIndex();
        if (next < 0)
        {
            QuantumRemaining = 0;
            ProcessorState = "Ожидание";
            return;
        }

        Psw[next].State = ProcState.Running;
        CurrentProcessIndex = next;
        RestoreProcessState(next);
        QuantumRemaining = QuantumTicks;
        ProcessorState = "Работа";
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
        "вариант 27: один ЦПр, относительнвые приоритеты" + Environment.NewLine +
        Environment.NewLine +
        "НАЗНАЧЕНИЕ" + Environment.NewLine +
        $"  ЦПр выдаёт активному процессу квант {DefaultQuantumTicks} тактов. По окончании кванта" + Environment.NewLine +
        "  выбирается готовый процесс с наибольшим приоритетом; равные обслуживаются по FIFO." + Environment.NewLine +
        "  PC сохраняется при переключении и восстанавливается при назначении процесса." + Environment.NewLine +
        Environment.NewLine +
        "КОМАНДЫ ОПЕРАТОРА" + Environment.NewLine +
        "  ?       вывести эту справку" + Environment.NewLine +
        "  +       ускорить моделирование на 10 %" + Environment.NewLine +
        "  −       замедлить моделирование на 10 %" + Environment.NewLine +
        "  выход   завершить моделирование" + Environment.NewLine +
        Environment.NewLine +
        "ОКНО МОДЕЛИ" + Environment.NewLine +
        "  Кнопки  четыре директивы оператора" + Environment.NewLine +
        "  PC – счётчик команд текущего активного процесса; Speed – заданная скорость" + Environment.NewLine +
        "  Факт – число исполненных команд модели за время текущего замера" + Environment.NewLine +
        "  Таблица PSW – задания, счётчики, состояния" + Environment.NewLine +
        "  Состояния PSW: отсутствует, загружается, активен, готов, ввод-вывод," + Environment.NewLine +
        "  блокировка памяти/ввода-вывода, приостановлен." + Environment.NewLine +
        "  Память и буфер – результат начальной загрузки." + Environment.NewLine +
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
