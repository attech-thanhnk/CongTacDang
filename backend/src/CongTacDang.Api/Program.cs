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

// 3. Dang ky Repository & Storage Adapter (Infrastructure Layer)
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IUserRepository, CongTacDang.Infrastructure.Repositories.UserRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IAttachmentRepository, CongTacDang.Infrastructure.Repositories.AttachmentRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IOrganizationRepository, CongTacDang.Infrastructure.Repositories.OrganizationRepository>();

var storageProvider = builder.Configuration["Storage:Provider"] ?? "local";
if (storageProvider.Equals("minio", StringComparison.OrdinalIgnoreCase))
{
    var minioOptions = new CongTacDang.Infrastructure.Services.MinioStorageOptions
    {
        Endpoint = builder.Configuration["Storage:Minio:Endpoint"] ?? "localhost:9000",
        BucketName = builder.Configuration["Storage:Minio:BucketName"] ?? "congtacdang-files",
        AccessKey = builder.Configuration["Storage:Minio:AccessKey"] ?? "minioadmin",
        SecretKey = builder.Configuration["Storage:Minio:SecretKey"] ?? "minioadmin",
        UseSsl = bool.TryParse(builder.Configuration["Storage:Minio:UseSsl"], out var ssl) && ssl
    };
    builder.Services.AddSingleton<CongTacDang.Application.Common.Interfaces.IFileStorageService>(new CongTacDang.Infrastructure.Services.MinioFileStorageService(minioOptions));
}
else
{
    var storagePath = System.IO.Path.Combine(builder.Environment.ContentRootPath, "storage", "attachments");
    builder.Services.AddSingleton<CongTacDang.Application.Common.Interfaces.IFileStorageService>(new CongTacDang.Infrastructure.Services.LocalFileStorageService(storagePath));
}

// 4. Dang ky Services (Application Layer)
builder.Services.AddScoped<CongTacDang.Application.Services.IUserService, CongTacDang.Application.Services.UserService>();
builder.Services.AddScoped<CongTacDang.Application.Services.IOrganizationService, CongTacDang.Application.Services.OrganizationService>();
builder.Services.AddScoped<CongTacDang.Application.Services.IAttachmentService, CongTacDang.Application.Services.AttachmentService>();
builder.Services.AddScoped<CongTacDang.Application.Services.IReportService, CongTacDang.Infrastructure.Services.ReportService>();

// 5. Controllers & Swagger
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
