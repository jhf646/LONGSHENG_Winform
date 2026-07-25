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

    /// <summary>重试故障任务（用已保存的数据重新执行入库/出库）</summary>
    [HttpPost("{id}/retry")]
    public IActionResult RetryTask(Guid id, [FromBody] RetryFaultRequest? request)
    {
        var tasks = _repo.GetFaultTasks();
        var task = tasks.FirstOrDefault(t => t.Id == id);
        if (task is null)
            return NotFound(new { error = "故障任务不存在" });

        var operatorName = request?.OperatorName ?? task.OperatorName;

        // 根据任务类型重新执行
        string result;
        if (task.TaskType == "Inbound")
        {
            result = _repo.ResolveFaultTask(new ResolveFaultRequest
            {
                FaultId = id,
                Action = "已处理入库",
                OperatorName = operatorName
            });
        }
        else if (task.TaskType == "Outbound")
        {
            result = _repo.ResolveFaultTask(new ResolveFaultRequest
            {
                FaultId = id,
                Action = "已处理出库",
                OperatorName = operatorName
            });
        }
        else
        {
            return BadRequest(new { error = "未知的任务类型" });
        }

        if (!string.IsNullOrEmpty(result))
            return BadRequest(new { error = result });

        return Ok(new { message = "任务已重试执行成功" });
    }
}

/// <summary>重试请求</summary>
public sealed class RetryFaultRequest
{
    public string? OperatorName { get; set; }
}
