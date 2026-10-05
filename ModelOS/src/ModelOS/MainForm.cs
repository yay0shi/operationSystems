using System.Diagnostics;
using ModelOS.Model;

namespace ModelOS;

public sealed class MainForm : Form
{
    private readonly OsModel _model = new();
    private readonly TickScheduler _scheduler = new();
    private readonly System.Windows.Forms.Timer _tickTimer = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastPumpTime;
    private TimeSpan _lastDisplayTime;
    private TimeSpan _measurementStartTime;
    private long _measurementStartTicks;

    private readonly Button _btnFaster = new() { Text = "+ быстрее", AutoSize = true };
    private readonly Button _btnSlower = new() { Text = "− медленнее", AutoSize = true };
    private readonly Button _btnHelp = new() { Text = "? справка", AutoSize = true };
    private readonly Button _btnFinish = new() { Text = "Выход", AutoSize = true };

    private readonly Label _lblPc = new() { AutoSize = true };
    private readonly Label _lblSpeed = new() { AutoSize = true };
    private readonly Label _lblActualSpeed = new() { AutoSize = true };
    private readonly Label _lblMemory = new() { AutoSize = true };
    private readonly Label _lblBuffer = new() { AutoSize = true };
    private readonly Label _lblProcessor = new() { AutoSize = true };
    private readonly Label _lblQuantum = new() { AutoSize = true };

