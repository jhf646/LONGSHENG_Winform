using System.Text.Json;
using System.Text.Json.Serialization;

namespace LongShenStorageApi.Services;

/// <summary>
/// 故障代码解析服务 — 读取 faultcodes.json，将 4010/4011 寄存器值解析为具体故障列表
/// </summary>
public sealed class FaultCodeService
{
    private readonly List<FaultCodeDef> _faults;

    public FaultCodeService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "faultcodes.json");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "faultcodes.json");
        if (!File.Exists(path))
        {
            _faults = new();
            return;
        }
        var json = File.ReadAllText(path);
        var root = JsonSerializer.Deserialize<FaultCodeRoot>(json);
        _faults = root?.Faults ?? new();
    }

    /// <summary>
    /// 解析两个寄存器的值，返回触发的故障列表
    /// </summary>
    public List<ActiveFault> Parse(ushort reg4010, ushort reg4011)
    {
        // 组合为32位: D4010在低16位(bit0-15), D4011在高16位(bit16-31)
        uint combined = ((uint)reg4011 << 16) | reg4010;
        var result = new List<ActiveFault>();

        foreach (var def in _faults)
        {
            if (def.Bit < 0 || def.Bit > 31) continue;
            var isActive = ((combined >> def.Bit) & 1) == 1;
            if (isActive)
            {
                result.Add(new ActiveFault
                {
                    Bit = def.Bit,
                    Code = def.Code,
                    Name = def.Name,
                    Register = def.Bit < 16 ? 4010 : 4011
                });
            }
        }

        return result;
    }

    /// <summary>获取完整故障代码定义列表</summary>
    public List<FaultCodeDef> GetAll() => _faults;
}

public sealed class FaultCodeDef
{
    [JsonPropertyName("bit")]
    public int Bit { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class ActiveFault
{
    public int Bit { get; set; }
    public int Code { get; set; }
    public string Name { get; set; } = "";
    public int Register { get; set; }
}

public sealed class FaultCodeRoot
{
    [JsonPropertyName("faults")]
    public List<FaultCodeDef> Faults { get; set; } = new();
}
