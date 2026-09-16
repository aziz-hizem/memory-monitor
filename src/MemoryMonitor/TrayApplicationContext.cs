using System.Diagnostics;

namespace MemoryMonitor;

/// <summary>
/// Owns the tray icon and the sampling timer. Uses a WinForms Timer,
/// which is driven by WM_TIMER messages on the existing UI thread - no
/// extra thread, no busy loop. When paused the timer is stopped outright
/// so the process does nothing at all until resumed.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly int[] IntervalOptionsSeconds = { 5, 10, 30, 60 };

    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly SampleLogger _logger = new();
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem[] _intervalItems;

    private int _intervalSeconds = 10;
    private bool _paused;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();

        _pauseItem = new ToolStripMenuItem("Pause logging (gaming / heavy work)", null, OnTogglePause);
        menu.Items.Add(_pauseItem);

        var intervalMenu = new ToolStripMenuItem("Sample interval");
        _intervalItems = IntervalOptionsSeconds
            .Select(seconds =>
            {
                var item = new ToolStripMenuItem($"{seconds} seconds") { CheckOnClick = false };
                item.Click += (_, _) => SetInterval(seconds);
                intervalMenu.DropDownItems.Add(item);
                return item;
            })
            .ToArray();
        menu.Items.Add(intervalMenu);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("View stats...", null, OnViewStats));
        menu.Items.Add(new ToolStripMenuItem("Open log folder", null, OnOpenLogFolder));

        menu.Items.Add(new ToolStripSeparator());

        _startupItem = new ToolStripMenuItem("Start with Windows", null, OnToggleStartup)
        {
            Checked = StartupManager.IsEnabled(),
        };
        menu.Items.Add(_startupItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExit));

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Memory Monitor - starting...",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _timer = new System.Windows.Forms.Timer { Interval = _intervalSeconds * 1000 };
        _timer.Tick += OnTick;
        UpdateIntervalChecks();

        // Sample immediately so the tray icon shows real data right away,
        // then let the timer take over.
        OnTick(this, EventArgs.Empty);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        NativeMemory.Snapshot snapshot = NativeMemory.GetSnapshot();

        _trayIcon.Text = $"RAM: {snapshot.UsedMB:F0} MB / {snapshot.TotalMB:F0} MB ({snapshot.PercentUsed}%)";

        if (!_paused)
        {
            _logger.Append(snapshot);
        }
    }

    private void OnTogglePause(object? sender, EventArgs e)
    {
        _paused = !_paused;
        _pauseItem.Checked = _paused;

        // Stop the timer entirely while paused: zero CPU wake-ups, not
        // just skipped logging.
        if (_paused)
        {
            _timer.Stop();
            _trayIcon.Text = "Memory Monitor - paused";
        }
        else
        {
            _timer.Start();
        }
    }

    private void SetInterval(int seconds)
    {
        _intervalSeconds = seconds;
        _timer.Interval = seconds * 1000;
        UpdateIntervalChecks();
    }

    private void UpdateIntervalChecks()
    {
        foreach (ToolStripMenuItem item in _intervalItems)
        {
            item.Checked = item.Text == $"{_intervalSeconds} seconds";
        }
    }

    private void OnViewStats(object? sender, EventArgs e)
    {
        SampleLogger.Stats stats;
        try
        {
            stats = _logger.ComputeStats();
        }
        catch (IOException ex)
        {
            MessageBox.Show($"Could not read the log file:\n{ex.Message}", "Memory Monitor",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var form = new StatsForm(stats, _logger.LogFilePath);
        form.ShowDialog();
    }

    private void OnOpenLogFolder(object? sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _logger.LogDirectory,
            UseShellExecute = true,
        });
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        bool enable = !StartupManager.IsEnabled();
        StartupManager.SetEnabled(enable);
        _startupItem.Checked = enable;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _timer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }
}
