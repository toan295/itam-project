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
using ITAM.API.Models.DTOs.Forecasts;
using ITAM.API.Models.DTOs.Disposals;
using ITAM.API.Models.DTOs.Employees;
using ITAM.API.Models.DTOs.Lifecycle;
using ITAM.API.Models.DTOs.MaintenanceTickets;
using ITAM.API.Models.DTOs.SoftwareLicenses;
using ITAM.API.Models.DTOs.Users;
using ITAM.API.Repositories.Implementations;
using ITAM.API.Repositories.Interfaces;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using ITAM.API.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<ITAM.API.Filters.AuditActionFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<ITAM.API.Filters.AuditActionFilter>());
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
    // hiển thị nhầm ổ khóa trên /auth/login (không cần token).
    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
// Ngưỡng mặc định sai (vd 0) sẽ làm MỌI lần phân tích vòng đời/dự báo ném lỗi — kiểm tra ngay lúc khởi động.
builder.Services.AddOptions<LifecycleOptions>()
    .Bind(builder.Configuration.GetSection(LifecycleOptions.SectionName))
    .Validate(
        o => o.DefaultMaxAgeYears is >= 1 and <= 30 && o.DefaultMaxFailureCount is >= 1 and <= 100,
        "Lifecycle:DefaultMaxAgeYears phải từ 1 đến 30 và Lifecycle:DefaultMaxFailureCount phải từ 1 đến 100.")
    .ValidateOnStart();
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Mã hoá mật khẩu mặc định trong DB (Data Protection). Production PHẢI đặt DataProtection:KeysPath tới thư mục bền vững
// (volume) — nếu không, mỗi lần khởi động lại (đặc biệt trên Linux/container) khoá bị sinh mới và giá trị đã mã hoá
// không giải mã được nữa.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("EAIMS");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
else if (!builder.Environment.IsDevelopment())
{
    Console.Error.WriteLine("CẢNH BÁO: chưa cấu hình DataProtection:KeysPath — khoá mã hoá có thể mất khi khởi động lại.");
}
builder.Services.AddSingleton<DefaultPasswordCipher>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();
builder.Services.AddScoped<IExclusiveSection, ExclusiveSection>(); // khoá dòng cho các thao tác "kiểm tra rồi ghi".

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
builder.Services.AddScoped<IValidator<UpdateDepartmentRequestDto>, UpdateDepartmentRequestValidator>();

builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();

// UC-03: Quản lý người dùng & phân quyền — Admin IT.
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();

// Danh mục nhân viên nhận tài sản + mức độ khẩn phiếu bảo trì.
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IValidator<UpsertEmployeeRequestDto>, UpsertEmployeeRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateTicketPriorityRequestDto>, UpdateTicketPriorityRequestValidator>();

// Module Thanh lý tài sản (Technician kiểm tra -> đề xuất -> Manager duyệt -> Admin IT thực hiện).
builder.Services.AddScoped<IDisposalStatusRepository, DisposalStatusRepository>();
builder.Services.AddScoped<IDisposalRequestRepository, DisposalRequestRepository>();
builder.Services.AddScoped<IDisposalStatusService, DisposalStatusService>();
builder.Services.AddScoped<IDisposalRequestService, DisposalRequestService>();
builder.Services.AddScoped<IValidator<UpsertDisposalStatusRequestDto>, UpsertDisposalStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDisposalRequestDto>, CreateDisposalRequestValidator>();
builder.Services.AddScoped<IValidator<ProposeDisposalRequestDto>, ProposeDisposalRequestValidator>();
builder.Services.AddScoped<IValidator<ReviewDisposalRequestDto>, ReviewDisposalRequestValidator>();
builder.Services.AddScoped<IValidator<CompleteDisposalRequestDto>, CompleteDisposalRequestValidator>();
builder.Services.AddScoped<IDefaultPasswordService, DefaultPasswordService>();
builder.Services.AddScoped<IValidator<SetDefaultPasswordRequestDto>, SetDefaultPasswordRequestValidator>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IValidator<CreateUserRequestDto>, CreateUserRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateUserRequestDto>, UpdateUserRequestValidator>();

// Module Phần mềm & giấy phép (SoftwareLicenses) — Tuần 3-4, Hoàng Đức Tú.
builder.Services.AddScoped<ISoftwareLicenseRepository, SoftwareLicenseRepository>();
builder.Services.AddScoped<ISoftwareLicenseService, SoftwareLicenseService>();
builder.Services.AddScoped<IValidator<CreateSoftwareLicenseDto>, CreateSoftwareLicenseValidator>();
builder.Services.AddScoped<IValidator<UpdateSoftwareLicenseDto>, UpdateSoftwareLicenseValidator>();
builder.Services.AddScoped<IValidator<AssignSoftwareLicenseDto>, AssignSoftwareLicenseValidator>();

// Module Bảo trì & hỗ trợ kỹ thuật (MaintenanceTickets) — Tuần 5-6, Lâm Toàn.
builder.Services.AddScoped<IMaintenanceTicketRepository, MaintenanceTicketRepository>();
builder.Services.AddScoped<IMaintenanceTicketService, MaintenanceTicketService>();
builder.Services.AddScoped<IValidator<CreateMaintenanceTicketRequestDto>, CreateMaintenanceTicketRequestValidator>();
builder.Services.AddScoped<IValidator<AssignTechnicianRequestDto>, AssignTechnicianRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateTicketStatusRequestDto>, UpdateTicketStatusRequestValidator>();

