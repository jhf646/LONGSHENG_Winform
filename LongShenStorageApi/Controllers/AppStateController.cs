using Microsoft.AspNetCore.Mvc;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 系统状态与仪表盘
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AppStateController : ControllerBase
{
    private readonly SqlServerRepository _repo;

    public AppStateController(SqlServerRepository repo) => _repo = repo;

    /// <summary>获取完整系统状态</summary>
    [HttpGet]
    public ActionResult<AppState> GetState()
    {
        var state = _repo.Load();
        return Ok(state);
    }

    /// <summary>获取仪表盘数据</summary>
    [HttpGet("dashboard")]
    public ActionResult<DashboardData> GetDashboard()
    {
        var state = _repo.Load();
        var inventoryCount = state.Inventory.Count;
        var occupied = state.Slots.Count(s => s.IsOccupied);
        var free = state.Slots.Count(s => !s.IsOccupied);
        var alert = inventoryCount < state.AlertSettings.MinThreshold || inventoryCount > state.AlertSettings.MaxThreshold;
        var alertText = inventoryCount < state.AlertSettings.MinThreshold ? "低于下限" : inventoryCount > state.AlertSettings.MaxThreshold ? "高于上限" : "正常";

        return Ok(new DashboardData
        {
            InventoryCount = inventoryCount,
            OccupiedSlots = occupied,
            FreeSlots = free,
            AlertStatus = alertText,
            IsAlert = alert,
            Slots = state.Slots,
            RecentInventory = state.Inventory.OrderByDescending(i => i.InboundTime).Take(20).ToList()
        });
    }

    /// <summary>获取下拉选项列表</summary>
    [HttpGet("dropdowns")]
    public ActionResult<object> GetDropdowns()
    {
        var state = _repo.Load();
        var palletNumbers = state.PalletNumbers?.Count > 0 ? state.PalletNumbers : Enumerable.Range(1, 66).Select(i => $"T{i}").ToList();
        return Ok(new
        {
            palletNumbers,
            state.ToolingNumbers,
            state.ProjectNumbers,
            state.ModelTypes,
            state.CustomerNames
        });
    }

    /// <summary>保存预警阈值</summary>
    [HttpPost("alerts")]
    public IActionResult SaveAlerts([FromBody] InventoryAlertSettings settings)
    {
        var state = _repo.Load();
        state.AlertSettings = settings;
        _repo.Save(state);
        return Ok(new { message = "预警阈值已保存" });
    }

    /// <summary>保存下拉选项（用户新增的项）</summary>
    [HttpPost("dropdowns")]
    public IActionResult SaveDropdowns([FromBody] AppState updatedState)
    {
        var state = _repo.Load();
        if (updatedState.PalletNumbers?.Count > 0) state.PalletNumbers = updatedState.PalletNumbers;
        if (updatedState.ToolingNumbers?.Count > 0) state.ToolingNumbers = updatedState.ToolingNumbers;
        if (updatedState.ProjectNumbers?.Count > 0) state.ProjectNumbers = updatedState.ProjectNumbers;
        if (updatedState.ModelTypes?.Count > 0) state.ModelTypes = updatedState.ModelTypes;
        if (updatedState.CustomerNames?.Count > 0) state.CustomerNames = updatedState.CustomerNames;
        _repo.Save(state);
        return Ok(new { message = "下拉选项已保存" });
    }
}
