using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CongTacDang.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Database PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("Default") ?? builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<CongTacDangDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. CORS cho Frontend Next.js
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Hệ thống Đánh giá Cán bộ Đảng bộ ATTECH",
        Version = "v1",
        Description = "API phục vụ quy trình đánh giá định kỳ hằng quý theo Hướng dẫn 03-HD/TVĐU"
    });
});

var app = builder.Build();

// 4. Khởi tạo CSDL & Dữ liệu mẫu (Auto Migration & Seed)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        await DataSeeder.SeedAsync(db);
    }
    catch (System.Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger.LogError(ex, "Chưa kết nối được tới PostgreSQL để khởi tạo dữ liệu.");
    }
}

// 5. Middleware pipeline
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Đảng bộ ATTECH API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
