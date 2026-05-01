using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace lhm_exporter;

internal static class Program
{
    private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "lhm_exporter", "logs");
    private static readonly string LogFile = Path.Combine(LogDir, "error.log");

    public static async Task<int> Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(LogDir);

            if (!IsAdministrator())
            {
                Log("Not running as administrator. Exiting.");
                Console.Error.WriteLine("This exporter must be run as Administrator.");
                return 1;
            }

            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddWindowsService(options => options.ServiceName = "lhm_exporter");
            builder.Services.AddHostedService<LhmExporterWorker>();
            builder.Logging.ClearProviders();

            using var host = builder.Build();
            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            LogException(ex);
            return 1;
        }
    }

    internal static void Log(string message)
    {
        try
        {
            var line = $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}";
            File.AppendAllText(LogFile, line, Encoding.UTF8);
        }
        catch { }
    }

    internal static void LogException(Exception ex)
    {
        try
        {
            var header = $"[{DateTime.UtcNow:O}] Exception: {ex.GetType().FullName} - {ex.Message}{Environment.NewLine}";
            File.AppendAllText(LogFile, header + ex.ToString() + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }

    internal static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static string? GetArgumentOrDefault(string[] args, int index, string? fallback)
    {
        if (args.Length > index && !string.IsNullOrWhiteSpace(args[index]))
        {
            return args[index];
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    internal static int GetIntArgumentOrDefault(string[] args, int index, string? fallback, int defaultValue)
    {
        var raw = GetArgumentOrDefault(args, index, fallback);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    internal static void RefreshAllHardware(IEnumerable<IHardware> hardwareNodes)
    {
        foreach (var hardware in hardwareNodes)
        {
            hardware.Update();
            RefreshAllHardware(hardware.SubHardware);
        }
    }

    internal static void WriteHardware(StringBuilder metrics, IHardware hardware, string? parentPath)
    {
        var path = parentPath is null ? hardware.Name : $"{parentPath}/{hardware.Name}";
        var hardwareType = NormalizeHardwareType(hardware.HardwareType.ToString());

        AppendMetric(metrics, "lhm_hardware_info", 1, new Dictionary<string, string>
        {
            ["identifier"] = hardware.Identifier.ToString(),
            ["hardware"] = hardware.Name,
            ["hardware_type"] = hardwareType,
            ["path"] = path,
            ["parent"] = parentPath ?? string.Empty,
        });

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is null)
            {
                continue;
            }

            var (metricName, multiplier) = GetMetricDefinition(sensor.SensorType.ToString());
            var labels = new Dictionary<string, string>
            {
                ["parent_identifier"] = hardware.Identifier.ToString(),
                ["parent_name"] = hardware.Name,
                ["identifier"] = hardware.Identifier.ToString(),
                ["device"] = hardwareType,
                ["name"] = NormalizeSensorName(sensor.Name),
                ["index"] = sensor.Index.ToString(CultureInfo.InvariantCulture),
                ["type"] = sensor.SensorType.ToString(),
                ["hardware_type"] = hardwareType,
            };

            var value = sensor.Value.Value * multiplier;
            AppendMetric(metrics, metricName, value, labels);
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            WriteHardware(metrics, subHardware, path);
        }
    }

    private static (string MetricName, double Multiplier) GetMetricDefinition(string sensorType)
    {
        var metricName = $"lhm_{sensorType.ToLowerInvariant()}";
        var multiplier = 1d;

        switch (sensorType)
        {
            case "Clock":
                metricName += "_mhz";
                break;
            case "Control":
            case "Level":
            case "Load":
                metricName += "_percent";
                break;
            case "Current":
                metricName += "_amperes";
                break;
            case "Data":
                metricName += "_bytes";
                multiplier = 1024d * 1024d * 1024d;
                break;
            case "Factor":
                metricName += "_total";
                break;
            case "Fan":
                metricName += "_rpm";
                break;
            case "Frequency":
                metricName += "_mhz";
                break;
            case "HeatFlux":
                metricName += "_watts_per_square_meter";
                break;
            case "SmallData":
                metricName = "lhm_data_bytes";
                multiplier = 1024d * 1024d;
                break;
            case "Throughput":
                metricName += "_bytes_per_second";
                break;
            case "Power":
                metricName += "_watts";
                break;
            case "Voltage":
                metricName += "_volts";
                break;
            case "Temperature":
                metricName += "_celsius";
                break;
        }

        return (metricName, multiplier);
    }

    private static string NormalizeHardwareType(string hardwareType)
    {
        if (hardwareType.Contains("superio", StringComparison.OrdinalIgnoreCase))
        {
            return "motherboard";
        }

        if (hardwareType.Contains("storage", StringComparison.OrdinalIgnoreCase))
        {
            return "disk";
        }

        if (hardwareType.Contains("gpu", StringComparison.OrdinalIgnoreCase))
        {
            return "gpu";
        }

        return hardwareType.ToLowerInvariant();
    }

    private static string NormalizeSensorName(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized.Replace("#", " ", StringComparison.Ordinal);
        normalized = normalized.Replace("/", " ", StringComparison.Ordinal);
        normalized = normalized.Replace("-", " ", StringComparison.Ordinal);
        normalized = normalized.Replace("(", " ", StringComparison.Ordinal);
        normalized = normalized.Replace(")", " ", StringComparison.Ordinal);
        normalized = normalized.Replace("[", " ", StringComparison.Ordinal);
        normalized = normalized.Replace("]", " ", StringComparison.Ordinal);
        normalized = normalized.Replace("+", " plus ", StringComparison.Ordinal);
        normalized = normalized.Replace("%", " percent ", StringComparison.Ordinal);

        var builder = new StringBuilder(normalized.Length);
        var previousUnderscore = false;

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousUnderscore = false;
                continue;
            }

            if (!previousUnderscore)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        var result = builder.ToString().Trim('_');

        while (result.Contains("__", StringComparison.Ordinal))
        {
            result = result.Replace("__", "_", StringComparison.Ordinal);
        }

        result = result.Replace("_number", string.Empty, StringComparison.Ordinal);
        result = result.Replace("_0", string.Empty, StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
    }

    private static void AppendMetric(StringBuilder metrics, string metricName, double value, IReadOnlyDictionary<string, string> labels)
    {
        metrics.Append(metricName);

        var filteredLabels = labels.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)).ToArray();
        if (filteredLabels.Length > 0)
        {
            metrics.Append('{');
            metrics.Append(string.Join(",", filteredLabels.Select(static pair => $"{NormalizeLabelName(pair.Key)}=\"{EscapeLabelValue(pair.Value)}\"")));
            metrics.Append('}');
        }

        metrics.Append(' ');
        metrics.Append(value.ToString("G17", CultureInfo.InvariantCulture));
        metrics.AppendLine();
    }

    private static string NormalizeLabelName(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
        return new string(chars);
    }

    private static string EscapeLabelValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

internal sealed class LhmExporterWorker : BackgroundService
{
    private readonly ILogger<LhmExporterWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);
    private readonly string _outputDirectory;
    private readonly string _outputFileName;

    public LhmExporterWorker(ILogger<LhmExporterWorker> logger)
    {
        _logger = logger;
        var args = Environment.GetCommandLineArgs();
        _outputDirectory = Program.GetArgumentOrDefault(args, 1, Environment.GetEnvironmentVariable("LHM_EXPORTER_OUTPUT_DIR"))
            ?? @"C:\Program Files\windows_exporter\textfile_inputs";
        _outputFileName = Program.GetArgumentOrDefault(args, 2, Environment.GetEnvironmentVariable("LHM_EXPORTER_OUTPUT_FILE"))
            ?? "lhm_exporter.prom";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        Directory.CreateDirectory(_outputDirectory);

        var outputPath = Path.Combine(_outputDirectory, _outputFileName);
        var tempPath = outputPath + ".tmp";
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsNetworkEnabled = true,
            IsPsuEnabled = true,
            IsStorageEnabled = true,
            IsBatteryEnabled = true,
            IsControllerEnabled = true,
            IsPowerMonitorEnabled = true,
        };

        computer.Open();

        try
        {
            using var timer = new PeriodicTimer(_interval);
            do
            {
                try
                {
                    WriteSnapshot(computer, outputPath, tempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while writing snapshot");
                    Program.LogException(ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ExecuteAsync");
            Program.LogException(ex);
        }
        finally
        {
            computer.Close();
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private void WriteSnapshot(Computer computer, string outputPath, string tempPath)
    {
        Program.RefreshAllHardware(computer.Hardware);

        var metrics = new StringBuilder();
        metrics.AppendLine("# HELP lhm_hardware_info LibreHardwareMonitor hardware inventory.");
        metrics.AppendLine("# TYPE lhm_hardware_info gauge");
        metrics.AppendLine("# HELP lhm_sensor_value LibreHardwareMonitor sensor values.");
        metrics.AppendLine("# TYPE lhm_sensor_value gauge");

        foreach (var hardware in computer.Hardware)
        {
            Program.WriteHardware(metrics, hardware, null);
        }

        File.WriteAllText(tempPath, metrics.ToString(), Encoding.UTF8);
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        File.Move(tempPath, outputPath);
        _logger.LogInformation("Wrote {OutputPath}", outputPath);
    }
}
