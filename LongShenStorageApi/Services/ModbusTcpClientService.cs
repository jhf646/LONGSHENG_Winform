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
    // 串行化所有网络往返，避免原始帧与NModbus请求的应答互相串包
    private readonly SemaphoreSlim _ioLock = new(1, 1);
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
            // 设置3秒连接超时
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _tcpClient.ConnectAsync(_host, _port, timeoutCts.Token);
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
        await _ioLock.WaitAsync();
        try
        {
            await EnsureConnectedAsync();
            await DiscardStaleDataAsync();
            var result = await _master!.ReadHoldingRegistersAsync(_slaveId, (ushort)address, 1);
            _logger.Info($"读取 D{address} = {result[0]}");
            return result[0];
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(int startAddress, int count)
    {
        await _ioLock.WaitAsync();
        try
        {
            await EnsureConnectedAsync();
            await DiscardStaleDataAsync();
            var result = await _master!.ReadHoldingRegistersAsync(_slaveId, (ushort)startAddress, (ushort)count);
            var summary = string.Join(", ", result.Take(8));
            _logger.Info($"批量读取 D{startAddress}~D{startAddress + count - 1} ({count}个) = [{summary}]");
            return result;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task WriteHoldingRegisterAsync(int address, ushort value)
    {
        await _ioLock.WaitAsync();
        try
        {
            await EnsureConnectedAsync();
            await DiscardStaleDataAsync();
            await _master!.WriteSingleRegisterAsync(_slaveId, (ushort)address, value);
            _logger.Info($"写入 D{address} = {value}");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    /// <summary>发送原始帧并读取完整应答帧（MBAP头6字节 + 数据区）</summary>
    public async Task<byte[]> SendRawFrameAsync(byte[] frame)
    {
        await _ioLock.WaitAsync();
        try
        {
            await EnsureConnectedAsync();
            var stream = _tcpClient!.GetStream();

            // 丢弃缓存区中的历史残留数据，避免本次应答错位
            await DiscardStaleDataAsync();

            await stream.WriteAsync(frame);
            _logger.Info($"发送原始帧: {BitConverter.ToString(frame)}");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            // MBAP头: 事务号2 + 协议号2 + 长度2
            var header = new byte[6];
            await ReadExactAsync(stream, header, timeoutCts.Token, frame);

            var bodyLength = (header[4] << 8) | header[5];
            byte[] response = header;
            if (bodyLength > 0)
            {
                response = new byte[6 + bodyLength];
                Array.Copy(header, response, 6);
                await ReadExactAsync(stream, response.AsMemory(6, bodyLength), timeoutCts.Token, frame);
            }

            _logger.Info($"收到响应: {BitConverter.ToString(response)}");
            return response;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public void Tick()
    {
        // 真实设备无需模拟，留空
    }

    private async Task DiscardStaleDataAsync()
    {
        if (_tcpClient is not { Connected: true }) return;
        var stream = _tcpClient.GetStream();
        var stale = new byte[256];
        while (stream.DataAvailable)
        {
            if (await stream.ReadAsync(stale, 0, stale.Length) <= 0) break;
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken token, byte[] frame)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.Slice(offset), token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"PLC 3秒内未返回完整应答，发送帧: {BitConverter.ToString(frame)}");
            }

            if (read <= 0)
                throw new IOException($"PLC 连接已断开，应答不完整，发送帧: {BitConverter.ToString(frame)}");

            offset += read;
        }
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
        _ioLock.Dispose();
    }
}
