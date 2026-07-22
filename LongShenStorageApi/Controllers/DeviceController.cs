using Microsoft.AspNetCore.Mvc;
using LongShenStorageApi.Services;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

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
    private readonly FileLogger _logger;
    private readonly SqlServerRepository _repo;

    public DeviceController(IModbusDevice device, RegisterConfigService config, FileLogger logger, SqlServerRepository repo)
    {
        _device = device;
        _config = config;
        _logger = logger;
        _repo = repo;
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
                    deviceNo = 0, actionFlag = 0,
                    row = 0, col = 0, level = 0
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

    /// <summary>入库PLC任务：查询状态→发送指令→等待完成→写入数据库</summary>
    [HttpPost("inbound-task")]
    public async Task<ActionResult<object>> InboundTask([FromBody] PlcTaskRequest request)
    {
        var steps = new List<object>();
        var success = false;
        var logEntries = new List<string>();

        try
        {
            // 步骤1：查询PLC状态
            _logger.Info($"===== 入库任务开始 托盘:{request.PalletNumber} 位置:{request.Row}排/{request.Col}列/{request.Level}层 =====");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 入库任务开始 - 托盘:{request.PalletNumber}");

            ushort status = await _device.ReadHoldingRegisterAsync(4009);
            var statusText = status switch { 1 => "空闲中", 2 => "运行中", 3 => "故障中", 4 => "暂停中", _ => $"未知({status})" };
            _logger.Info($"查询立库状态 D4009 = {status} ({statusText})");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 查询立库状态 D4009 = {status} ({statusText})");
            steps.Add(new { step = 1, name = "查询PLC状态", detail = $"D4009 = {status} ({statusText})", raw = $"读取 D4009 = {status}" });

            if (status != 1)
            {
                var errMsg = status switch { 3 => "立库故障中", 4 => "立库暂停中", _ => $"立库状态异常({status})" };
                _logger.Error($"入库失败: {errMsg}");
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ❌ 入库失败: {errMsg}");
                return Ok(new { success = false, error = errMsg, steps, logs = logEntries });
            }

            // 步骤2：发送PLC指令（入库动作码=3）
            // 第1排第1列特殊处理：层数-1发送给PLC
            var sendLevel = request.Level;
            var levelNote = "";
            if (request.Row == 1 && request.Col == 1)
            {
                sendLevel = request.Level - 1;
                levelNote = " (第1排第1列 层数特殊处理: " + request.Level + "→" + sendLevel + ")";
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ⚠️ 第1排第1列 层数特殊处理: {request.Level}→{sendLevel}");
            }
            _logger.Info($"发送入库指令: D2022=100, D2023=3, D2024={request.Row}, D2025={request.Col}, D2026={sendLevel}{levelNote}");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 发送入库指令 设备=100 动作=3 位置={request.Row}排/{request.Col}列/{sendLevel}层{levelNote}");
            await _device.WriteHoldingRegisterAsync(2022, 100);
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2022 = 100");
            await _device.WriteHoldingRegisterAsync(2023, 3);   // 入库=3
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2023 = 3 (入库)");
            await _device.WriteHoldingRegisterAsync(2024, (ushort)request.Row);
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2024 = {request.Row} (排)");
            await _device.WriteHoldingRegisterAsync(2025, (ushort)request.Col);
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2025 = {request.Col} (列)");
            await _device.WriteHoldingRegisterAsync(2026, (ushort)sendLevel);
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2026 = {sendLevel} (层){levelNote}");
            var writeRaw = $"写入 D2022=100, D2023=3, D2024={request.Row}, D2025={request.Col}, D2026={sendLevel}";
            _logger.Info($"入库指令发送成功: {writeRaw}{levelNote}");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ✅ 入库指令发送完成{levelNote}");
            steps.Add(new { step = 2, name = "发送PLC指令", detail = $"设备=100 动作=3 位置={request.Row}/{request.Col}/{sendLevel}{levelNote}", raw = writeRaw });

            // 等待3秒后检测PLC状态
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 等待3秒后检测PLC状态...");
            await Task.Delay(3000);
            status = await _device.ReadHoldingRegisterAsync(4009);
            _logger.Info($"指令后检测 D4009 = {status}");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 指令后检测 D4009 = {status}");
            if (status == 3 || status == 4)
            {
                var errMsg = status == 3 ? "立库故障中，任务中断" : "立库暂停中，任务中断";
                _logger.Error($"任务中断: {errMsg}");
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ⛔ {errMsg}");
                return Ok(new { success = false, error = errMsg, steps, logs = logEntries });
            }

            // 步骤3：等待任务完成（检测4009回到1）
            _logger.Info("等待任务完成...");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 等待任务完成...");
            steps.Add(new { step = 3, name = "等待任务完成", detail = "检测立库状态回到空闲", raw = "" });

            var maxWait = 60; // 最多等待60秒
            for (var i = 0; i < maxWait; i++)
            {
                await Task.Delay(1000);
                status = await _device.ReadHoldingRegisterAsync(4009);
                var currentStatusText = status switch { 1 => "空闲中", 2 => "运行中", 3 => "故障中", 4 => "暂停中", _ => $"未知({status})" };
                _logger.Info($"等待中 D4009 = {status} ({currentStatusText}) 第{i + 1}秒");

                if (status == 3 || status == 4)
                {
                    var errMsg = status == 3 ? $"立库故障中，任务中断 (第{i + 1}秒)" : $"立库暂停中，任务中断 (第{i + 1}秒)";
                    _logger.Error(errMsg);
                    logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ⛔ {errMsg}");
                    steps.Add(new { step = 4, name = "任务中断", detail = errMsg, raw = $"D4009 = {status}" });
                    return Ok(new { success = false, error = errMsg, steps, logs = logEntries });
                }

                if (status == 1)
                {
                    _logger.Info("✅ 任务执行成功，立库回到空闲状态");
                    logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ✅ 任务执行成功");
                    steps.Add(new { step = 4, name = "任务完成", detail = "立库回到空闲状态", raw = $"D4009 = {status} (等待{i + 1}秒)" });
                    success = true;
                    break;
                }
            }

            if (!success)
            {
                var errMsg = "等待超时，任务未完成";
                _logger.Error(errMsg);
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ⛔ {errMsg}");
                return Ok(new { success = false, error = errMsg, steps, logs = logEntries });
            }

            // 步骤4：写入数据库
            _logger.Info($"写入数据库 - 托盘:{request.PalletNumber} 位置:{request.Row}排/{request.Col}列/{request.Level}层");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入数据库...");
            try
            {
                var record = _repo.Inbound(new InboundRequest
                {
                    PalletNumber = request.PalletNumber,
                    ToolingNumber = request.ToolingNumber ?? "",
                    ProjectNumber = request.ProjectNumber ?? "",
                    ModelType = request.ModelType ?? "",
                    WorkOrder = request.WorkOrder ?? "",
                    CellNumber = request.CellNumber ?? "",
                    ComponentSections = request.ComponentSections,
                    CustomerName = request.CustomerName ?? "",
                    OperatorName = request.OperatorName,
                    SpecifiedSlot = $"{request.Row}排-{request.Col}列-{request.Level}层",
                    Notes = request.Notes ?? ""
                });
                if (record != null)
                {
                    _logger.Info($"✅ 数据库写入成功 货位:{record.SlotCode}");
                    logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ✅ 入库完成 货位:{record.SlotCode}");
                    steps.Add(new { step = 5, name = "数据库写入", detail = $"货位:{record.SlotCode}", raw = "" });
                }
                else
                {
                    logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ❌ 数据库写入失败：无空闲货位");
                    steps.Add(new { step = 5, name = "数据库写入", detail = "失败：无空闲货位", raw = "" });
                    success = false;
                }
            }
            catch (Exception dbEx)
            {
                _logger.Error($"数据库写入异常: {dbEx.Message}");
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ❌ 数据库写入异常: {dbEx.Message}");
                steps.Add(new { step = 5, name = "数据库写入", detail = dbEx.Message, raw = "" });
                success = false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"入库任务异常: {ex.Message}");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ❌ 任务异常: {ex.Message}");
            return Ok(new { success = false, error = ex.Message, steps, logs = logEntries });
        }

        _logger.Info($"===== 入库任务结束 ({(success ? "成功" : "失败")}) =====");
        logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ===== 入库任务 {(success ? "成功" : "失败")} =====");
        return Ok(new { success, steps, logs = logEntries });
    }

    /// <summary>出库PLC任务：查询状态→发送指令→等待完成→写入数据库</summary>
    [HttpPost("outbound-task")]
    public async Task<ActionResult<object>> OutboundTask([FromBody] PlcTaskRequest request)
    {
        var steps = new List<object>();
        var success = false;
        var logEntries = new List<string>();

        try
        {
            _logger.Info($"===== 出库任务开始 记录ID:{request.RecordId} 位置:{request.Row}排/{request.Col}列/{request.Level}层 =====");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 出库任务开始");

            // 步骤1：查询PLC状态
            ushort status = await _device.ReadHoldingRegisterAsync(4009);
            var statusText = status switch { 1 => "空闲中", 2 => "运行中", 3 => "故障中", 4 => "暂停中", _ => $"未知({status})" };
            _logger.Info($"查询立库状态 D4009 = {status} ({statusText})");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 查询立库状态 D4009 = {status} ({statusText})");
            steps.Add(new { step = 1, name = "查询PLC状态", detail = $"D4009 = {status} ({statusText})", raw = $"读取 D4009 = {status}" });

            if (status != 1)
            {
                var errMsg = status switch { 3 => "立库故障中", 4 => "立库暂停中", _ => $"立库状态异常({status})" };
                _logger.Error($"出库失败: {errMsg}");
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ❌ 出库失败: {errMsg}");
                return Ok(new { success = false, error = errMsg, steps, logs = logEntries });
            }

            // 步骤2：发送PLC指令（出库动作码=2）
            // 第1排第1列特殊处理：层数-1发送给PLC
            var sendLevel = request.Level;
            var levelNote = "";
            if (request.Row == 1 && request.Col == 1)
            {
                sendLevel = request.Level - 1;
                levelNote = " (第1排第1列 层数特殊处理: " + request.Level + "→" + sendLevel + ")";
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ⚠️ 第1排第1列 层数特殊处理: {request.Level}→{sendLevel}");
            }
            _logger.Info($"发送出库指令: D2022=100, D2023=2, D2024={request.Row}, D2025={request.Col}, D2026={sendLevel}{levelNote}");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 发送出库指令 设备=100 动作=2 位置={request.Row}排/{request.Col}列/{sendLevel}层{levelNote}");
            await _device.WriteHoldingRegisterAsync(2022, 100);
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2022 = 100");
            await _device.WriteHoldingRegisterAsync(2023, 2);   // 出库=2
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2023 = 2 (出库)");
            await _device.WriteHoldingRegisterAsync(2024, (ushort)request.Row);
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2024 = {request.Row} (排)");
            await _device.WriteHoldingRegisterAsync(2025, (ushort)request.Col);
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2025 = {request.Col} (列)");
            await _device.WriteHoldingRegisterAsync(2026, (ushort)sendLevel);
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 写入 D2026 = {sendLevel} (层){levelNote}");
            var writeRaw = $"写入 D2022=100, D2023=2, D2024={request.Row}, D2025={request.Col}, D2026={sendLevel}";
            _logger.Info($"出库指令发送成功: {writeRaw}{levelNote}");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ✅ 出库指令发送完成{levelNote}");
            steps.Add(new { step = 2, name = "发送PLC指令", detail = $"设备=100 动作=2 位置={request.Row}/{request.Col}/{sendLevel}{levelNote}", raw = writeRaw });

            // 等待3秒后检测PLC状态
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 等待3秒后检测PLC状态...");
            await Task.Delay(3000);
            status = await _device.ReadHoldingRegisterAsync(4009);
            _logger.Info($"指令后检测 D4009 = {status}");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 指令后检测 D4009 = {status}");
            if (status == 3 || status == 4)
            {
                var errMsg = status == 3 ? "立库故障中，任务中断" : "立库暂停中，任务中断";
                _logger.Error($"任务中断: {errMsg}");
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ⛔ {errMsg}");
                return Ok(new { success = false, error = errMsg, steps, logs = logEntries });
            }

            // 步骤3：等待任务完成
            _logger.Info("等待任务完成...");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 等待任务完成...");
            steps.Add(new { step = 3, name = "等待任务完成", detail = "检测立库状态回到空闲", raw = "" });

            var maxWait = 60;
            for (var i = 0; i < maxWait; i++)
            {
                await Task.Delay(1000);
                status = await _device.ReadHoldingRegisterAsync(4009);
                _logger.Info($"等待中 D4009 = {status} 第{i + 1}秒");

                if (status == 3 || status == 4)
                {
                    var errMsg = status == 3 ? $"立库故障中，任务中断 (第{i + 1}秒)" : $"立库暂停中，任务中断 (第{i + 1}秒)";
                    _logger.Error(errMsg);
                    logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ⛔ {errMsg}");
                    steps.Add(new { step = 4, name = "任务中断", detail = errMsg, raw = $"D4009 = {status}" });
                    return Ok(new { success = false, error = errMsg, steps, logs = logEntries });
                }

                if (status == 1)
                {
                    _logger.Info("✅ 任务执行成功，立库回到空闲状态");
                    logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ✅ 任务执行成功");
                    steps.Add(new { step = 4, name = "任务完成", detail = "立库回到空闲状态", raw = $"D4009 = {status} (等待{i + 1}秒)" });
                    success = true;
                    break;
                }
            }

            if (!success)
            {
                var errMsg = "等待超时，任务未完成";
                _logger.Error(errMsg);
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ⛔ {errMsg}");
                return Ok(new { success = false, error = errMsg, steps, logs = logEntries });
            }

            // 步骤4：写入数据库（出库）
            if (request.RecordId.HasValue)
            {
                _logger.Info($"执行出库数据库操作 记录ID:{request.RecordId}");
                logEntries.Add($"[{DateTime.Now:HH:mm:ss}] 执行出库数据库操作...");
                try
                {
                    var outResult = _repo.Outbound(new OutboundRequest
                    {
                        RecordId = request.RecordId.Value,
                        OperatorName = request.OperatorName,
                        SpecifiedSlot = null
                    });
                    if (outResult)
                    {
                        _logger.Info("✅ 出库数据库操作成功");
                        logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ✅ 出库完成");
                        steps.Add(new { step = 5, name = "数据库写入", detail = "出库成功", raw = "" });
                    }
                    else
                    {
                        logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ❌ 出库数据库操作失败");
                        steps.Add(new { step = 5, name = "数据库写入", detail = "失败", raw = "" });
                        success = false;
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.Error($"出库数据库异常: {dbEx.Message}");
                    logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ❌ 出库数据库异常: {dbEx.Message}");
                    steps.Add(new { step = 5, name = "数据库写入", detail = dbEx.Message, raw = "" });
                    success = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"出库任务异常: {ex.Message}");
            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ❌ 任务异常: {ex.Message}");
            return Ok(new { success = false, error = ex.Message, steps, logs = logEntries });
        }

        _logger.Info($"===== 出库任务结束 ({(success ? "成功" : "失败")}) =====");
        logEntries.Add($"[{DateTime.Now:HH:mm:ss}] ===== 出库任务 {(success ? "成功" : "失败")} =====");
        return Ok(new { success, steps, logs = logEntries });
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
    public int ActionFlag { get; set; } = 1;
    public int FromRow { get; set; }
    public int FromCol { get; set; }
    public int FromLevel { get; set; }
}

public sealed class PlcTaskRequest
{
    public string PalletNumber { get; set; } = "";
    public string? ToolingNumber { get; set; }
    public string? ProjectNumber { get; set; }
    public string? ModelType { get; set; }
    public string? WorkOrder { get; set; }
    public string? CellNumber { get; set; }
    public int ComponentSections { get; set; } = 1;
    public string? CustomerName { get; set; }
    public string OperatorName { get; set; } = "";
    public string? Notes { get; set; }
    public Guid? RecordId { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int Level { get; set; }
}
