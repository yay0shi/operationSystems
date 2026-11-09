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

        Application.Run(new MainForm(new OsModel(configuration)));
    }
}
