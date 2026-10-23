using System.Diagnostics;
using ModelOS.Model;
using ModelOS.Presentation;

namespace ModelOS;

public sealed class MainForm : Form
{
    private readonly OsModel _model;
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

    private readonly Dictionary<string, Label> _values = new();

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

    public MainForm(ModelParameters? parameters = null)
    {
        _model = new OsModel(parameters);
        Text = "Модель ОС";
        ClientSize = new Size(1100, 960);
        MinimumSize = new Size(1000, 900);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6
        };
        foreach (int height in new[] { 38, 68, 400, 74 })
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        _btnFaster.Text = "+ Ускорить";
        _btnSlower.Text = "− Замедлить";
        _btnHelp.Text = "Справка";
        buttons.Controls.AddRange(new Control[] { _btnFaster, _btnSlower, _btnHelp, _btnFinish });
        root.Controls.Add(buttons, 0, 0);

        var indicators = CreateColumns();
        indicators.Controls.Add(CreateCard("Время модели", ("time", "Текущий такт")), 0, 0);
        indicators.Controls.Add(CreateCard("Заданная скорость", ("speed", "Тактов в секунду")), 1, 0);
        indicators.Controls.Add(CreateCard("Измеренная скорость", ("actualSpeed", "С простоем")), 2, 0);
        root.Controls.Add(indicators, 0, 1);

        var summary = CreateColumns();
        summary.RowCount = 2;
        summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 225));
        summary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        summary.Controls.Add(CreateCard("Процессор (ЦПр)",
            ("processor", "Состояние"), ("assigned", "Назначен"), ("pc", "Счётчик команд PC"),
            ("quantum", "Остаток кванта"), ("cpuTick", "Последний такт")), 0, 0);
        summary.Controls.Add(CreateCard("Последняя выполненная команда",
            ("commandOwner", "Исполнитель"), ("commandType", "Операция"),
            ("commandTime", "Когда выполнена"), ("commandDetail", "Действие")), 1, 0);
        summary.Controls.Add(CreateCard("Статистика выполнения",
            ("completed", "Завершено заданий"), ("commands", "Выполнено команд"),
            ("ioRequests", "Запросов I/O"), ("ioCompleted", "Прерываний I/O"),
            ("timerInterrupts", "Прерываний таймера")), 2, 0);
        summary.Controls.Add(CreateCard("Оперативная память",
            ("memoryTotal", "Всего"), ("memoryUsed", "Занято"), ("memoryFree", "Свободно")), 0, 1);
        summary.Controls.Add(CreateCard("Буфер следующего задания",
            ("buffer", "Задание"), ("bufferSize", "Объём"), ("loading", "Загрузка")), 1, 1);
        summary.Controls.Add(CreateCard("Параметры модели",
            ("algorithm", "Алгоритм"), ("ioParameters", "I/O"),
            ("processCount", "Процессы")), 2, 1);
        root.Controls.Add(summary, 0, 2);
        root.Controls.Add(CreateCard("Последнее прерывание", ("lastInterrupt", "Событие")), 0, 3);

        _grid.Dock = DockStyle.Fill;
        _grid.ColumnCount = 8;
        _grid.RowTemplate.Height = 19;
        _grid.ColumnHeadersHeight = 32;
        string[] headers = { "Строка", "Задание", "Память, ячеек", "Команд", "PC процесса", "Приоритет", "Состояние", "I/O, тактов" };
        for (int i = 0; i < headers.Length; i++) _grid.Columns[i].Name = headers[i];
        _grid.Columns[0].FillWeight = 50;
        _grid.Columns[0].MinimumWidth = 65;
        _grid.Columns[6].FillWeight = 175;
        _grid.RowCount = OsModel.PswCapacity;
        root.Controls.Add(_grid, 0, 4);

        var console = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        console.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        console.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        console.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        console.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        console.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        console.Controls.Add(new Label { Text = "Команда оператора", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _cmd.Dock = DockStyle.Fill;
        _cmd.PlaceholderText = "?, +, −, выход";
        console.Controls.Add(_cmd, 1, 0);
        console.Controls.Add(_btnRun, 2, 0);
        _log.Dock = DockStyle.Fill;
        console.Controls.Add(_log, 0, 1);
        console.SetColumnSpan(_log, 3);
        root.Controls.Add(console, 0, 5);
        Controls.Add(root);
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
        string action = dir switch
        {
            Directive.SpeedUp => "ускорение", Directive.SlowDown => "замедление",
            Directive.Help => "справка", Directive.Finish => "выход", _ => "неизвестная команда"
        };
        Log("> " + text + " → " + action);
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
            "-" or "−" or "замедлить" => Directive.SlowDown,
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
        PumpModel();
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

    private static TableLayoutPanel CreateColumns()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        return panel;
    }

    private GroupBox CreateCard(string title, params (string Key, string Caption)[] fields)
    {
        var card = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8) };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = fields.Length };
        float captionPercent = title == "Статистика выполнения" ? 65
            : title == "Параметры модели" ? 33 : title == "Последнее прерывание" ? 12 : 46;
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, captionPercent));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 - captionPercent));
        for (int i = 0; i < fields.Length; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var caption = new Label { Text = fields[i].Caption, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 3, 5, 3) };
            var value = new Label { AutoSize = true, Margin = new Padding(0, 3, 0, 3) };
            _values.Add(fields[i].Key, value);
            table.Controls.Add(caption, 0, i);
            table.Controls.Add(value, 1, i);
            table.SizeChanged += (_, _) =>
            {
                caption.MaximumSize = new Size(Math.Max(1, (int)(table.ClientSize.Width * captionPercent / 100) - 5), 0);
                value.MaximumSize = new Size(Math.Max(1, (int)(table.ClientSize.Width * (100 - captionPercent) / 100)), 0);
            };
        }
        card.Controls.Add(table);
        return card;
    }

    private void ShowParams()
    {
        var display = ModelDisplay.Create(_model,
            (_clock.Elapsed - _measurementStartTime).TotalSeconds,
            _model.TotalTicks - _measurementStartTicks);
        foreach (var pair in display.Values)
            if (_values[pair.Key].Text != pair.Value) _values[pair.Key].Text = pair.Value;
        for (int i = 0; i < display.ProcessRows.Count; i++)
            _grid.Rows[i].SetValues(display.ProcessRows[i]);
    }
}
