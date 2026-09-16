namespace MemoryMonitor;

/// <summary>
/// A tiny, disposable window that shows aggregate stats and is closed
/// immediately after — it does not stay resident, so it costs nothing
/// while the tray icon sits idle.
/// </summary>
internal sealed class StatsForm : Form
{
    public StatsForm(SampleLogger.Stats stats, string logPath)
    {
        Text = "Memory Monitor - Stats";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 180);

        var label = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            Text = stats.SampleCount == 0
                ? "No samples logged yet."
                : $"Samples logged: {stats.SampleCount}\n\n" +
                  $"Minimum RAM used: {stats.MinMB:F0} MB\n" +
                  $"Average RAM used: {stats.AvgMB:F0} MB\n" +
                  $"Maximum RAM used: {stats.MaxMB:F0} MB\n\n" +
                  $"Log file:\n{logPath}",
        };

        Controls.Add(label);
    }
}
