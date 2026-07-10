using Microsoft.AspNetCore.Mvc;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 工件记录（在库清单）
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkpieceRecordsController : ControllerBase
{
    private readonly SqlServerRepository _repo;

    public WorkpieceRecordsController(SqlServerRepository repo) => _repo = repo;

    /// <summary>获取所有在库工件</summary>
    [HttpGet]
    public ActionResult<List<WorkpieceRecord>> GetAll()
    {
        return Ok(_repo.LoadInventory().OrderByDescending(i => i.InboundTime).ToList());
    }

    /// <summary>根据ID获取工件</summary>
    [HttpGet("{id}")]
    public ActionResult<WorkpieceRecord> GetById(Guid id)
    {
        var item = _repo.LoadInventory().FirstOrDefault(i => i.Id == id);
        if (item is null) return NotFound();
        return Ok(item);
    }

    /// <summary>入库操作</summary>
    [HttpPost("inbound")]
    public ActionResult<WorkpieceRecord> Inbound([FromBody] InboundRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OperatorName))
            return BadRequest(new { error = "请输入操作人员" });

        var result = _repo.Inbound(request);
        if (result is null)
            return BadRequest(new { error = "当前无空闲货位可分配或指定货位不可用" });

        return Ok(result);
    }

    /// <summary>出库操作</summary>
    [HttpPost("outbound")]
    public IActionResult Outbound([FromBody] OutboundRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OperatorName))
            return BadRequest(new { error = "请输入操作人员" });

        var success = _repo.Outbound(request);
        if (!success)
            return BadRequest(new { error = "出库失败，请检查工件编号或货位" });

        return Ok(new { message = "出库成功" });
    }
}
