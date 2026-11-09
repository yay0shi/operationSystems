namespace ModelOS.Model;

public sealed class OsModel
{
    private ModelParameters _parameters;
    private readonly ParameterFile? _configuration;
    private const int ParameterSeed = 27;
    private const int CommandSeed = 28;
    private Random? _parameterRandom;
    private CommandExecutor _commands;
    private readonly Func<int, long, CommandType>? _commandSource;
    private readonly Queue<SystemWork> _systemQueue = new();
    private SystemWork? _system;
    private int _loadedTasks;
    public long CpuSystemTicks { get; private set; }
    public long CpuIdleTicks { get; private set; }
    public bool CpuSystemThisTick { get; private set; }
    public string LastSystemOperation { get; private set; } = "Нет";
    public string SystemOperation => _system is null ? "Нет" : $"{_system.Name}: {_system.Remaining} такт.";
    public ModelParameters Parameters => _parameters;
    public bool AllTasksCompleted => _parameters.TaskLimit is int limit && CompletedTaskCount == limit;
    private readonly Queue<ExecutionEntry> _executionHistory = new();
    public const int HistoryCapacity = 16;
    public IReadOnlyList<ExecutionEntry> ExecutionHistory => _executionHistory.ToArray();
    public CompletedStatistics Statistics { get; } = new();
    public int PriorityLevels => _parameters.PriorityLevels;

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
    /// Аппаратный счетчик команд
    /// </summary>
    public long Pc { get; private set; }

    /// <summary>
    /// Общее число модельных тактов, включая простой ЦПр
    /// </summary>
    public long TotalTicks { get; private set; }
    public long ExecutedCommandCount { get; private set; }
    public long CompletedTaskCount { get; private set; }
    public long IoRequestCount { get; private set; }
    public long IoCompletedCount { get; private set; }
    public long TimerInterruptCount { get; private set; }
    public string LastInterruptText { get; private set; } = "Прерываний еще нет";
    public bool CpuExecutedThisTick { get; private set; }
    public string LastCommandText { get; private set; } = "Нет выполненной команды";
    public ProcessCommand? LastCommand { get; private set; }
    public int LastCommandTaskId { get; private set; }
    public long LastCommandTick { get; private set; }
    public int IoCommandPercent => _parameters.IoCommandPercent;
    public int IoTicks => _parameters.IoTicks;

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
    /// Объем памяти модели
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

    public OsModel(ModelParameters? parameters = null, Func<int, long, CommandType>? commandSource = null)
    {
        _parameters = parameters ?? new ModelParameters();
        _parameters.Validate();
        _commands = new CommandExecutor();
        _commandSource = commandSource;
        for (int i = 0; i < Psw.Length; i++)
            Psw[i] = new PswEntry();
    }

    internal OsModel(ParameterFile configuration) : this()
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Свободная память: V_free = V_озу − V_занято
    /// </summary>
    public int FreeMemory => MemSize - MemUsed;

    public void InitModel()
    {
        if (_configuration is not null)
        {
            _parameterRandom = new Random(ParameterSeed);
            _parameters = _configuration.GenerateSystemParameters(_parameterRandom);
            _parameters.Validate();
            _commands = new CommandExecutor(CommandSeed);
        }

        Pc = 0;
        TotalTicks = 0;
        CpuSystemTicks = CpuIdleTicks = 0;
        CpuSystemThisTick = false;
        LastSystemOperation = "Нет";
        _system = null;
        _systemQueue.Clear();
        _loadedTasks = 0;
        _executionHistory.Clear();
        Statistics.Clear();
        ExecutedCommandCount = 0;
        CompletedTaskCount = 0;
        IoRequestCount = 0;
        IoCompletedCount = 0;
        TimerInterruptCount = 0;
        LastInterruptText = "Прерываний еще нет";
        CpuExecutedThisTick = false;
        LastCommand = null;
        LastCommandTaskId = 0;
        LastCommandTick = 0;
        LastCommandText = "Нет выполненной команды";
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
        PswTask.TaskSize = _configuration?.Generate("TaskSize", _parameterRandom!) ?? _parameters.TaskSize;
        PswTask.CommandCount = _configuration?.Generate("TaskCommands", _parameterRandom!) ?? _parameters.TaskCommands;
        // приоритеты распределяются циклически от 1 до PriorityLevels
        PswTask.Priority = ((_taskSeq - 1) % _parameters.PriorityLevels) + 1;
        PswTask.State = ProcState.Absent; // Буфер не является резидентным процессом.
        PswTask.ProcessPc = 0;
        PswTask.ReadyOrder = 0;
        PswTask.IoTicksRemaining = 0;
        PswTask.IoStartedTick = 0;
        PswTask.OperandMemory = [];
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
        if (_parameters.TaskLimit is int limit && _loadedTasks >= limit) return false;
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
        Psw[i].OperandMemory = Enumerable.Repeat(1.0, Psw[i].TaskSize).ToArray();
        Psw[i].LoadedAtTick = TotalTicks;
        Psw[i].CpuTicks = Psw[i].ReadyTicks = Psw[i].IoBlockedTicks = 0;
        Psw[i].MonoTicks = Psw[i].SystemOutsideReadyTicks = 0;
        Psw[i].State = ProcState.Loading;
        MemUsed += Psw[i].TaskSize;
        ProcCount++;
        _loadedTasks++;
        EnqueueSystem("Загрузка", _parameters.LoadTicks, i, () => Process(i, ProcessEvent.Loaded));
        GenNewTask();
        return true;
    }

