using System.Net;
using System.Net.Mail;
using System.Text;
using ITAM.API.Configurations;
using ITAM.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ITAM.API.Services.Implementations;

// Gửi email qua SMTP bằng System.Net.Mail (có sẵn trong .NET, không thêm thư viện).
// Chưa cấu hình SMTP: ở Development ghi email ra App_Data/outbox để xem thử luồng; môi trường khác trả "disabled"
// (không ghi email chứa liên kết xác nhận ra đĩa ở production).
public class SmtpEmailSender : IEmailSender
{
    private const int SmtpTimeoutMs = 15_000;

    private readonly EmailSettings _settings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> settings, IHostEnvironment environment, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(
        string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsSmtpConfigured)
        {
            return await SaveToOutboxOrDisabledAsync(toAddress, subject, htmlBody, cancellationToken);
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName, Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = htmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
            };
            message.To.Add(new MailAddress(toAddress));

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Timeout = SmtpTimeoutMs,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };
            if (!string.IsNullOrWhiteSpace(_settings.UserName))
            {
                client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Đã gửi email '{Subject}' tới {To}", subject, toAddress);
            return new EmailSendResult(true, "smtp");
        }
        catch (Exception ex) when (ex is SmtpException or FormatException or InvalidOperationException or OperationCanceledException or IOException)
        {
            _logger.LogWarning(ex, "Gửi email tới {To} thất bại", toAddress);
            return new EmailSendResult(false, "smtp", $"Gửi email thất bại: {ex.Message}");
        }
    }

    private async Task<EmailSendResult> SaveToOutboxOrDisabledAsync(
        string toAddress, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogWarning("Chưa cấu hình SMTP (Email:Enabled/Host/FromAddress) — không gửi được email tới {To}", toAddress);
            return new EmailSendResult(false, "disabled", "Chưa cấu hình máy chủ email (SMTP) nên không gửi được email.");
        }

        var folder = Path.Combine(_environment.ContentRootPath, "App_Data", "outbox");
        Directory.CreateDirectory(folder);

        var safeTo = new string(toAddress.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '@' ? c : '_').ToArray());
        var path = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}_{safeTo}.html");
        var content = $"<!-- To: {WebUtility.HtmlEncode(toAddress)} | Subject: {WebUtility.HtmlEncode(subject)} -->\n{htmlBody}";
        await File.WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken);

        _logger.LogWarning("Chưa cấu hình SMTP — email tới {To} được lưu thử tại {Path}", toAddress, path);
        return new EmailSendResult(true, "outbox", $"Chưa cấu hình SMTP: email được lưu thử tại App_Data/outbox, chưa gửi tới hộp thư thật.");
    }
}
