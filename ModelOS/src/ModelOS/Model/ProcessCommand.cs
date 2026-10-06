namespace ModelOS.Model;

public enum CommandType { Compute, Io, Exit }
public enum OperationCode { Add, Subtract, Multiply, Io, Exit }

/// <summary>
/// Команда и математические адреса в памяти данных процесса
/// </summary>
public readonly record struct ProcessCommand(CommandType Type, OperationCode Operation,
    int Address1 = 0, int Address2 = 0);

/// <summary>
/// Генерация, разбор команды и операции АЛУ
/// </summary>
public sealed class CommandExecutor
{
    private readonly Random _random;

    public CommandExecutor(int? seed = null) => _random = seed.HasValue ? new Random(seed.Value) : new Random();

    public CommandType GenerateCommand(long pc, int count, int ioPercent)
    {
        if (pc >= count) return CommandType.Exit;
        return _random.Next(100) < ioPercent ? CommandType.Io : CommandType.Compute;
    }

    public ProcessCommand DecodeCommand(CommandType type, int memorySize) => type switch
    {
        CommandType.Compute => new(type, (OperationCode)_random.Next(3),
            _random.Next(memorySize), _random.Next(memorySize)),
        CommandType.Io => new(type, OperationCode.Io),
        CommandType.Exit => new(type, OperationCode.Exit),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static double ReadOperand(PswEntry process, int address) => process.OperandMemory[address];
    public static void WriteResult(PswEntry process, int address, double result) => process.OperandMemory[address] = result;

    public static double DoOperation(OperationCode operation, double first, double second)
    {
        double result = operation switch
        {
            OperationCode.Add => first + second,
            OperationCode.Subtract => first - second,
            OperationCode.Multiply => first * second,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        // Значения ограничены, чтобы длительное моделирование не давало Infinity
        return Math.Clamp(result, -1_000_000, 1_000_000);
    }
}
