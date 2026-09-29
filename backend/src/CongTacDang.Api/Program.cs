using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using CongTacDang.Application.Services;
using CongTacDang.Application.Common.Security;
using CongTacDang.Api.Services;
using CongTacDang.Api.Extensions;
using CongTacDang.Api.Logging;

var builder = WebApplication.CreateBuilder(args);

// Log có cấu trúc ra console và file xoay vòng theo ngày (cấu hình Logging:Console:Json, Logging:File:*)
builder.Logging.AddConfiguredLogging(builder.Configuration, builder.Environment);

builder.Configuration.ValidateRequiredConfiguration();
builder.Services.AddConfiguredForwardedHeaders(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CongTacDang.Application.Common.Interfaces.ICurrentUserService, HttpCurrentUserService>();

// 1. Database PostgreSQL, repository, Unit of Work và DB health check
builder.Services.AddPersistence(builder.Configuration);

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
builder.Services.AddSecurityRateLimiting();

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

// 4. Authorization — policy theo mã quyền, đánh giá từ IPermissionResolver (Api/Extensions/AuthorizationExtensions.cs)
builder.Services.AddPermissionAuthorization();

// 5. Đăng ký Storage Service
var localStoragePath = builder.Configuration["Storage:Local:Path"]
    ?? Path.Combine(AppContext.BaseDirectory, "uploads");
builder.Services.AddSingleton<CongTacDang.Application.Common.Interfaces.IFileStorageService>(
    new CongTacDang.Infrastructure.Services.LocalFileStorageService(localStoragePath));

// 6. Đăng ký Application Services
builder.Services.AddSingleton<CongTacDang.Application.Common.Interfaces.IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddAccountServices(builder.Configuration); // Tài khoản, phiên, nhật ký đăng nhập (Api/Extensions/AccountExtensions.cs)
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<ICollectiveEvaluationService, CollectiveEvaluationService>();
builder.Services.AddScoped<IReportService, CongTacDang.Infrastructure.Services.ReportService>();
builder.Services.AddDocumentGeneration(builder.Configuration);
builder.Services.AddScoped<IReportAccessService, ReportAccessService>();
builder.Services.AddOrganizationModel(builder.Configuration); // Chức vụ, kiêm nhiệm, thẩm quyền suy ra (Api/Extensions/OrganizationExtensions.cs)
builder.Services.AddEvaluationWorkflow(); // Luồng đánh giá theo cấu hình kỳ (Api/Extensions/EvaluationExtensions.cs)

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
        Title = "Hệ thống đánh giá cán bộ Đảng bộ",
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
});

var app = builder.Build();

// 8. Migration và seed dữ liệu theo cấu hình
await app.ApplyPersistenceAsync();

// 9. Middleware pipeline
app.UseMiddleware<CongTacDang.Api.Middlewares.CorrelationIdMiddleware>();
app.UseForwardedHeaders();
app.UseConfiguredSecurityHeaders();
app.UseMiddleware<CongTacDang.Api.Middlewares.GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CongTacDang API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication(); // Phải đứng trước UseAuthorization
app.UsePasswordChangeEnforcement(); // Chặn API khi còn mật khẩu tạm (Api/Extensions/SecurityExtensions.cs)
app.UseAuthorization();
app.MapHealthChecks("/healthz");
app.MapControllers();

app.Run();

/// <summary>Lớp Program công khai để test tích hợp dùng WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program { }
