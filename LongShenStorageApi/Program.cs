using LongShenStorageApi.Data;
using LongShenStorageApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

// 禁用JWT默认的claim映射，保留原始claim类型
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// JWT 认证
var jwtKey = builder.Configuration["Jwt:Key"] ?? "HydrogenStarWmsSecretKey2026!@#$%";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "LongShenStorageApi",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "LongShenStorageApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// 控制器
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册数据仓库
builder.Services.AddSingleton<SqlServerRepository>();

// 注册Modbus设备（模拟器/真实PLC根据配置切换）
var useSimulator = builder.Configuration.GetValue<bool>("ModbusTcp:UseSimulator");
if (useSimulator)
{
    builder.Services.AddSingleton<ModbusSimulator>();
    builder.Services.AddSingleton<IModbusDevice>(sp => sp.GetRequiredService<ModbusSimulator>());
}
else
{
    builder.Services.AddSingleton<IModbusDevice, ModbusTcpClientService>();
}
builder.Services.AddHostedService<ModbusSimulatorHostedService>();
// 注册寄存器配置服务
builder.Services.AddSingleton<RegisterConfigService>();

// 文件日志 (输出到运行目录/Logs/)
builder.Services.AddSingleton(sp => new FileLogger(Path.Combine(AppContext.BaseDirectory, "Logs")));

// CORS - 允许所有来源（支持前端跨域访问）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
