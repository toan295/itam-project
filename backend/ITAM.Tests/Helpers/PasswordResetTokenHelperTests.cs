using ITAM.API.Configurations;
using ITAM.API.Helpers;
using ITAM.API.Models.Entities;
using Microsoft.Extensions.Options;

namespace ITAM.Tests.Helpers;

public class PasswordResetTokenHelperTests
{
    private static readonly JwtSettings Settings = new()
    {
        Secret = "unit-test-secret-key-not-used-in-production-1234567890",
        Issuer = "EAIMS.Tests",
        Audience = "EAIMS.Tests.Client",
        ExpiryMinutes = 60,
    };

    private static PasswordResetTokenHelper CreateHelper(TimeSpan? lifetime = null) =>
        new(Options.Create(Settings), lifetime);

    private static User CreateUser(int id = 1, string passwordHash = "hash-v1") =>
        new() { Id = id, FullName = "Test User", Email = "user@eaims.local", PasswordHash = passwordHash };

    [Fact]
    public void GenerateThenParse_ValidToken_ReturnsMatchingUserIdAndFingerprint()
    {
        var helper = CreateHelper();
        var user = CreateUser(id: 42, passwordHash: "hash-v1");

        var token = helper.GenerateToken(user);
        var parsed = helper.TryParse(token, out var payload, out var error);

        Assert.True(parsed);
        Assert.Equal("", error);
        Assert.Equal(42, payload!.UserId);
        Assert.Equal(PasswordResetTokenHelper.ComputeFingerprint("hash-v1"), payload.PasswordFingerprint);
    }

    [Fact]
    public void TryParse_TamperedSignature_Fails()
    {
        var helper = CreateHelper();
        var token = helper.GenerateToken(CreateUser());

        var parts = token.Split('.');
        var tampered = parts[0] + "." + new string(parts[1].Reverse().ToArray());

        var parsed = helper.TryParse(tampered, out var payload, out var error);

        Assert.False(parsed);
        Assert.Null(payload);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("only.one.dot.too.many")]
    [InlineData("!!!invalid-base64!!!.also-invalid")]
    public void TryParse_MalformedToken_FailsGracefully(string malformed)
    {
        var helper = CreateHelper();

        var parsed = helper.TryParse(malformed, out var payload, out var error);

        Assert.False(parsed);
        Assert.Null(payload);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParse_NullToken_Fails()
    {
        var helper = CreateHelper();

        var parsed = helper.TryParse(null, out var payload, out var error);

        Assert.False(parsed);
        Assert.Null(payload);
    }

    [Fact]
    public void TryParse_ExpiredToken_Fails()
    {
        // Lifetime âm -> token coi như đã hết hạn ngay khi vừa tạo, không cần chờ thời gian thật.
        var helper = CreateHelper(TimeSpan.FromMinutes(-1));
        var token = helper.GenerateToken(CreateUser());

        var parsed = helper.TryParse(token, out var payload, out var error);

        Assert.False(parsed);
        Assert.Null(payload);
        Assert.Contains("hết hạn", error);
    }

    [Fact]
    public void TryParse_TokenFromDifferentSecret_Fails()
    {
        var helperA = CreateHelper();
        var otherSettings = new JwtSettings
        {
            Secret = "a-completely-different-secret-key-0987654321",
            Issuer = Settings.Issuer,
            Audience = Settings.Audience,
            ExpiryMinutes = Settings.ExpiryMinutes,
        };
        var helperB = new PasswordResetTokenHelper(Options.Create(otherSettings));

        var token = helperA.GenerateToken(CreateUser());
        var parsed = helperB.TryParse(token, out var payload, out var error);

        Assert.False(parsed);
        Assert.Null(payload);
    }

    [Fact]
    public void ComputeFingerprint_DifferentPasswordHashes_ProduceDifferentFingerprints()
    {
        var fingerprint1 = PasswordResetTokenHelper.ComputeFingerprint("hash-v1");
        var fingerprint2 = PasswordResetTokenHelper.ComputeFingerprint("hash-v2");

        Assert.NotEqual(fingerprint1, fingerprint2);
    }

    [Fact]
    public void ComputeFingerprint_SamePasswordHash_IsDeterministic()
    {
        var fingerprint1 = PasswordResetTokenHelper.ComputeFingerprint("hash-v1");
        var fingerprint2 = PasswordResetTokenHelper.ComputeFingerprint("hash-v1");

        Assert.Equal(fingerprint1, fingerprint2);
    }

    [Fact]
    public void GenerateToken_AfterPasswordChanged_OldTokenFingerprintNoLongerMatchesNewHash()
    {
        // Mô phỏng đúng kịch bản bảo mật cốt lõi: token cấp trước khi đổi mật khẩu phải tự
        // vô hiệu hoá sau khi mật khẩu đã thực sự đổi (không cần lưu trạng thái "đã dùng" trong DB).
        var helper = CreateHelper();
        var user = CreateUser(passwordHash: "old-hash");
        var token = helper.GenerateToken(user);

        user.PasswordHash = "new-hash-after-reset";

        helper.TryParse(token, out var payload, out _);

        Assert.NotEqual(PasswordResetTokenHelper.ComputeFingerprint(user.PasswordHash), payload!.PasswordFingerprint);
    }
}
