namespace LongShenStorageApi.Services;

/// <summary>
/// 汇川Easy522 Modbus TCP 模拟器
/// 模拟D区寄存器(地址0-65535)，模拟真实PLC行为
/// 实现 IModbusDevice 接口，可与真实客户端互换
/// </summary>
public sealed class ModbusSimulator : IModbusDevice
{
    public bool IsConnected => true; // 模拟器始终在线

    private readonly ushort[] _registers = new ushort[65536];
    private readonly object _lock = new();
    private readonly Random _rng = new();
    private int _simTick;

    public ModbusSimulator()
    {
        // 初始化读取寄存器的默认值
        _registers[100] = 1;    // 立库状态: 空闲中
        _registers[102] = 1;    // 运行模式: 手动
        _registers[103] = 1;    // 动作步骤: 初始
        _registers[104] = 0;    // A->B完成
        _registers[105] = 0;    // 转移状态
        _registers[111] = 1;    // 可以移库
        _registers[112] = 1;    // 左侧入库口可入库
        _registers[113] = 1;    // 左侧出库口可出库
        _registers[114] = 1;    // 右侧入库口可入库
        _registers[115] = 1;    // 右侧出库口可出库
        _registers[116] = 0;    // A->B动作未完成
        _registers[117] = 0;    // 堆垛车位置

        // 模拟X/Y/Z位置初始值
        _registers[107] = 0;    // X轴
        _registers[108] = 0;    // Y轴
        _registers[109] = 0;    // Z深
        _registers[110] = 0;    // Z浅
    }

    /// <summary>模拟运行一个周期 - 响应写入指令，模拟设备运行</summary>
    public void Tick()
    {
        lock (_lock)
        {
            _simTick++;

            // 读取写入寄存器(地址101-130)检查是否有新指令
            ushort actionFlag = _registers[103]; // 动作标志位 (写入区偏移)
            ushort status = _registers[100];      // 读取区: 立库状态

            // 如果写入区有动作指令且设备空闲，模拟执行
            if (actionFlag == 1 && status == 1) // 有动作且设备空闲
            {
                status = 2; // 运行中
                _registers[100] = 2;

                // 模拟A->B动作执行（简化版）
                SimulateMotion();
            }

            // 模拟位置实时更新（运行中时）
            if (status == 2)
            {
                // 位置数据在模拟运动中已更新
            }

            // 每5个tick随机微调位置（模拟传感器波动）
            if (_simTick % 5 == 0 && status == 1)
            {
                _registers[107] = (ushort)Math.Max(0, _registers[107] + _rng.Next(-1, 2));
                _registers[108] = (ushort)Math.Max(0, _registers[108] + _rng.Next(-1, 2));
            }

            // 复位写入标志位（模拟PLC处理完成后清零）
            if (status == 1) // 空闲时才允许新指令
            {
                _registers[103] = 0; // 清除动作标志
            }
        }
    }

    private void SimulateMotion()
    {
        // 模拟堆垛机从A点到B点的运动过程
        // 写入区地址101=设备序号, 102=序列1, 103=动作标志, 104=A排, 105=A列, 106=A层
        // 107=B排, 108=B列, 109=B层, 110=附加参数1, 111=附加参数2
        ushort aRow = _registers[104];
        ushort aCol = _registers[105];
        ushort aLevel = _registers[106];
        ushort bRow = _registers[107];
        ushort bCol = _registers[108];
        ushort bLevel = _registers[109];

        // 模拟X轴移动（列方向）
        int targetX = bCol * 100;
        int currentX = _registers[107];
        int newX = currentX;
        if (newX < targetX) newX = Math.Min(targetX, newX + _rng.Next(5, 20));
        else if (newX > targetX) newX = Math.Max(targetX, newX - _rng.Next(5, 20));
        _registers[107] = (ushort)newX;

        // 模拟Y轴移动（层方向）
        int targetY = bLevel * 50;
        int currentY = _registers[108];
        int newY = currentY;
        if (newY < targetY) newY = Math.Min(targetY, newY + _rng.Next(3, 10));
        else if (newY > targetY) newY = Math.Max(targetY, newY - _rng.Next(3, 10));
        _registers[108] = (ushort)newY;

        // 模拟Z轴伸出/收回
        ushort zDeep = _registers[109];
        ushort zShallow = _registers[110];
        if (zDeep < 50) _registers[109] = (ushort)(zDeep + _rng.Next(1, 5));
        if (zShallow < 30) _registers[110] = (ushort)(zShallow + _rng.Next(1, 4));

        // 检查是否到达目标位置
        if (Math.Abs(newX - targetX) < 5 && Math.Abs(newY - targetY) < 5)
        {
            // 到达B点
            _registers[104] = 1; // A->B任务完成标志
            _registers[100] = 1; // 恢复空闲
            _registers[103] = 0; // 清除动作标志
            _registers[116] = 1; // A->B完成
        }
    }

    // ===== 公开读写方法 =====

    public ushort ReadRegister(int address)
    {
        lock (_lock)
        {
            if (address < 0 || address >= 65536) return 0;
            return _registers[address];
        }
    }

    public void WriteRegister(int address, ushort value)
    {
        lock (_lock)
        {
            if (address < 0 || address >= 65536) return;

            // 写入区: 地址101~130 为上位机写入区
            if (address >= 101 && address <= 130)
            {
                _registers[address] = value;
                // 当写入动作标志位(地址103=1)时触发动作
                if (address == 103 && value == 1)
                {
                    _registers[100] = 2; // 设置为运行中
                }
            }
            // 读取区只读(地址0-100)，但允许API强制写入(管理员)
            else
            {
                _registers[address] = value;
            }
        }
    }

    public ushort[] ReadRegisters(int startAddress, int count)
    {
        lock (_lock)
        {
            var result = new ushort[count];
            for (int i = 0; i < count; i++)
                result[i] = (startAddress + i < 65536) ? _registers[startAddress + i] : (ushort)0;
            return result;
        }
    }

    // ===== IModbusDevice 接口实现 =====
    public Task ConnectAsync() => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<ushort> ReadHoldingRegisterAsync(int address)
    {
        return Task.FromResult(ReadRegister(address));
    }

    public Task<ushort[]> ReadHoldingRegistersAsync(int startAddress, int count)
    {
        return Task.FromResult(ReadRegisters(startAddress, count));
    }

    public Task WriteHoldingRegisterAsync(int address, ushort value)
    {
        WriteRegister(address, value);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 后台服务 - 定时触发模拟器/设备心跳
/// </summary>
public sealed class ModbusSimulatorHostedService : IHostedService, IDisposable
{
    private readonly IModbusDevice _device;
    private Timer? _timer;

    public ModbusSimulatorHostedService(IModbusDevice device)
    {
        _device = device;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 每500ms模拟一个周期
        _timer = new Timer(_ => _device.Tick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();
}
