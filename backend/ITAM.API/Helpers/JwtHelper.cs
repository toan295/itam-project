using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ITAM.API.Configurations;
using ITAM.API.Models.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ITAM.API.Helpers;

public class JwtHelper
{
    // Phiên đăng nhập không tự hết hạn theo thời gian ngắn: mặc định 10 năm (chỉ mất khi đăng xuất hoặc tài khoản bị khoá).
    private const int DefaultExpiryMinutes = 5_256_000;
    private readonly JwtSettings _jwtSettings;

    public JwtHelper(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : DefaultExpiryMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.Name),
            new Claim("DepartmentId", user.DepartmentId.ToString()),
            new Claim(ServerInstance.ClaimType, ServerInstance.Id)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
