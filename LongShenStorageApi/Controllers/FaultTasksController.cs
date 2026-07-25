using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 出入库故障任务管理
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FaultTasksController : ControllerBase
{
    private readonly SqlServerRepository _repo;

    public FaultTasksController(SqlServerRepository repo) => _repo = repo;

    /// <summary>获取所有故障任务</summary>
    [HttpGet]
    public ActionResult<List<FaultTaskRecord>> GetAll()
    {
        return Ok(_repo.GetFaultTasks());
    }

    /// <summary>处理故障任务</summary>
    [HttpPost("resolve")]
    public IActionResult Resolve([FromBody] ResolveFaultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
            return BadRequest(new { error = "请选择处理方式" });

        var error = _repo.ResolveFaultTask(request);
        if (!string.IsNullOrEmpty(error))
            return BadRequest(new { error });

        return Ok(new { message = "故障已处理" });
    }
}
