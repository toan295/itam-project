namespace ITAM.API.Models.DTOs;

public class ForgotPasswordResponseDto
{
    public string Message { get; set; } = null!;

    // Dự án chưa có hạ tầng gửi email (SMTP) — ở môi trường Development, Controller trả thẳng token
    // vào đây để tiện demo/test luồng reset mà không cần email thật. Ở Production, Controller LUÔN
    // đặt về null (token phải được gửi qua email, không bao giờ lộ qua response).
    public string? DevOnlyResetToken { get; set; }
}
