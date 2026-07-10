using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 角色权限管理
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class RolesController : ControllerBase
{
    private readonly SqlServerRepository _repo;

    public RolesController(SqlServerRepository repo) => _repo = repo;

    /// <summary>获取所有角色及其权限</summary>
    [HttpGet]
    public ActionResult<List<RolePermissionsResponse>> GetAllRoles()
    {
        var roles = new[] { "Admin", "Operator", "Viewer" };
        var roleNames = new Dictionary<string, string> { ["Admin"] = "管理员", ["Operator"] = "操作员", ["Viewer"] = "查看员" };
        var allPages = AppPages.PageNames;

        var result = roles.Select(role =>
        {
            var saved = _repo.GetRolePermissions(role);
            var defaults = AppPages.GetDefaultPages(Enum.Parse<UserRole>(role));
            var allowed = saved.Count > 0 ? saved : defaults;
            return new RolePermissionsResponse
            {
                Role = role,
                RoleDisplayName = roleNames.GetValueOrDefault(role, role),
                AllowedPages = allowed,
                AllPages = allPages.Select(p => new PageInfo
                {
                    Id = p.Key,
                    Name = p.Value,
                    Allowed = allowed.Contains(p.Key)
                }).ToList()
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>保存某个角色的权限</summary>
    [HttpPost("{role}")]
    public IActionResult SaveRolePermissions(string role, [FromBody] RolePermissionRequest request)
    {
        var validRoles = new[] { "Admin", "Operator", "Viewer" };
        if (!validRoles.Contains(role))
            return BadRequest(new { error = "无效的角色名称" });

        _repo.SaveRolePermissions(role, request.Pages);
        return Ok(new { message = $"角色 {role} 权限已更新" });
    }
}