    public void LoadCycle()
    {
        if (_system is not null || _systemQueue.Count > 0) return;
        while (CheckFreeMem())
        {
            LoadTask();
            if (_system is not null || _systemQueue.Count > 0) return;
        }
        if (CurrentProcessIndex < 0) DispatchNextProcess();
    }

    private void EnqueueSystem(string name, int ticks, int owner, Action complete)
    {
        if (ticks == 0) { complete(); return; }
        _systemQueue.Enqueue(new(name, ticks, owner, complete));
        StartNextSystemOperation();
    }

    private void StartNextSystemOperation()
    {
        if (_system is null && _systemQueue.Count > 0) _system = _systemQueue.Dequeue();
    }

    public void DoTick()
    {
        CheckSystemState();
        StartNextSystemOperation();
        TotalTicks++;
        CpuSystemThisTick = _system is not null;
        CpuExecutedThisTick = !CpuSystemThisTick && CurrentProcessIndex >= 0;
        AccumulateProcessTimes();
        if (CpuSystemThisTick)
        {
            CpuSystemTicks++;
            LastSystemOperation = _system!.Name;
            if (--_system.Remaining == 0)
            {
                var finished = _system;
                _system = null;
                finished.Complete();
            }
        }
        else if (CpuExecutedThisTick)
        {
            RecordExecution();
            ExecuteProcessCommand();
        }
        else CpuIdleTicks++;
        AdvanceIo();
        if (CpuExecutedThisTick) HandleTimerInterrupt();
        CheckSystemState();
    }

    /// <summary>Начисление Ready выполняется ровно один раз за модельный интервал.</summary>
    public void IncreaseReadyProcessTime(long ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        foreach (var process in Psw)
            if (process.State == ProcState.Ready) process.ReadyTicks += ticks;
    }

    private void AccumulateProcessTimes()
    {
        IncreaseReadyProcessTime(1);
        foreach (var process in Psw)
        {
            if (process.IsFree || process.State == ProcState.Ready) continue;
            if (process.State == ProcState.BlockedByIo)
            {
                process.IoBlockedTicks++;
                process.MonoTicks++;
            }
            else if (process.State == ProcState.Running && CpuExecutedThisTick)
            {
                process.CpuTicks++;
                process.MonoTicks++;
            }
            else
            {
                process.SystemOutsideReadyTicks++;
                if (process.State is ProcState.Loading or ProcState.Finishing) process.MonoTicks++;
            }
        }
    }

    private void RecordExecution()
    {
        if (_executionHistory.Count == HistoryCapacity) _executionHistory.Dequeue();
        _executionHistory.Enqueue(new(TotalTicks, Psw[CurrentProcessIndex].TaskId, CurrentProcessIndex));
    }

    /// <summary>
    /// Выборка, увеличение PC, декодирование, чтение, операция и запись
    /// </summary>
    private void ExecuteProcessCommand()
    {
        PswEntry process = Psw[CurrentProcessIndex];
        CommandType type = _commandSource?.Invoke(process.TaskId, Pc)
            ?? _commands.GenerateCommand(Pc, process.CommandCount, IoCommandPercent);
        Pc++; // Длина любой команды = 1
        process.ProcessPc = Pc;
        ProcessCommand command = _commands.DecodeCommand(type, process.TaskSize);
        LastCommand = command;
        LastCommandTaskId = process.TaskId;
        LastCommandTick = TotalTicks;
        ExecutedCommandCount++;
        QuantumRemaining--;
        ExecuteOperation(process, command);
    }

