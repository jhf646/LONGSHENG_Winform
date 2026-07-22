using Microsoft.AspNetCore.Mvc;
using LongShenStorageApi.Services;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 设备监控与调用（汇川Easy522 Modbus TCP）
/// 寄存器定义见 registers.json，可编辑无需改代码
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly IModbusDevice _device;
    private readonly RegisterConfigService _config;

    public DeviceController(IModbusDevice device, RegisterConfigService config)
    {
        _device = device;
        _config = config;
    }

    /// <summary>获取读取寄存器定义（来自registers.json）</summary>
    [HttpGet("read-defs")]
    public ActionResult<object> GetReadDefs()
    {
        return Ok(new { registers = _config.GetReadRegisters() });
    }

    /// <summary>获取写入寄存器定义（来自registers.json）</summary>
    [HttpGet("write-defs")]
    public ActionResult<object> GetWriteDefs()
    {
        return Ok(new { registers = _config.GetWriteRegisters() });
    }

    /// <summary>获取完整配置（含说明）</summary>
    [HttpGet("config")]
    public ActionResult<RegisterConfig> GetConfig()
    {
        return Ok(_config.GetConfig());
    }

    /// <summary>保存配置到registers.json</summary>
    [HttpPost("config")]
    public IActionResult SaveConfig([FromBody] RegisterConfig config)
    {
        _config.SaveConfig(config);
        return Ok(new { message = "寄存器配置已保存到 registers.json" });
    }

    /// <summary>读取指定地址的寄存器值</summary>
    [HttpGet("read/{address}")]
    public async Task<ActionResult<object>> ReadRegister(int address)
    {
        try
        {
            var value = await _device.ReadHoldingRegisterAsync(address);
            var def = _config.GetReadRegisters().FirstOrDefault(r => r.Address == address);
            return Ok(new { address, value, name = def?.Name ?? "", description = def?.Description ?? "", unit = def?.Unit ?? "" });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = $"读取D{address}失败: {ex.Message}", address });
        }
    }

    /// <summary>批量读取寄存器</summary>
    [HttpGet("read-batch")]
    public async Task<ActionResult<List<object>>> ReadBatch([FromQuery] string addresses)
    {
        var parts = addresses.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<object>();
        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), out int addr))
            {
                try
                {
                    var value = await _device.ReadHoldingRegisterAsync(addr);
                    var def = _config.GetReadRegisters().FirstOrDefault(r => r.Address == addr)
                           ?? _config.GetWriteRegisters().FirstOrDefault(r => r.Address == addr);
                    results.Add(new { address = addr, value, name = def?.Name ?? "", description = def?.Description ?? "", unit = def?.Unit ?? "" });
                }
                catch { results.Add(new { address = addr, value = 0, name = "", description = "读取失败", unit = "" }); }
            }
        }
        return Ok(results);
    }

    /// <summary>读取所有监控寄存器（一次性批量读取 4008~4031）</summary>
    [HttpGet("monitor")]
    public async Task<ActionResult<object>> MonitorAll()
    {
        try
        {
            var readRegs = _config.GetReadRegisters();
            var writeRegs = _config.GetWriteRegisters();

            // 一次性读取24个寄存器（4008~4031），对应PLC指令：01 03 0F A8 00 18
            ushort[] vals;
            try
            {
                vals = await _device.ReadHoldingRegistersAsync(4008, 24);
            }
            catch
            {
                vals = new ushort[24];
            }

            ushort V(int offset) => offset < vals.Length ? vals[offset] : (ushort)0;

            var data = new Dictionary<string, object>
            {
                ["deviceNo"] = V(0),
                ["status"] = new { state = V(1), errorCode = V(2), mode = V(3), step = V(4) },
                ["flags"] = new
                {
                    taskDone = V(5), transferState = V(6), spare = V(7),
                    leftIn = V(13), leftOut = V(14), rightIn = V(15),
                    rightOut = V(16), actionDone = V(17), carrierPos = V(18)
                },
                ["position"] = new { x = V(8), y = V(9), zDeep = V(10), zShallow = V(11) },
                ["canMove"] = V(12),
                ["connected"] = _device.IsConnected,
                ["configName"] = _config.GetConfig().DeviceName,
                ["command"] = new
                {
                    deviceNo = 0, seqNo = 0, actionFlag = 0,
                    aRow = 0, aCol = 0, aLevel = 0,
                    bRow = 0, bCol = 0, bLevel = 0,
                    param1 = 0, param2 = 0, actionType = 0
                }
            };
            return Ok(data);
        }
        catch (Exception ex)
        {
            return Ok(new { connected = false, error = ex.Message, status = new { mode = 0, state = 0, step = 0, errorCode = 0 },
                position = new { x = 0, y = 0, zDeep = 0, zShallow = 0 }, flags = new { }, command = new { } });
        }
    }

    /// <summary>写入寄存器值</summary>
    [HttpPost("write")]
    public async Task<IActionResult> WriteRegister([FromBody] WriteRequest request)
    {
        if (request.Address < 0 || request.Address > 65535)
            return BadRequest(new { error = "地址范围 0-65535" });
        if (request.Value < 0 || request.Value > 65535)
            return BadRequest(new { error = "值范围 0-65535" });

        await _device.WriteHoldingRegisterAsync(request.Address, (ushort)request.Value);
        return Ok(new { message = $"D{request.Address} = {request.Value} 已写入" });
    }

    /// <summary>便捷方法：发送取货/送货指令</summary>
    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] DeviceCommand cmd)
    {
        await _device.WriteHoldingRegisterAsync(4101, (ushort)cmd.DeviceNo);
        await _device.WriteHoldingRegisterAsync(4102, 1); // 动作序列+标志位(合并)
        await _device.WriteHoldingRegisterAsync(4103, (ushort)cmd.FromRow);
        await _device.WriteHoldingRegisterAsync(4104, (ushort)cmd.FromCol);
        await _device.WriteHoldingRegisterAsync(4105, (ushort)cmd.FromLevel);
        await _device.WriteHoldingRegisterAsync(4106, (ushort)cmd.ToRow);
        await _device.WriteHoldingRegisterAsync(4107, (ushort)cmd.ToCol);
        await _device.WriteHoldingRegisterAsync(4108, (ushort)cmd.ToLevel);
        await _device.WriteHoldingRegisterAsync(4111, (ushort)cmd.ActionType);

        return Ok(new { message = $"指令已发送: {cmd.ActionType} | A({cmd.FromRow},{cmd.FromCol},{cmd.FromLevel}) → B({cmd.ToRow},{cmd.ToCol},{cmd.ToLevel})" });
    }
}

public sealed class WriteRequest
{
    public int Address { get; set; }
    public int Value { get; set; }
}

public sealed class DeviceCommand
{
    public int DeviceNo { get; set; } = 1;
    public int FromRow { get; set; }
    public int FromCol { get; set; }
    public int FromLevel { get; set; }
    public int ToRow { get; set; }
    public int ToCol { get; set; }
    public int ToLevel { get; set; }
    public int ActionType { get; set; } = 1;
}
