namespace ITAM.API.Services.Interfaces;

// Mode: "smtp" (đã gửi qua SMTP) | "outbox" (chỉ Development: lưu file HTML, chưa gửi thật) | "disabled" (chưa cấu hình SMTP).
public sealed record EmailSendResult(bool Sent, string Mode, string? Detail = null);

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
