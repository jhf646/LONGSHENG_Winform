using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 用户认证与权限管理
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SqlServerRepository _repo;
    private readonly IConfiguration _config;

    public AuthController(SqlServerRepository repo, IConfiguration config)
    {
        _repo = repo;
        _config = config;
    }

    /// <summary>用户登录</summary>
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "请输入用户名和密码" });

        var user = _repo.GetUserByUsername(request.Username);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "用户名或密码错误" });

        if (!_repo.VerifyPassword(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "用户名或密码错误" });

        var token = GenerateToken(user);
        return Ok(new LoginResponse
        {
            Token = token,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            UserId = user.Id
        });
    }

    /// <summary>获取当前用户信息（需要登录）</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var displayName = User.FindFirst("DisplayName")?.Value;

        return Ok(new
        {
            userId = userIdClaim,
            username,
            role,
            displayName
        });
    }

    /// <summary>管理员：创建新用户</summary>
    [HttpPost("create-user")]
    [Authorize(Roles = "Admin")]
    public IActionResult CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "用户名和密码不能为空" });

        if (_repo.GetUserByUsername(request.Username) is not null)
            return BadRequest(new { error = "用户名已存在" });

        _repo.CreateUser(new User
        {
            Username = request.Username,
            PasswordHash = HashPassword(request.Password),
            Role = request.Role,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username : request.DisplayName,
            IsActive = true
        });

        return Ok(new { message = "用户创建成功" });
    }

    /// <summary>管理员：获取所有用户</summary>
    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public ActionResult<List<User>> GetAllUsers()
    {
        var users = _repo.GetAllUsers().Select(u => new
        {
            u.Id, u.Username,
            Role = u.Role.ToString(), // 枚举转字符串
            u.DisplayName, u.IsActive, u.CreatedAt
        }).ToList();
        return Ok(users);
    }

    /// <summary>管理员：更新用户信息</summary>
    [HttpPut("users/{id}")]
    [Authorize(Roles = "Admin")]
    public IActionResult UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = _repo.GetUserById(id);
        if (user is null) return NotFound(new { error = "用户不存在" });

        user.DisplayName = request.DisplayName ?? user.DisplayName;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        _repo.UpdateUser(user);
        return Ok(new { message = "用户已更新" });
    }

    /// <summary>管理员：重置用户密码</summary>
    [HttpPut("users/{id}/password")]
    [Authorize(Roles = "Admin")]
    public IActionResult ResetPassword(Guid id, [FromBody] PasswordResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "密码不能为空" });

        var user = _repo.GetUserById(id);
        if (user is null) return NotFound(new { error = "用户不存在" });

        user.PasswordHash = HashPassword(request.NewPassword);
        _repo.UpdateUser(user);
        return Ok(new { message = "密码已重置" });
    }

    /// <summary>管理员：删除用户</summary>
    [HttpDelete("users/{id}")]
    [Authorize(Roles = "Admin")]
    public IActionResult DeleteUser(Guid id)
    {
        var user = _repo.GetUserById(id);
        if (user is null) return NotFound(new { error = "用户不存在" });

        _repo.DeleteUser(id);
        return Ok(new { message = "用户已删除" });
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _config["Jwt:Key"] ?? "LongShenStorageSystemSecretKey2026!@#$%"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("role", user.Role.ToString()), // JWT标准role声明
            new Claim("DisplayName", user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "LongShenStorageApi",
            audience: _config["Jwt:Audience"] ?? "LongShenStorageApp",
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "LongShenSalt2026"));
        return Convert.ToBase64String(bytes);
    }
}
