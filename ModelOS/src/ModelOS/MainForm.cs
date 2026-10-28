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
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        AllowUserToResizeRows = false,
    };

    public MainForm(ModelParameters? parameters = null)
    {
        _model = new OsModel(parameters);
        Text = "Модель ОС";
        ClientSize = new Size(1280, 850);
        MinimumSize = new Size(1060, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        ShowStaticInfo();
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
        var card = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(6) };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = fields.Length };
        int captionWidth = title is "События" or "Средние значения" ? 160 : 115;
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, captionWidth));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < fields.Length; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var caption = new Label { Text = fields[i].Caption, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 2, 8, 2) };
            var value = new Label { AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
            _values.Add(fields[i].Key, value);
            table.Controls.Add(caption, 0, i);
            table.Controls.Add(value, 1, i);
            table.SizeChanged += (_, _) =>
            {
                caption.MaximumSize = new Size(captionWidth - 10, 0);
                value.MaximumSize = new Size(Math.Max(1, table.ClientSize.Width - captionWidth), 0);
            };
        }
        card.Controls.Add(table);
        return card;
    }

    /// <summary>Постоянные области окна: управление, сводка, таблица и подробности.</summary>
    private void ShowStaticInfo()
    {
        BackColor = Color.FromArgb(245, 247, 250);
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 6
        };
        foreach (int height in new[] { 42, 58, 166, 38 })
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 186));

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        _btnFaster.Text = "+ Ускорить"; _btnSlower.Text = "− Замедлить"; _btnHelp.Text = "Справка";
        buttons.Controls.AddRange(new Control[] { _btnFaster, _btnSlower, _btnHelp, _btnFinish });
        toolbar.Controls.Add(buttons, 0, 0);
        _cmd.Dock = DockStyle.Fill; _cmd.Margin = new Padding(18, 4, 8, 4);
        _cmd.PlaceholderText = "Команда: ?, +, −, выход";
        _btnRun.Dock = DockStyle.Top;
        toolbar.Controls.Add(_cmd, 1, 0); toolbar.Controls.Add(_btnRun, 2, 0);
        root.Controls.Add(toolbar, 0, 0);

        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        (string Key, string Caption)[] metricFields =
        {
            ("time", "Модельный такт"), ("speed", "Заданная скорость"),
            ("actualSpeed", "Фактическая скорость"), ("utilization", "Занятость ЦПр")
        };
        for (int i = 0; i < metricFields.Length; i++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = metricFields[i].Caption, AutoSize = true,
                ForeColor = Color.DimGray }, 0, 0);
            var value = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), Dock = DockStyle.Fill };
            _values.Add(metricFields[i].Key, value); panel.Controls.Add(value, 0, 1);
            metrics.Controls.Add(panel, i, 0);
        }
        root.Controls.Add(metrics, 0, 1);

        var summary = CreateColumns();
        summary.Controls.Add(CreateCard("Система",
            ("memorySummary", "Память"), ("processCount", "Процессы"), ("queues", "Состояния")), 0, 0);
        summary.Controls.Add(CreateCard("Центральный процессор",
            ("processor", "Состояние"), ("assigned", "Назначен"), ("pc", "PC"),
            ("quantum", "Квант"), ("cpuTick", "За такт")), 1, 0);
        summary.Controls.Add(CreateCard("Следующее задание",
            ("buffer", "Номер"), ("bufferSize", "Объём"), ("bufferPriority", "Приоритет"),
            ("loading", "Загрузка")), 2, 0);
        root.Controls.Add(summary, 0, 2);

        var history = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        history.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        history.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        history.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        history.Controls.Add(new Label { Text = "История исполнителей", AutoSize = true,
            ForeColor = Color.DimGray, Anchor = AnchorStyles.Left }, 0, 0);
        var historyValue = new Label { AutoEllipsis = true, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        _values.Add("history", historyValue); history.Controls.Add(historyValue, 1, 0);
        root.Controls.Add(history, 0, 3);

        _grid.Dock = DockStyle.Fill;
        _grid.Font = new Font("Segoe UI", 9);
        _grid.ColumnCount = 12;
        _grid.RowTemplate.Height = 28;
        _grid.ColumnHeadersHeight = 36;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ScrollBars = ScrollBars.Both;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 237, 243);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(45, 55, 70);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = _grid.ColumnHeadersDefaultCellStyle.BackColor;
        _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = _grid.ColumnHeadersDefaultCellStyle.ForeColor;
        _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        _grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 233, 248);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        _grid.GridColor = Color.FromArgb(222, 227, 233);
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        string[] headers = { "Строка", "Задание", "Память", "Команд", "PC", "Приоритет", "Состояние",
            "I/O осталось", "CPU", "Готов", "I/O время", "В системе" };
        int[] widths = { 80, 85, 100, 95, 75, 100, 210, 125, 85, 100, 110, 125 };
        for (int i = 0; i < headers.Length; i++)
        {
            _grid.Columns[i].Name = headers[i];
            _grid.Columns[i].MinimumWidth = widths[i]; _grid.Columns[i].Width = widths[i];
            _grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        _grid.Columns[0].Frozen = true; _grid.Columns[1].Frozen = true;
        _grid.RowCount = OsModel.PswCapacity;
        root.Controls.Add(_grid, 0, 4);

        var details = new TabControl { Dock = DockStyle.Fill, Name = "DetailsTabs" };
        void AddDetail(string name, Control content)
        {
            var tab = new TabPage(name) { Padding = new Padding(6), BackColor = BackColor };
            content.Dock = DockStyle.Fill; tab.Controls.Add(content); details.TabPages.Add(tab);
        }
        AddDetail("События", CreateCard("События", ("commandSummary", "Последняя команда"),
            ("lastInterrupt", "Прерывание")));
        AddDetail("Средние значения", CreateCard("Средние значения", ("sample", "Выборка"),
            ("avgInput", "Входные"), ("avgOutput", "Выходные, такты")));
        var parameters = new TableLayoutPanel { ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++) parameters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        parameters.Controls.Add(CreateCard("Планирование", ("algorithm", "Алгоритм"),
            ("priorityLevels", "Приоритетов"), ("ioParameters", "Ввод-вывод")), 0, 0);
        parameters.Controls.Add(CreateCard("Ресурсы и выполнение", ("freeSlots", "PSW свободно"),
            ("completed", "Завершено"), ("commands", "Команд")), 1, 0);
        parameters.Controls.Add(CreateCard("Прерывания и завершения", ("interruptCounts", "I/O / таймер"),
            ("ioRequests", "Запросов I/O"), ("throughput", "Заверш./такт")), 2, 0);
        AddDetail("Параметры и счётчики", parameters);
        AddDetail("Журнал оператора", _log);
        root.Controls.Add(details, 0, 5);
        Controls.Add(root);
    }

    private void ShowValues(ModelDisplay display, params string[] keys)
    {
        foreach (string key in keys)
            if (_values[key].Text != display.Values[key]) _values[key].Text = display.Values[key];
    }

    private void ShowSystemParams(ModelDisplay d) => ShowValues(d, "time", "speed", "actualSpeed",
        "memorySummary", "processCount", "queues", "freeSlots", "algorithm", "priorityLevels",
        "completed", "commands", "interruptCounts", "ioRequests", "utilization", "throughput");

    private void ShowNewTaskParams(ModelDisplay d) => ShowValues(d,
        "buffer", "bufferSize", "bufferPriority", "ioParameters", "loading");

    private void ShowCpuParams(ModelDisplay d) => ShowValues(d,
        "processor", "assigned", "pc", "quantum", "cpuTick", "commandSummary", "lastInterrupt");

    private void ShowProcessParams(ModelDisplay display)
    {
        for (int row = 0; row < display.ProcessRows.Count; row++)
            for (int col = 0; col < display.ProcessRows[row].Length; col++)
                if (!Equals(_grid[col, row].Value, display.ProcessRows[row][col]))
                    _grid[col, row].Value = display.ProcessRows[row][col];
    }

    private void ShowExecutionHistory(ModelDisplay d) => ShowValues(d, "history");

    private void ShowAverageParams(ModelDisplay d) => ShowValues(d, "sample", "avgInput", "avgOutput");

    private void ShowParams()
    {
        var display = ModelDisplay.Create(_model,
            (_clock.Elapsed - _measurementStartTime).TotalSeconds,
            _model.TotalTicks - _measurementStartTicks);
        ShowSystemParams(display);
        ShowNewTaskParams(display);
        ShowCpuParams(display);
        ShowProcessParams(display);
        ShowExecutionHistory(display);
        ShowAverageParams(display);
    }
}
