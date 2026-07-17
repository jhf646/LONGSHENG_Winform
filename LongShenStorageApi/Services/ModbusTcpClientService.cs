using NModbus;
using System.Net.Sockets;

namespace LongShenStorageApi.Services;

/// <summary>
/// 真实 Modbus TCP 客户端 - 连接汇川Easy522 PLC
/// 读取D区寄存器功能码03，写入功能码06
/// </summary>
public sealed class ModbusTcpClientService : IModbusDevice, IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly byte _slaveId;
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private readonly object _lock = new();
    private bool _disposed;

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public ModbusTcpClientService(IConfiguration config)
    {
        var section = config.GetSection("ModbusTcp");
        _host = section["Host"] ?? "192.168.1.100";
        _port = int.TryParse(section["Port"], out var p) ? p : 502;
        _slaveId = byte.TryParse(section["SlaveId"], out var s) ? s : (byte)1;
    }

    public async Task ConnectAsync()
    {
        if (IsConnected) return;

        try
        {
            _tcpClient?.Dispose();
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_host, _port);
            var factory = new ModbusFactory();
            _master = factory.CreateMaster(_tcpClient);
            Console.WriteLine($"[ModbusTCP] 已连接到 {_host}:{_port} (站号:{_slaveId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModbusTCP] 连接失败: {ex.Message}");
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
        Console.WriteLine("[ModbusTCP] 已断开连接");
        return Task.CompletedTask;
    }

    public async Task<ushort> ReadHoldingRegisterAsync(int address)
    {
        EnsureConnected();
        var result = await _master!.ReadHoldingRegistersAsync(_slaveId, (ushort)address, 1);
        return result[0];
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(int startAddress, int count)
    {
        EnsureConnected();
        return await _master!.ReadHoldingRegistersAsync(_slaveId, (ushort)startAddress, (ushort)count);
    }

    public async Task WriteHoldingRegisterAsync(int address, ushort value)
    {
        EnsureConnected();
        await _master!.WriteSingleRegisterAsync(_slaveId, (ushort)address, value);
    }

    public void Tick()
    {
        // 真实设备无需模拟，留空
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Modbus TCP 未连接。请先调用 ConnectAsync()。");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _master?.Dispose();
        _tcpClient?.Dispose();
    }
}
