using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using ITAM.API.Configurations;
using ITAM.API.Data;
using ITAM.API.Helpers;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.AssetCategories;
using ITAM.API.Models.DTOs.AssetAllocations;
using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Models.DTOs.Departments;
using ITAM.API.Models.DTOs.SoftwareLicenses;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Repositories.Implementations;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using ITAM.API.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Lỗi validate DTO tự động (do [ApiController]) cũng phải theo đúng chuẩn
// response { success, data, message, errors } thay vì ProblemDetails mặc định.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => error.ErrorMessage))
            .ToList();

        return new BadRequestObjectResult(
            ApiResponse<object>.Fail("Dữ liệu không hợp lệ.", errors));
    };
});

// Swagger / OpenAPI documentation.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EAIMS API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán JWT token vào đây, không cần gõ chữ Bearer phía trước."
    });

    // Chỉ gắn yêu cầu Bearer token cho action có [Authorize] — tránh Swagger
    // hiển thị nhầm ổ khóa trên /auth/login và /auth/register (không cần token).
    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddSingleton<PasswordResetTokenHelper>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Module Quản lý tài sản CNTT (Assets) — Tuần 3-4, Lâm Toàn.
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IValidator<CreateAssetRequestDto>, CreateAssetRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateAssetRequestDto>, UpdateAssetRequestValidator>();

// UC-04: danh mục dùng chung (loại tài sản, phòng ban) — prerequisite của module Assets.
builder.Services.AddScoped<IAssetCategoryRepository, AssetCategoryRepository>();
builder.Services.AddScoped<IAssetCategoryService, AssetCategoryService>();
builder.Services.AddScoped<IValidator<CreateAssetCategoryRequestDto>, CreateAssetCategoryRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateAssetCategoryRequestDto>, UpdateAssetCategoryRequestValidator>();

builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IValidator<CreateDepartmentRequestDto>, CreateDepartmentRequestValidator>();

builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();

// UC-03: Quản lý người dùng & phân quyền — Admin IT.
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IValidator<CreateUserRequestDto>, CreateUserRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateUserRequestDto>, UpdateUserRequestValidator>();

// Module Phần mềm & giấy phép (SoftwareLicenses) — Tuần 3-4, Hoàng Đức Tú.
builder.Services.AddScoped<ISoftwareLicenseRepository, SoftwareLicenseRepository>();
builder.Services.AddScoped<ISoftwareLicenseService, SoftwareLicenseService>();
builder.Services.AddScoped<IValidator<CreateSoftwareLicenseDto>, CreateSoftwareLicenseValidator>();
builder.Services.AddScoped<IValidator<UpdateSoftwareLicenseDto>, UpdateSoftwareLicenseValidator>();
builder.Services.AddScoped<IValidator<AssignSoftwareLicenseDto>, AssignSoftwareLicenseValidator>();

// Module Nhật ký hệ thống (AuditLogs) — Tuần 6, Hoàng Đức Tú.
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Module Phân bổ & thu hồi tài sản (AssetAllocations) — Tuần 5, Hoàng Đức Tú.
builder.Services.AddScoped<IAssetAllocationRepository, AssetAllocationRepository>();
builder.Services.AddScoped<IAssetAllocationService, AssetAllocationService>();
builder.Services.AddScoped<IValidator<CreateAssetAllocationDto>, CreateAssetAllocationValidator>();
builder.Services.AddScoped<IValidator<ReturnAssetAllocationDto>, ReturnAssetAllocationValidator>();

// Cho phép frontend (chạy ở origin khác — Live Server/static server) gọi API qua fetch().
// Danh sách origin cấu hình trong appsettings (Development): Cors:AllowedOrigins.
const string FrontendCorsPolicy = "FrontendCorsPolicy";
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(corsAllowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Thiếu cấu hình JwtSettings trong appsettings.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Giới hạn tốc độ gọi cho các endpoint xác thực nhạy cảm (đăng nhập, đăng ký, quên/đặt lại mật khẩu)
// — đều là endpoint anonymous, không có JWT nào chặn được việc dò brute-force mật khẩu hay spam sinh
// token. Giới hạn theo IP: tối đa 10 request/phút, vượt quá trả 429 thay vì tiếp tục xử lý.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// Role authorization dùng [Authorize(Roles = "...")]. Phân quyền theo phòng ban cho từng module
// thật (Assets, ...) được xử lý bằng business logic riêng trong Service (vd AssetService), không
// dùng policy chung — policy "SameDepartmentOnly" (Tuần 2, chỉ phục vụ AuthorizationTestController
// minh hoạ) đã được gỡ bỏ cùng handler/requirement liên quan, xác nhận với Hoàng Đức Tú không còn
// module nào cần.
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy
            .WithOrigins(
                "http://127.0.0.1:5500",
                "http://localhost:5500"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Swagger UI is exposed only in Development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("FrontendDev");

app.UseCors(FrontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();

// JWT là stateless — token đã phát hành vẫn hợp lệ tới khi hết hạn tự nhiên (mặc định 60 phút) dù
// tài khoản vừa bị Admin IT khoá (UC-03). Middleware này kiểm tra lại IsActive của user trong token
// trên MỌI request đã xác thực (1 lượt SELECT theo khoá chính, rất rẻ), để việc khoá tài khoản có
// hiệu lực ngay lập tức thay vì phải chờ token tự hết hạn — đúng kỳ vọng nghiệp vụ "khoá là khoá ngay".
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var userId))
        {
            var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
            var isActive = await dbContext.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => (bool?)u.IsActive)
                .FirstOrDefaultAsync();

            if (isActive != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(
                    ApiResponse<object>.Fail("Tài khoản đã bị khoá hoặc không còn tồn tại. Vui lòng đăng nhập lại."),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return;
            }
        }
    }

    await next();
});

app.UseAuthorization();

app.MapControllers();

// Seed development data after the database schema has been created/migrated.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(dbContext);
}

app.Run();
