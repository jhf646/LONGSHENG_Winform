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
}
