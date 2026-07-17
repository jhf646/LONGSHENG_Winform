using System.Text.Json;
using System.Text.Json.Serialization;

namespace LongShenStorageApi.Services;

/// <summary>
/// 寄存器配置服务 - 从 JSON 文件读取/保存寄存器定义
/// 编辑 registers.json 文件即可修改协议映射，无需重新编译
/// </summary>
public sealed class RegisterConfigService
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private RegisterConfig? _cache;

    public RegisterConfigService(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "registers.json");
    }

    public RegisterConfig GetConfig()
    {
        lock (_lock)
        {
            if (_cache is not null) return _cache;

            if (!File.Exists(_filePath))
                return _cache = CreateDefault();

            try
            {
                var json = File.ReadAllText(_filePath);
                var config = JsonSerializer.Deserialize<RegisterConfig>(json);
                return _cache = config ?? CreateDefault();
            }
            catch
            {
                return _cache = CreateDefault();
            }
        }
    }

    public void SaveConfig(RegisterConfig config)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            _cache = config;
        }
    }

    public List<RegisterDef> GetReadRegisters() => GetConfig().ReadRegisters ?? new();
    public List<RegisterDef> GetWriteRegisters() => GetConfig().WriteRegisters ?? new();

    private RegisterConfig CreateDefault()
    {
        var config = new RegisterConfig
        {
            DeviceName = "汇川Easy522 堆垛机",
            Protocol = "Modbus TCP D区寄存器",
            Description = "上位机通过Modbus TCP读写D区寄存器与堆垛机PLC通信。",
            ReadRegisters = new()
            {
                new() { Address = 100, Name = "立库状态", Description = "1:空闲中 2:运行中 3:故障中 4:暂停中" },
                new() { Address = 101, Name = "故障异常代码", Description = "0:正常" },
                new() { Address = 102, Name = "运行模式", Description = "1:手动 2:自动" },
                new() { Address = 103, Name = "动作步骤", Description = "当前执行到的步骤编号" },
                new() { Address = 104, Name = "A->B任务完成", Description = "1:完成 2:执行中" },
                new() { Address = 105, Name = "托盘转移状态", Description = "1:未取 2:已转堆垛车 3:已到目标" },
                new() { Address = 107, Name = "X轴实时位置", Description = "列方向", Unit = "mm" },
                new() { Address = 108, Name = "Y轴实时位置", Description = "层方向", Unit = "mm" },
                new() { Address = 109, Name = "Z轴深库位置", Description = "深库伸叉", Unit = "mm" },
                new() { Address = 110, Name = "Z轴浅库位置", Description = "浅库伸叉", Unit = "mm" },
                new() { Address = 111, Name = "是否可以移库", Description = "1:可以 2:不可以" },
                new() { Address = 112, Name = "左侧入库口可入库", Description = "1:可以 2:不可以" },
                new() { Address = 113, Name = "左侧出库口可出库", Description = "1:可以 2:不可以" },
                new() { Address = 114, Name = "右侧入库口可入库", Description = "1:可以 2:不可以" },
                new() { Address = 115, Name = "右侧出库口可出库", Description = "1:可以 2:不可以" },
                new() { Address = 116, Name = "A->B动作是否已完成", Description = "0/1" },
                new() { Address = 117, Name = "堆垛车当前位置", Description = "101+ 车位号" },
            },
            WriteRegisters = new()
            {
                new() { Address = 101, Name = "设备序号", Description = "1~N" },
                new() { Address = 102, Name = "序列1", Description = "动作序列号" },
                new() { Address = 103, Name = "动作标志位", Description = "1:有动作(触发执行)" },
                new() { Address = 104, Name = "A:排", Description = "11:入库 12:出库" },
                new() { Address = 105, Name = "A:列", Description = "" },
                new() { Address = 106, Name = "A:层", Description = "" },
                new() { Address = 107, Name = "B:排", Description = "" },
                new() { Address = 108, Name = "B:列", Description = "" },
                new() { Address = 109, Name = "B:层", Description = "" },
                new() { Address = 110, Name = "附加参数1", Description = "" },
                new() { Address = 111, Name = "附加参数2", Description = "" },
                new() { Address = 112, Name = "动作类型", Description = "1:默认 2:出库 3:入库 4:移库 5:直接出" },
                new() { Address = 113, Name = "出库后续动作", Description = "1:默认无动作" },
                new() { Address = 114, Name = "起始地址", Description = "" },
                new() { Address = 115, Name = "目标地址", Description = "" },
                new() { Address = 116, Name = "其他参数", Description = "" },
            }
        };
        SaveConfig(config);
        return config;
    }

    public void InvalidateCache() => _cache = null;
}

public sealed class RegisterConfig
{
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = "汇川Easy522";

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "Modbus TCP";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("readRegisters")]
    public List<RegisterDef> ReadRegisters { get; set; } = new();

    [JsonPropertyName("writeRegisters")]
    public List<RegisterDef> WriteRegisters { get; set; } = new();
}

public sealed class RegisterDef
{
    [JsonPropertyName("address")]
    public int Address { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;
}
