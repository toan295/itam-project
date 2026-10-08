using ITAM.API.Helpers;

namespace ITAM.Tests.Helpers;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdef12")]
    [InlineData("Eaims@123")]
    [InlineData("mật khẩu 123")]            // chữ cái có dấu vẫn tính là chữ
    [InlineData("a1aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // đúng 72 ký tự
    public void Validate_ValidPassword_ReturnsNull(string password)
    {
        Assert.Null(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Abc123")]                   // 6 ký tự
    [InlineData("Abcdef1")]                  // 7 ký tự
    [InlineData("abcdefgh")]                 // không có số
    [InlineData("12345678")]                 // không có chữ
    [InlineData("!@#$%^&*")]                 // chỉ ký tự đặc biệt
    public void Validate_InvalidPassword_ReturnsMessage(string? password)
    {
        Assert.False(string.IsNullOrWhiteSpace(PasswordPolicy.Validate(password)));
    }

    [Fact]
    public void Validate_LongerThanBCryptLimit_IsRejected()
    {
        // BCrypt cắt âm thầm sau 72 byte -> 2 mật khẩu khác nhau ở phần đuôi sẽ bị coi là một; phải chặn từ đầu.
        Assert.NotNull(PasswordPolicy.Validate("a1" + new string('x', 71)));
    }
}