// Module Nhật ký hệ thống (AuditLogs) — Tuần 7, Hoàng Đức Tú.
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Module Phân bổ & thu hồi tài sản (AssetAllocations) — Tuần 5, Hoàng Đức Tú.
builder.Services.AddScoped<IAssetAllocationRepository, AssetAllocationRepository>();
builder.Services.AddScoped<IAssetAllocationService, AssetAllocationService>();
builder.Services.AddScoped<IValidator<CreateAssetAllocationDto>, CreateAssetAllocationValidator>();
builder.Services.AddScoped<IValidator<ReturnAssetAllocationDto>, ReturnAssetAllocationValidator>();

// Module Vòng đời tài sản (UC-16) — Tuần 7 (lần 2).
builder.Services.AddScoped<ILifecycleRepository, LifecycleRepository>();
builder.Services.AddScoped<ILifecycleAnalysisService, LifecycleAnalysisService>();
builder.Services.AddScoped<IValidator<UpsertLifecyclePolicyRequestDto>, UpsertLifecyclePolicyRequestValidator>();

// Module Dự báo ngân sách — Tuần 7 (lần 2), Lâm Toàn.
builder.Services.AddScoped<IBudgetForecastRepository, BudgetForecastRepository>();
builder.Services.AddScoped<IBudgetForecastService, BudgetForecastService>();
builder.Services.AddScoped<IValidator<GenerateForecastRequestDto>, GenerateForecastRequestValidator>();
builder.Services.AddScoped<IValidator<UpsertReferencePriceRequestDto>, UpsertReferencePriceRequestValidator>();

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

// HMAC-SHA256 cần khoá đủ dài; khoá ngắn/đoán được cho phép giả mạo token (đăng nhập thành Admin IT).
// Production phải cấp qua biến môi trường/secret manager, không commit vào repo.
if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || Encoding.UTF8.GetByteCount(jwtSettings.Secret) < 32)
{
    throw new InvalidOperationException("JwtSettings:Secret phải dài tối thiểu 32 byte.");
}

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

// Giới hạn tốc độ gọi cho endpoint xác thực — endpoint anonymous, không có JWT nào chặn được việc dò
// mật khẩu hàng loạt (đặc biệt khi tài khoản mới dùng chung mật khẩu mặc định). Giới hạn theo IP:
// tối đa 30 request/phút, vượt quá trả 429. Việc dò mật khẩu cho TỪNG tài khoản được chặn riêng, chặt hơn, ở
// LoginAttemptTracker (5 lần sai/cặp IP+email); giới hạn này chỉ chống flood để một người sai mật khẩu không
// khoá cả văn phòng dùng chung một IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
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

var app = builder.Build();

// Swagger UI is exposed only in Development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Header bảo mật cơ bản cho mọi response của API.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Cache-Control"] = "no-store"; // dữ liệu nghiệp vụ/nhạy cảm không được cache ở trình duyệt/proxy.
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

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
            var state = await dbContext.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.MustChangePassword, RoleName = u.Role.Name, u.DepartmentId })
                .FirstOrDefaultAsync();

            // Token mang sẵn role/phòng ban lúc đăng nhập. Nếu Admin đã đổi vai trò/phòng ban của user, token cũ
            // (sống tới 60 phút) vẫn giữ quyền cũ -> từ chối để buộc đăng nhập lại và nhận quyền đúng.
            var tokenRole = context.User.FindFirstValue(ClaimTypes.Role);
            var tokenDept = context.User.FindFirstValue("DepartmentId");
            if (state is { IsActive: true }
                && (!string.Equals(tokenRole, state.RoleName, StringComparison.Ordinal)
                    || tokenDept != state.DepartmentId.ToString()))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(
                    ApiResponse<object>.Fail("Quyền hoặc phòng ban của tài khoản đã thay đổi. Vui lòng đăng nhập lại."),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return;
            }

            // Đang dùng mật khẩu mặc định -> chỉ cho gọi /auth/* (đổi mật khẩu, xem hồ sơ) cho tới khi đổi xong.
            if (state is { IsActive: true, MustChangePassword: true }
                && !context.Request.Path.StartsWithSegments("/api/v1/auth"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(
                    ApiResponse<object>.Fail("Bạn đang dùng mật khẩu mặc định. Vui lòng đổi mật khẩu trước khi tiếp tục."),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return;
            }

            if (state?.IsActive != true)
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

// Việc khởi tạo dữ liệu bắt buộc, chạy ở MỌI môi trường (cần schema đã migrate):
//  - tạo Admin IT đầu tiên nếu cấu hình Bootstrap:* (xem AdminBootstrapper);
//  - mã hoá mật khẩu mặc định cũ còn lưu dạng văn bản thuần.
using (var startupScope = app.Services.CreateScope())
{
    var startupDb = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = startupScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await AdminBootstrapper.RunAsync(startupDb, builder.Configuration, startupLogger);
    await startupScope.ServiceProvider.GetRequiredService<IDefaultPasswordService>().EnsureEncryptedAsync();
}

// Seed development data after the database schema has been created/migrated.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(dbContext, scope.ServiceProvider.GetRequiredService<DefaultPasswordCipher>());
}

app.Run();
