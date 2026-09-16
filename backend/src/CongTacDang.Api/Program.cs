using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using CongTacDang.Application.Services;
using CongTacDang.Api.Services;
using CongTacDang.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Database PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
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

// 3. JWT Authentication — đọc token từ HttpOnly Cookie (ưu tiên), fallback sang Authorization header
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret chưa được cấu hình.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CongTacDang.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CongTacDang.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Đọc JWT từ HttpOnly Cookie trước, fallback sang Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue("auth_token", out var cookieToken))
                    ctx.Token = cookieToken;
                return Task.CompletedTask;
            }
        };
    });

// 4. Authorization — 4 Policy theo ma trận phân quyền 03-HD/TVĐU
builder.Services.AddAuthorization(options =>
{
    // Bất kỳ cán bộ đã đăng nhập
    options.AddPolicy("RequireCaNBo", p => p.RequireRole(AppRoles.CAN_BO));

    // Bí thư / Phó Bí thư Chi bộ trở lên
    options.AddPolicy("RequireBiThuChiBo", p =>
        p.RequireRole(AppRoles.BI_THU_CHI_BO, AppRoles.BAN_THUONG_VU, AppRoles.QUAN_TRI_HE_THONG));

    // Ban Thường vụ Đảng ủy trở lên
    options.AddPolicy("RequireBanThuongVu", p =>
        p.RequireRole(AppRoles.BAN_THUONG_VU, AppRoles.QUAN_TRI_HE_THONG));

    // Quản trị hệ thống — toàn quyền
    options.AddPolicy("RequireQuanTriHeTong", p =>
        p.RequireRole(AppRoles.QUAN_TRI_HE_THONG));
});

// 5. Đăng ký Repository & Storage Service (MinIO)
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IUserRepository, CongTacDang.Infrastructure.Repositories.UserRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IAttachmentRepository, CongTacDang.Infrastructure.Repositories.AttachmentRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IOrganizationRepository, CongTacDang.Infrastructure.Repositories.OrganizationRepository>();

var minioOptions = new CongTacDang.Infrastructure.Services.MinioStorageOptions
{
    Endpoint = builder.Configuration["Storage:Minio:Endpoint"] ?? "localhost:9000",
    BucketName = builder.Configuration["Storage:Minio:BucketName"] ?? "congtacdang-files",
    AccessKey = builder.Configuration["Storage:Minio:AccessKey"] ?? "minioadmin",
    SecretKey = builder.Configuration["Storage:Minio:SecretKey"] ?? "minioadmin",
    UseSsl = bool.TryParse(builder.Configuration["Storage:Minio:UseSsl"], out var ssl) && ssl,
    PublicEndpoint = builder.Configuration["Storage:Minio:PublicEndpoint"]
};
builder.Services.AddSingleton<CongTacDang.Application.Common.Interfaces.IFileStorageService>(
    new CongTacDang.Infrastructure.Services.MinioFileStorageService(minioOptions));

// 6. Đăng ký Application Services
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IReportService, CongTacDang.Infrastructure.Services.ReportService>();

// 7. Controllers & Swagger với hỗ trợ JWT Bearer Authorization
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hệ thống Đánh giá Cán bộ Đảng bộ ATTECH",
        Version = "v1",
        Description = "API phục vụ quy trình đánh giá định kỳ hằng quý theo Hướng dẫn 03-HD/TVĐU"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token (không cần tiền tố 'Bearer ')"
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 8. Khởi tạo CSDL và Seed dữ liệu mẫu khi khởi động
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        await DataSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger.LogError(ex, "Chưa kết nối được tới PostgreSQL để khởi tạo dữ liệu.");
    }
}

// 9. Middleware pipeline
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
app.UseAuthentication(); // Phải đứng trước UseAuthorization
app.UseAuthorization();
app.MapControllers();

app.Run();
