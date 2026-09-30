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
        Application.Run(new MainForm());
    }
}
