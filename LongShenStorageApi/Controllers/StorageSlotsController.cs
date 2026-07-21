using Microsoft.AspNetCore.Mvc;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 货位管理
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StorageSlotsController : ControllerBase
{
    private readonly SqlServerRepository _repo;

    public StorageSlotsController(SqlServerRepository repo) => _repo = repo;

    /// <summary>获取所有货位</summary>
    [HttpGet]
    public ActionResult<List<StorageSlot>> GetAll()
    {
        var state = _repo.Load();
        return Ok(state.Slots.OrderBy(s => s.RowNumber).ThenBy(s => s.ColumnNumber).ThenBy(s => s.LevelNumber).ToList());
    }

    /// <summary>获取下一个空闲货位</summary>
    [HttpGet("next-available")]
    public ActionResult<object> GetNextAvailable()
    {
        var state = _repo.Load();
        var slot = state.Slots.FirstOrDefault(s => !s.IsOccupied);
        if (slot is null) return Ok(new { slotCode = "", message = "没有空闲货位" });
        return Ok(new { slotCode = slot.SlotCode, message = $"推荐空闲货位：{slot.SlotCode}" });
    }

    /// <summary>释放指定货位（仅空闲状态允许）</summary>
    [HttpPost("{slotCode}/release")]
    public IActionResult Release(string slotCode)
    {
        var state = _repo.Load();
        var slot = state.Slots.FirstOrDefault(s => s.SlotCode == slotCode);
        if (slot is null) return NotFound(new { error = "货位不存在" });
        if (slot.IsOccupied) return BadRequest(new { error = "货位当前有工件占用，不能释放" });

        slot.WorkpieceId = null;
        _repo.Save(state);
        return Ok(new { message = $"货位 {slotCode} 已释放" });
    }

    /// <summary>更新库位（内部编号、启用状态）</summary>
    [HttpPut]
    public IActionResult Update([FromBody] SlotUpdateRequest request)
    {
        var state = _repo.Load();
        var slot = state.Slots.FirstOrDefault(s => s.SlotCode == request.SlotCode);
        if (slot is null) return NotFound(new { error = "货位不存在" });

        slot.InternalNumber = request.InternalNumber;
        slot.IsEnabled = request.IsEnabled;
        _repo.Save(state);
        return Ok(new { message = $"货位 {slot.SlotCode} 已更新" });
    }

    /// <summary>异常释放：清空库位信息（强制释放，会删除工件记录）</summary>
    [HttpPost("{slotCode}/abnormal-release")]
    public IActionResult AbnormalRelease(string slotCode)
    {
        var state = _repo.Load();
        var slot = state.Slots.FirstOrDefault(s => s.SlotCode == slotCode);
        if (slot is null) return NotFound(new { error = "货位不存在" });

        // 如果有工件占用，删除工件记录
        if (slot.IsOccupied && slot.WorkpieceId.HasValue)
        {
            var record = state.Inventory.FirstOrDefault(r => r.Id == slot.WorkpieceId.Value);
            if (record != null)
            {
                state.Inventory.Remove(record);
                state.Ledger.Add(new LedgerEntry
                {
                    Type = TransactionType.Outbound,
                    Timestamp = DateTime.Now,
                    OperatorName = "系统(异常释放)",
                    PalletNumber = record.PalletNumber,
                    SlotCode = record.SlotCode,
                    ActionDescription = $"异常释放：托盘{record.PalletNumber}从{slot.SlotCode}强制出库"
                });
            }
        }

        slot.IsOccupied = false;
        slot.WorkpieceId = null;
        _repo.Save(state);
        return Ok(new { message = $"货位 {slotCode} 异常释放成功" });
    }
}
