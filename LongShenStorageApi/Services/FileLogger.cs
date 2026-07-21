using System.Text;

namespace LongShenStorageApi.Services;

/// <summary>
/// 按天滚动的文件日志
/// 日志路径: {LogsFolder}/{yyyy-MM-dd}.txt
/// </summary>
public sealed class FileLogger
{
    private readonly string _logsFolder;
    private readonly object _lock = new();
    private string? _currentDate;
    private string? _currentFilePath;

    public FileLogger(string logsFolder)
    {
        _logsFolder = logsFolder;
        Directory.CreateDirectory(logsFolder);
    }

    public void Log(string level, string message)
    {
        var now = DateTime.Now;
        var dateStr = now.ToString("yyyy-MM-dd");
        var timestamp = now.ToString("HH:mm:ss.fff");

        // 按天切换文件
        if (_currentDate != dateStr)
        {
            lock (_lock)
            {
                if (_currentDate != dateStr)
                {
                    _currentDate = dateStr;
                    _currentFilePath = Path.Combine(_logsFolder, $"{dateStr}.txt");
                    Directory.CreateDirectory(_logsFolder);
                }
            }
        }

        var line = $"[{timestamp}] [{level}] {message}";

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_currentFilePath!, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // 日志写入失败不影响主程序
            }
        }
    }

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);
    public void Error(string message) => Log("ERROR", message);

    /// <summary>Modbus 报文日志（含十六进制）</summary>
    public void Modbus(string direction, string description, byte[] data, int offset, int count)
    {
        var hex = BitConverter.ToString(data, offset, count).Replace("-", " ");
        Log("MODBUS", $"{direction} | {description} | [{hex}]");
    }
}
