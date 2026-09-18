using System.Text;
using FluentValidation;
using ITAM.API.Configurations;
using ITAM.API.Data;
using ITAM.API.Helpers;
using ITAM.API.Middlewares.Authorization;
using ITAM.API.Models.DTOs;
using ITAM.API.Models.DTOs.AssetCategories;
using ITAM.API.Models.DTOs.Assets;
using ITAM.API.Models.DTOs.Departments;
using ITAM.API.Models.DTOs.SoftwareLicenses;
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

// Module Phần mềm & giấy phép (SoftwareLicenses) — Tuần 3-4, Hoàng Đức Tú.
builder.Services.AddScoped<ISoftwareLicenseRepository, SoftwareLicenseRepository>();
builder.Services.AddScoped<ISoftwareLicenseService, SoftwareLicenseService>();
builder.Services.AddScoped<IValidator<CreateSoftwareLicenseDto>, CreateSoftwareLicenseValidator>();
builder.Services.AddScoped<IValidator<UpdateSoftwareLicenseDto>, UpdateSoftwareLicenseValidator>();
builder.Services.AddScoped<IValidator<AssignSoftwareLicenseDto>, AssignSoftwareLicenseValidator>();

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

// Role authorization still uses [Authorize(Roles = "...")].
// Department authorization is implemented with a policy + handler.
builder.Services.AddSingleton<IAuthorizationHandler, DepartmentAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SameDepartmentOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new DepartmentRequirement());
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

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
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
