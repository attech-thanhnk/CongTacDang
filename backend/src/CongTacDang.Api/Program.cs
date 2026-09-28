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
using CongTacDang.Application.Common.Security;
using CongTacDang.Api.Services;
using CongTacDang.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.ICurrentUserService, HttpCurrentUserService>();

// 1. Database PostgreSQL với SplitQuery tối ưu truy vấn quan hệ nhiều tầng
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<CongTacDangDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// 2. CORS động cho Frontend Next.js
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:3001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddHealthChecks();

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

// 4. Authorization — Policies dựa trên Permission Code và Role
builder.Services.AddAuthorization(options =>
{
    // Đăng ký tự động toàn bộ 14 chính sách dựa trên Permission Codes (kiểm tra claim "perm")
    foreach (var perm in CongTacDang.Application.Common.Security.AppPermissions.All)
    {
        options.AddPolicy(perm, p => p.RequireClaim("perm", perm));
    }

    // Role-based policies bổ trợ
    options.AddPolicy("RequireCaNBo", p => p.RequireRole(AppRoles.CAN_BO));
    options.AddPolicy("RequireBiThuChiBo", p =>
        p.RequireRole(AppRoles.BI_THU_CHI_BO, AppRoles.BAN_THUONG_VU, AppRoles.QUAN_TRI_HE_THONG));
    options.AddPolicy("RequireBanThuongVu", p =>
        p.RequireRole(AppRoles.BAN_THUONG_VU, AppRoles.QUAN_TRI_HE_THONG));
    options.AddPolicy("RequireQuanTriHeTong", p =>
        p.RequireRole(AppRoles.QUAN_TRI_HE_THONG));

    // Composite Policies bảo vệ nghiêm ngặt các endpoint thẩm định, chuẩn y và danh sách hồ sơ
    options.AddPolicy(AppPermissions.PolicyEvaluationsAppraiseOrApprove, p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("perm", AppPermissions.EvaluationsAppraise) ||
            ctx.User.HasClaim("perm", AppPermissions.EvaluationsApprove) ||
            ctx.User.IsInRole(AppRoles.QUAN_TRI_HE_THONG)));

    options.AddPolicy(AppPermissions.PolicyEvaluationsBranchView, p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("perm", AppPermissions.EvaluationsBranchVote) ||
            ctx.User.HasClaim("perm", AppPermissions.EvaluationsAppraise) ||
            ctx.User.HasClaim("perm", AppPermissions.EvaluationsApprove) ||
            ctx.User.IsInRole(AppRoles.QUAN_TRI_HE_THONG)));

    options.AddPolicy(AppPermissions.PolicyManagePeriods, p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("perm", AppPermissions.EvaluationsApprove) ||
            ctx.User.IsInRole(AppRoles.QUAN_TRI_HE_THONG) ||
            ctx.User.IsInRole(AppRoles.BAN_THUONG_VU)));
});

// 5. Đăng ký Repository & Storage Service (MinIO)
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IUserRepository, CongTacDang.Infrastructure.Repositories.UserRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IAttachmentRepository, CongTacDang.Infrastructure.Repositories.AttachmentRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IAuditRepository, CongTacDang.Infrastructure.Repositories.AuditRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IOrganizationRepository, CongTacDang.Infrastructure.Repositories.OrganizationRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IRoleRepository, CongTacDang.Infrastructure.Repositories.RoleRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IRefreshTokenRepository, CongTacDang.Infrastructure.Repositories.RefreshTokenRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IEvaluationRepository, CongTacDang.Infrastructure.Repositories.EvaluationRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.ICollectiveEvaluationRepository, CongTacDang.Infrastructure.Repositories.CollectiveEvaluationRepository>();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.IEvaluationMeetingRepository, CongTacDang.Infrastructure.Repositories.EvaluationMeetingRepository>();

var localStoragePath = builder.Configuration["Storage:Local:Path"]
    ?? Path.Combine(AppContext.BaseDirectory, "uploads");
builder.Services.AddSingleton<CongTacDang.Application.Common.Interfaces.IFileStorageService>(
    new CongTacDang.Infrastructure.Services.LocalFileStorageService(localStoragePath));

// 6. Đăng ký Application Services
builder.Services.AddSingleton<CongTacDang.Application.Common.Interfaces.IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<ICollectiveEvaluationService, CollectiveEvaluationService>();
builder.Services.AddScoped<IReportService, CongTacDang.Infrastructure.Services.ReportService>();

// 7. Controllers & Swagger với hỗ trợ JWT Bearer Authorization
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(x => !string.IsNullOrWhiteSpace(x.ErrorMessage) ? x.ErrorMessage : x.Exception?.Message ?? "Dữ liệu không hợp lệ"))
                .ToList();

            var message = errors.Count > 0 ? string.Join("; ", errors) : "Dữ liệu gửi lên chưa hợp lệ.";
            var response = CongTacDang.Application.Common.Models.ApiResponse.Fail(message, errors);
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
    });
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
app.UseMiddleware<CongTacDang.Api.Middlewares.GlobalExceptionMiddleware>();

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
app.MapHealthChecks("/healthz");
app.MapControllers();

app.Run();
