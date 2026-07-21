using NModbus;
using System.Net.Sockets;

namespace LongShenStorageApi.Services;

/// <summary>
/// 真实 Modbus TCP 客户端 - 连接汇川Easy522 PLC
/// 读取D区寄存器功能码03，写入功能码06
/// 所有通信记录到 Logs/ 目录，按天分割
/// </summary>
public sealed class ModbusTcpClientService : IModbusDevice, IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly byte _slaveId;
    private readonly FileLogger _logger;
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private readonly object _lock = new();
    private bool _disposed;

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public ModbusTcpClientService(IConfiguration config, FileLogger logger)
    {
        var section = config.GetSection("ModbusTcp");
        _host = section["Host"] ?? "192.168.1.100";
        _port = int.TryParse(section["Port"], out var p) ? p : 502;
        _slaveId = byte.TryParse(section["SlaveId"], out var s) ? s : (byte)1;
        _logger = logger;
        _logger.Info($"ModbusTcpClientService 初始化: {_host}:{_port} 站号={_slaveId}");
    }

    public async Task ConnectAsync()
    {
        if (IsConnected)
        {
            _logger.Info($"PLC 已连接: {_host}:{_port}");
            return;
        }

        try
        {
            _tcpClient?.Dispose();
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_host, _port);
            var factory = new ModbusFactory();
            _master = factory.CreateMaster(_tcpClient);
            _logger.Info($"PLC 连接成功: {_host}:{_port} (站号:{_slaveId})");
        }
        catch (Exception ex)
        {
            _logger.Error($"PLC 连接失败 {_host}:{_port} - {ex.Message}");
            _tcpClient?.Dispose();
            _tcpClient = null;
            throw;
        }
    }

    public Task DisconnectAsync()
    {
        _master?.Dispose();
        _master = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
        _logger.Info("PLC 已断开连接");
        return Task.CompletedTask;
    }

    public async Task<ushort> ReadHoldingRegisterAsync(int address)
    {
        await EnsureConnectedAsync();
        var result = await _master!.ReadHoldingRegistersAsync(_slaveId, (ushort)address, 1);
        _logger.Info($"读取 D{address} = {result[0]}");
        return result[0];
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(int startAddress, int count)
    {
        await EnsureConnectedAsync();
        var result = await _master!.ReadHoldingRegistersAsync(_slaveId, (ushort)startAddress, (ushort)count);
        var summary = string.Join(", ", result.Take(8));
        _logger.Info($"批量读取 D{startAddress}~D{startAddress + count - 1} ({count}个) = [{summary}]");
        return result;
    }

    public async Task WriteHoldingRegisterAsync(int address, ushort value)
    {
        await EnsureConnectedAsync();
        await _master!.WriteSingleRegisterAsync(_slaveId, (ushort)address, value);
        _logger.Info($"写入 D{address} = {value}");
    }

    public void Tick()
    {
        // 真实设备无需模拟，留空
    }

    private async Task EnsureConnectedAsync()
    {
        if (!IsConnected)
        {
            _logger.Info("PLC 未连接，触发自动连接...");
            await ConnectAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _master?.Dispose();
        _tcpClient?.Dispose();
    }
}
