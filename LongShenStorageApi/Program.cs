using LongShenStorageApi.Data;

var builder = WebApplication.CreateBuilder(args);

// 控制器
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册数据仓库
builder.Services.AddSingleton<SqlServerRepository>();

// CORS - 允许前端跨域访问
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// Swagger 中间件
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// 首页重定向到 Swagger UI
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
