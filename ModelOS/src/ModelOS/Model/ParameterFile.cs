namespace ModelOS.Model;

internal readonly record struct ParameterRange(int Min, int Max, int Step, string Comment)
{
    public int Last => Min + (Max - Min) / Step * Step;

    public int Generate(Random random) =>
        Min + random.Next((Max - Min) / Step + 1) * Step;
}

internal sealed class ParameterFile
{
    private static readonly (string Name, int Min, int Max)[] Fields =
    [
        ("MemorySize", 1, ushort.MaxValue),
        ("TaskSize", 1, ushort.MaxValue),
        ("TaskCommands", 1, ushort.MaxValue),
        ("QuantumTicks", 1, ushort.MaxValue),
        ("PriorityLevels", 1, ushort.MaxValue),
        ("IoCommandPercent", 0, 100),
        ("IoTicks", 1, ushort.MaxValue),
        ("LoadTicks", 0, ushort.MaxValue),
        ("DispatchTicks", 0, ushort.MaxValue),
        ("IoInitializationTicks", 0, ushort.MaxValue),
        ("IoInterruptTicks", 0, ushort.MaxValue),
        ("UnloadTicks", 0, ushort.MaxValue)
    ];

    private readonly Dictionary<string, ParameterRange> _ranges;

    private ParameterFile(Dictionary<string, ParameterRange> ranges) => _ranges = ranges;

    public static ParameterFile Read(string path)
    {
        var ranges = new Dictionary<string, ParameterRange>(StringComparer.Ordinal);
        int number = 0;
        foreach (string raw in File.ReadLines(path))
        {
            number++;
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            string[] parts = line.Split((char[]?)null, 5, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 || !ushort.TryParse(parts[1], out ushort min) ||
                !ushort.TryParse(parts[2], out ushort max) ||
                !ushort.TryParse(parts[3], out ushort step) || min > max || step == 0)
                throw new FormatException($"Строка {number}: ожидаются имя, минимум, максимум, положительный шаг и комментарий.");

            var field = Array.Find(Fields, f => f.Name == parts[0]);
            if (field.Name is null)
                throw new FormatException($"Строка {number}: неизвестный параметр {parts[0]}.");
            if (min < field.Min || max > field.Max)
                throw new FormatException($"Строка {number}: диапазон {field.Name} должен лежать в пределах {field.Min}–{field.Max}.");
            if (!ranges.TryAdd(field.Name, new(min, max, step, parts[4])))
                throw new FormatException($"Строка {number}: повтор параметра {field.Name}.");
        }

        foreach (var field in Fields)
            if (!ranges.ContainsKey(field.Name))
                throw new FormatException($"Отсутствует параметр {field.Name}.");
        if (ranges["TaskSize"].Last > ranges["MemorySize"].Min)
            throw new FormatException("Наибольший возможный размер задания превышает минимальный объем памяти.");
        return new(ranges);
    }

    public ModelParameters GenerateSystemParameters(Random random) => new(
        MemorySize: Generate("MemorySize", random),
        QuantumTicks: Generate("QuantumTicks", random),
        PriorityLevels: Generate("PriorityLevels", random),
        IoCommandPercent: Generate("IoCommandPercent", random),
        IoTicks: Generate("IoTicks", random),
        LoadTicks: Generate("LoadTicks", random),
        DispatchTicks: Generate("DispatchTicks", random),
        IoInitializationTicks: Generate("IoInitializationTicks", random),
        IoInterruptTicks: Generate("IoInterruptTicks", random),
        UnloadTicks: Generate("UnloadTicks", random));

    public int Generate(string name, Random random) => _ranges[name].Generate(random);
}