    private readonly TextBox _cmd = new() { Width = 560 };
    private readonly Button _btnRun = new() { Text = "Выполнить", AutoSize = true };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 960, Height = 90 };

    private readonly DataGridView _grid = new()
    {
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        Width = 960,
        Height = 380,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AllowUserToResizeRows = false,
    };

    public MainForm()
    {
        Text = "Модель ОС";
        Width = 1100;
        Height = 1000;
        StartPosition = FormStartPosition.CenterScreen;

        var info = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoScroll = true,
            WrapContents = false,
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(960, 0), FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(_btnFaster);
        buttons.Controls.Add(_btnSlower);
        buttons.Controls.Add(_btnHelp);
        buttons.Controls.Add(_btnFinish);
        info.Controls.Add(buttons);
        var indicators = new TableLayoutPanel
        {
            Width = 960,
            Height = 28,
            ColumnCount = 3,
            RowCount = 1,
        };
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        indicators.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var label in new[] { _lblPc, _lblSpeed, _lblActualSpeed })
        {
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
        }
        indicators.Controls.Add(_lblPc, 0, 0);
        indicators.Controls.Add(_lblSpeed, 1, 0);
        indicators.Controls.Add(_lblActualSpeed, 2, 0);
        info.Controls.Add(indicators);
        info.Controls.Add(_lblProcessor);
        info.Controls.Add(_lblQuantum);
        info.Controls.Add(_lblMemory);
        info.Controls.Add(_lblBuffer);

        _grid.ColumnCount = 7;
        _grid.RowTemplate.Height = 19;
        _grid.ColumnHeadersHeight = 24;
        _grid.Columns[0].Name = "№";
        _grid.Columns[1].Name = "Task_Id";
        _grid.Columns[2].Name = "V_task";
        _grid.Columns[3].Name = "N_cmnd";
        _grid.Columns[4].Name = "PCi";
        _grid.Columns[5].Name = "Приоритет";
        _grid.Columns[6].Name = "Состояние";
        _grid.Columns[0].FillWeight = 45;
        _grid.Columns[6].FillWeight = 130;
        _grid.RowCount = OsModel.PswCapacity;
        info.Controls.Add(_grid);

        var console = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        console.Controls.Add(_cmd);
        console.Controls.Add(_btnRun);
        info.Controls.Add(console);
        info.Controls.Add(_log);
        Controls.Add(info);
        _btnFaster.Click += (_, _) => DoDirective(Directive.SpeedUp);
        _btnSlower.Click += (_, _) => DoDirective(Directive.SlowDown);
        _btnHelp.Click += (_, _) => DoDirective(Directive.Help);
        _btnFinish.Click += (_, _) => DoDirective(Directive.Finish);
        _btnRun.Click += (_, _) => RunCommand();
        _cmd.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                RunCommand();
                e.Handled = true;
            }
        };

        _tickTimer.Tick += (_, _) => PumpModel();

        Load += (_, _) =>
        {
            _model.InitModel();
            _model.LoadCycle();
            Log("Модель запущена. Команды: ?, +, -, выход.");
            _scheduler.Reset();
            _clock.Restart();
            _lastPumpTime = TimeSpan.Zero;
            _lastDisplayTime = TimeSpan.Zero;
            ResetSpeedMeasurement();
            ShowParams();
            _tickTimer.Start();
        };
    }

    private void RunCommand()
    {
        string text = _cmd.Text.Trim();
        if (text.Length == 0)
            return;
        Directive dir = ParseCommand(text);
        Log("> " + text + " –> " + dir);
        DoDirective(dir);
        _cmd.Clear();
        _cmd.Focus();
    }

    private Directive ParseCommand(string text)
    {
        return text.Trim().ToLowerInvariant() switch
        {
            "выход" or "завершить" => Directive.Finish,
            "+" or "ускорить" => Directive.SpeedUp,
            "-" or "замедлить" => Directive.SlowDown,
            "?" or "справка" => Directive.Help,
            _ => Directive.None,
        };
    }

    private void Log(string line)
    {
        _log.AppendText(line + Environment.NewLine);
        var lines = _log.Lines;
        if (lines.Length > 200)
            _log.Lines = lines[^200..];
    }

    private void DoDirective(Directive dir)
    {
        if (dir == Directive.None)
        {
            Log("Неизвестная команда. Допустимо: ?, +, -, выход.");
            return;
        }
        PumpModel(); // account for time at the old speed before changing it
        _model.DoDirective(dir);
        if (dir is Directive.SpeedUp or Directive.SlowDown)
            ResetSpeedMeasurement();
        if (dir == Directive.Help)
            ShowHelp(this);
        if (dir == Directive.Finish)
        {
            _tickTimer.Stop();
            Close();
        }
        else
            ShowParams();
    }

    private void PumpModel()
    {
        if (!_clock.IsRunning || _model.Finish)
            return;

        TimeSpan now = _clock.Elapsed;
        TimeSpan elapsed = now - _lastPumpTime;
        _lastPumpTime = now;
        
        if (elapsed > TimeSpan.FromSeconds(1))
            _scheduler.Reset();
        else
        {
            int dueTicks = _scheduler.Advance(elapsed, _model.Speed);
            for (int i = 0; i < dueTicks; i++)
                _model.DoTick();
        }

        if (now - _lastDisplayTime >= TimeSpan.FromMilliseconds(100))
        {
            ShowParams();
            _lastDisplayTime = now;
        }
    }

    private void ResetSpeedMeasurement()
    {
        _measurementStartTime = _clock.Elapsed;
        _measurementStartTicks = _model.TotalTicks;
    }

    internal static void ShowHelp(IWin32Window? owner)
    {
        using var dlg = new Form
        {
            Text = "Справка – Модель ОС",
            Width = 800,
            Height = 600,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
        };
        var text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Font = new Font(FontFamily.GenericMonospace, 10),
            Text = OsModel.ShowHelpText(),
        };
        var ok = new Button { Text = "Закрыть", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
        dlg.Controls.Add(text);
        dlg.Controls.Add(ok);
        dlg.AcceptButton = ok;
        dlg.ShowDialog(owner);
    }

    private void ShowParams()
    {
        _lblPc.Text = $"PC: {_model.Pc}";
        _lblSpeed.Text = $"Speed: {_model.Speed:0.###} такт/с";
        double measuredSeconds = (_clock.Elapsed - _measurementStartTime).TotalSeconds;
        double measuredSpeed = measuredSeconds > 0 ? (_model.TotalTicks - _measurementStartTicks) / measuredSeconds : 0;
        _lblActualSpeed.Text = $"Факт: {measuredSpeed:0.##} такт/с ({measuredSeconds:0.#} с)";
        _lblProcessor.Text = $"ЦПр: процесс {(_model.CurrentProcessIndex < 0 ? "не назначен" : _model.CurrentProcessIndex.ToString())}; состояние: {_model.ProcessorState}";
        _lblQuantum.Text = $"Квант: {_model.QuantumTicks}; осталось: {_model.QuantumRemaining}";
        _lblMemory.Text = $"Память: всего {_model.MemSize}; занято {_model.MemUsed}; свободно {_model.FreeMemory}; процессов {_model.ProcCount}";
        _lblBuffer.Text = $"Буфер: задание {_model.PswTask.TaskId}; память {_model.PswTask.TaskSize}; команд {_model.PswTask.CommandCount}; загрузка {(_model.CheckFreeMem() ? "возможна" : "невозможна")}";
        for (int i = 0; i < OsModel.PswCapacity; i++)
        {
            var p = _model.Psw[i];
            _grid.Rows[i].SetValues(i, p.TaskId, p.TaskSize, p.CommandCount, p.ProcessPc, p.Priority, p.State.ToDisplay());
        }

    }
}
