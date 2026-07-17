namespace LongShenStorageApi.Services;

/// <summary>
/// Modbus 设备抽象接口 - 模拟器和真实PLC都实现此接口
/// </summary>
public interface IModbusDevice
{
    /// <summary>设备是否已连接</summary>
    bool IsConnected { get; }

    /// <summary>连接设备</summary>
    Task ConnectAsync();

    /// <summary>断开连接</summary>
    Task DisconnectAsync();

    /// <summary>读取单个保持寄存器 (功能码03)</summary>
    Task<ushort> ReadHoldingRegisterAsync(int address);

    /// <summary>批量读取保持寄存器</summary>
    Task<ushort[]> ReadHoldingRegistersAsync(int startAddress, int count);

    /// <summary>写入单个保持寄存器 (功能码06)</summary>
    Task WriteHoldingRegisterAsync(int address, ushort value);

    /// <summary>模拟运行周期（模拟器用，真实设备忽略）</summary>
    void Tick();
}