    /// <summary>
    /// Выполнение распознанной операции: АЛУ, системная инициализация I/O или выгрузка
    /// </summary>
    private void ExecuteOperation(PswEntry process, ProcessCommand command)
    {
        switch (command.Operation)
        {
            case OperationCode.Add:
            case OperationCode.Subtract:
            case OperationCode.Multiply:
                ExecuteComputation(process, command);
                break;
            case OperationCode.Io:
                InitializeIo();
                break;
            case OperationCode.Exit:
                FinishProcess();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    private void ExecuteComputation(PswEntry process, ProcessCommand command)
    {
        double first = CommandExecutor.ReadOperand(process, command.Address1);
        double second = CommandExecutor.ReadOperand(process, command.Address2);
        double result = CommandExecutor.DoOperation(command.Operation, first, second);
        CommandExecutor.WriteResult(process, command.Address2, result);
        string op = command.Operation switch { OperationCode.Add => "+", OperationCode.Subtract => "−", _ => "×" };
        LastCommandText = $"Задание {process.TaskId}: вычисление [{command.Address1}] {op} [{command.Address2}] = {result:0.###}";
    }

    private void InitializeIo()
    {
        PswEntry process = Psw[CurrentProcessIndex];
        LastCommandText = $"Задание {process.TaskId}: ввод-вывод, длительность {IoTicks} тактов";
        Process(CurrentProcessIndex, ProcessEvent.IoRequested);
    }

    /// <summary>
    /// I/O выполняется независимо от занятости ЦПр, начиная со следующего такта после запроса
    /// </summary>
    private void AdvanceIo()
    {
        for (int i = 0; i < Psw.Length; i++)
        {
            PswEntry process = Psw[i];
            if (process.State != ProcState.BlockedByIo || process.IoStartedTick >= TotalTicks) continue;
            process.IoTicksRemaining--;
            if (process.IoTicksRemaining != 0) continue;
            HandleIoInterrupt(i);
        }
    }

    private void HandleIoInterrupt(int processIndex)
    {
        int taskId = Psw[processIndex].TaskId;
        Psw[processIndex].State = ProcState.IoCompleted;
        EnqueueSystem("Прерывание I/O", _parameters.IoInterruptTicks, processIndex, () =>
        {
            Process(processIndex, ProcessEvent.IoCompleted);
            IoCompletedCount++;
            LastInterruptText = $"Такт {TotalTicks}: завершение I/O задания {taskId}";
        });
    }

    private void HandleTimerInterrupt()
    {
        if (CurrentProcessIndex < 0 || QuantumRemaining != 0) return;
        int taskId = Psw[CurrentProcessIndex].TaskId;
        Process(CurrentProcessIndex, ProcessEvent.QuantumExpired);
        TimerInterruptCount++;
        LastInterruptText = $"Такт {TotalTicks}: окончание кванта задания {taskId}";
    }

    /// <summary>После обработки событий загрузить допустимые задания и назначить свободный ЦПр.</summary>
    public void CheckSystemState()
    {
        StartNextSystemOperation();
        if (_system is null) LoadCycle();
    }

    private void FinishProcess()
    {
        PswEntry process = Psw[CurrentProcessIndex];
        LastCommandText = $"Задание {process.TaskId}: завершение; освобождено {process.TaskSize} единиц памяти";
        Process(CurrentProcessIndex, ProcessEvent.Finished);
    }

    /// <summary>Единственная процедура переходов резидентных процессов во время работы.</summary>
    public void Process(int processIndex, ProcessEvent reason)
    {
        if ((uint)processIndex >= Psw.Length)
            throw new ArgumentOutOfRangeException(nameof(processIndex));
        PswEntry process = Psw[processIndex];
        bool active = process.State == ProcState.Running && CurrentProcessIndex == processIndex;
        bool allowed = reason switch
        {
            ProcessEvent.Loaded => process.State == ProcState.Loading && process.TaskId > 0,
            ProcessEvent.Assigned => process.State == ProcState.Ready && CurrentProcessIndex < 0,
            ProcessEvent.QuantumExpired => active && QuantumRemaining == 0,
            ProcessEvent.IoRequested => active,
            ProcessEvent.IoCompleted => process.State == ProcState.IoCompleted && process.IoTicksRemaining == 0,
            ProcessEvent.Finished => active,
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException($"Недопустимое событие {reason} для состояния {process.State}");

        switch (reason)
        {
            case ProcessEvent.Loaded:
                MakeReady(process);
                break;
            case ProcessEvent.Assigned:
                process.State = ProcState.Running;
                CurrentProcessIndex = processIndex;
                RestoreProcessState(processIndex);
                QuantumRemaining = QuantumTicks;
                ProcessorState = "Работа";
                break;
            case ProcessEvent.QuantumExpired:
                SaveProcessState(processIndex);
                MakeReady(process);
                ReleaseProcessor();
                break;
            case ProcessEvent.IoRequested:
                SaveProcessState(processIndex);
                process.State = ProcState.IoInitializing;
                IoRequestCount++;
                ReleaseProcessor();
                EnqueueSystem("Инициализация I/O", _parameters.IoInitializationTicks, processIndex, () =>
                {
                    process.State = ProcState.BlockedByIo;
                    process.IoTicksRemaining = IoTicks;
                    process.IoStartedTick = TotalTicks;
                });
                break;
            case ProcessEvent.IoCompleted:
                MakeReady(process);
                break;
            case ProcessEvent.Finished:
                process.State = ProcState.Finishing;
                ReleaseProcessor();
                EnqueueSystem("Выгрузка", _parameters.UnloadTicks, processIndex, () =>
                {
                    Statistics.Add(process, TotalTicks);
                    MemUsed -= process.TaskSize;
                    ProcCount--;
                    CompletedTaskCount++;
                    process.Clear();
                });
                break;
        }
    }

    // Вызывается только регулировщиком.
    private void MakeReady(PswEntry process)
    {
        process.State = ProcState.Ready;
        process.ReadyOrder = ++_readySequence;
    }

    private void ReleaseProcessor()
    {
        CurrentProcessIndex = -1;
        Pc = 0;
        QuantumRemaining = 0;
        ProcessorState = "Ожидание";
    }

    /// <summary>
    /// Сохраняет аппаратный счетчик команд в слове состояния активного процесса.
    /// </summary>
    public void SaveProcessState(int processIndex)
    {
        if ((uint)processIndex >= Psw.Length || Psw[processIndex].IsFree)
            throw new ArgumentOutOfRangeException(nameof(processIndex));
        Psw[processIndex].ProcessPc = Pc;
    }

    /// <summary>
    /// Восстанавливает аппаратный счетчик команд из слова состояния процесса.
    /// </summary>
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
    /// Назначает свободный ЦПр; работающий процесс не вытесняется относительным приоритетом.
    /// </summary>
    public void DispatchNextProcess()
    {
        if (CurrentProcessIndex >= 0 || _system is not null || _systemQueue.Count > 0) return;

        int next = GetNextProcessIndex();
        if (next < 0)
        {
            ReleaseProcessor();
            return;
        }

        EnqueueSystem("Назначение процесса", _parameters.DispatchTicks, next,
            () => Process(next, ProcessEvent.Assigned));
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

    public static string ShowHelpText() => """
        Лабораторная работа 8. Вариант 27. Чтение файла параметров

        parameters.txt читается при запуске. Строка: имя минимум максимум шаг комментарий.
        Размер и число команд задания генерируются при заполнении буфера.
        Начальные значения генераторов параметров и команд фиксированы.

        Один ЦПр; относительные приоритеты; FIFO при равных приоритетах.
        Квант расходуется только на команды задания. I/O идет параллельно ЦПр.
        Системные операции занимают отдельные модельные такты, I/O продолжается.
        Очередь системных операций FIFO; начатая операция выполняется до конца.
        Порядок: системная операция или команда; I/O; таймер; загрузка/назначение.
        Загрузка имеет приоритет перед новым назначением, но ограничена ресурсами.

        T_mono: собственная загрузка, фактические команды, физический I/O, выгрузка.
        Exit занимает дополнительный такт после заданного числа обычных команд.
        T_multi: от начала загрузки до окончания завершения задания.
        T_multi = CPU + готов + I/O + системное время вне готовности.
        Системные задержки готовых уже входят в их ожидание; повторно не добавляются.
        D_exe = 100*(T_multi-T_mono)/T_mono; D_ready = 100*готов/T_multi.
        D_sys = 100*системные такты ЦПр/общее время.
        Занятость ЦПр = 100*(команды + системные такты)/общее время.
        M_mono = общее время/среднее T_mono; D_multi = 100*M_multi/M_mono.
        100% означает равную эталону производительность, 200% – вдвое выше.
        Среднее оборотное время: среднее T_multi завершенных заданий.
        Выборка: завершенные с начала запуска; до завершений средние – прочерки.
        Все времена – модельные такты; заданий/такт – абсолютная интенсивность.
        Скорость окна не меняет расчетные показатели для одинаковой трассы.

        ?, справка – помощь; +, ускорить – быстрее; -, замедлить – медленнее;
        выход, завершить – завершить работу. ModelOS.exe /? – справка и выход.
        История показывает 16 последних фактических исполнителей команд.
        В таблице все 16 строк PSW; дополнительные показатели доступны прокруткой.
        Вкладки: события, средние, расчет Lab7, параметры и журнал оператора.
        """;
}
