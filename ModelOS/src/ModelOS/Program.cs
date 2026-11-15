using ModelOS.Model;

namespace ModelOS;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Any(a => a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            MainForm.ShowHelp(null);
            return;
        }

        ApplicationConfiguration.Initialize();
        int? seed = null;
        if (args.Length != 0)
        {
            if (args.Length != 2 || !args[0].Equals("--seed", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(args[1], out int value))
            {
                MessageBox.Show("Формат запуска: ModelOS.exe --seed <целое Int32>", "Ошибка аргументов",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            seed = value;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "parameters.txt");
        ParameterFile configuration;
        try
        {
            configuration = ParameterFile.Read(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            MessageBox.Show($"{path}\n{ex.Message}", "Ошибка файла параметров",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm(new OsModel(configuration, seed)));
    }
}
