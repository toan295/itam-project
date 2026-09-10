using System.Text;
using ITAM.API.Configurations;
using ITAM.API.Data;
using ITAM.API.Helpers;
using ITAM.API.Middlewares.Authorization;
using ITAM.API.Services.Implementations;
using ITAM.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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
