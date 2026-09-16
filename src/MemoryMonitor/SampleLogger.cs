using System.Globalization;
using System.Linq;

namespace MemoryMonitor;

/// <summary>
/// Appends RAM samples to a plain CSV file. At one write every few seconds
/// this is trivial I/O; no buffering or background thread needed.
/// </summary>
internal sealed class SampleLogger
{
    public string LogDirectory { get; }
    public string LogFilePath { get; }

    // Samples that could not be written because the CSV was locked (e.g.
    // open in Excel). They are written on the next successful append.
    private readonly List<string> _pendingLines = new();

    public SampleLogger(string? logDirectory = null)
    {
        LogDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MemoryMonitor");
        Directory.CreateDirectory(LogDirectory);
        LogFilePath = Path.Combine(LogDirectory, "log.csv");

        if (!File.Exists(LogFilePath))
        {
            File.WriteAllText(LogFilePath, "TimestampUtc,UsedMB,TotalMB,PercentUsed\n");
        }
    }

    public int PendingCount => _pendingLines.Count;

    public void Append(NativeMemory.Snapshot snapshot)
    {
        _pendingLines.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{0:O},{1:F0},{2:F0},{3}\n",
            DateTime.UtcNow, snapshot.UsedMB, snapshot.TotalMB, snapshot.PercentUsed));

        try
        {
            File.AppendAllText(LogFilePath, string.Concat(_pendingLines));
            _pendingLines.Clear();
        }
        catch (IOException)
        {
            // File is locked by another program; keep the samples and retry next tick.
        }
    }

    public readonly record struct Stats(double MinMB, double MaxMB, double AvgMB, int SampleCount);

    /// <exception cref="IOException">The log file is locked and cannot be read.</exception>
    public Stats ComputeStats()
    {
        if (!File.Exists(LogFilePath))
        {
            return new Stats(0, 0, 0, 0);
        }

        double min = double.MaxValue, max = double.MinValue, sum = 0;
        int count = 0;

        // Share read/write so stats still work while another program has the CSV open.
        using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        foreach (string line in ReadLines(reader).Skip(1).Concat(_pendingLines))
        {
            string[] parts = line.Split(',');
            if (parts.Length < 2 || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double usedMB))
            {
                continue;
            }

            min = Math.Min(min, usedMB);
            max = Math.Max(max, usedMB);
            sum += usedMB;
            count++;
        }

        if (count == 0)
        {
            return new Stats(0, 0, 0, 0);
        }

        return new Stats(min, max, sum / count, count);
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
