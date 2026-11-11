using System.Globalization;

namespace ITAM.API.Helpers;

// Dựng email "Kích hoạt tài khoản" gửi cho nhân viên sau khi Admin IT tạo tài khoản. Cấu trúc: tiêu đề, lời chào, thông báo,
// thông tin tài khoản, nút kích hoạt, hướng dẫn và lưu ý bảo mật. Email KHÔNG chứa mật khẩu — nhân viên tự đặt ở trang kích hoạt.
// Mọi dữ liệu người dùng đều được HTML-encode (họ tên/phòng ban do người khác nhập, không được chèn được thẻ HTML).
public static class AccountActivationEmail
{
    public sealed record Content(string Subject, string HtmlBody);

    private static string Esc(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");

    public static Content Build(
        string fullName,
        string? employeeCode,
        string loginEmail,
        string? phone,
        string roleLabel,
        string departmentName,
        string activateUrl,
        DateTime expiresAtUtc,
        int validDays,
        string reason,
        bool isEmployee)
    {
        var isReset = reason == "Reset";
        var isResend = reason == "Resent";
        var subject = isReset
            ? "[EAIMS] Đặt lại mật khẩu tài khoản của bạn"
            : isResend ? "[EAIMS] Nhắc kích hoạt tài khoản của bạn" : "[EAIMS] Kích hoạt tài khoản của bạn";
        var heading = isReset ? "Đặt lại mật khẩu EAIMS" : "Kích hoạt tài khoản EAIMS";
        var buttonText = isReset ? "Đặt mật khẩu mới" : "Kích hoạt tài khoản";
        var expires = expiresAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
        var url = Esc(activateUrl);

        const string cellLabel = "padding:8px;border:1px solid #e5e7eb;background:#f9fafb;width:36%";
        const string cellValue = "padding:8px;border:1px solid #e5e7eb";
        var info = new List<(string Label, string Value)>();
        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            info.Add(("Mã nhân viên", employeeCode));
        }

        info.Add(("Họ và tên", fullName));
        info.Add(("Tên đăng nhập (email)", loginEmail));
        if (!string.IsNullOrWhiteSpace(phone))
        {
            info.Add(("Số điện thoại", phone));
        }

        info.Add(("Vai trò", roleLabel));
        info.Add(("Phòng ban", departmentName));
        var rows = string.Concat(info.Select(f =>
            $"<tr><td style=\"{cellLabel}\">{Esc(f.Label)}</td><td style=\"{cellValue}\"><strong>{Esc(f.Value)}</strong></td></tr>"));

        var purpose = isEmployee
            ? "để bạn xem các thiết bị được cấp và gửi yêu cầu bảo trì khi thiết bị gặp sự cố"
            : "để bạn làm việc với hệ thống theo vai trò được giao";
        var notice = isReset
            ? "Quản trị viên đã đặt lại mật khẩu tài khoản EAIMS của bạn. Mật khẩu cũ đã hết hiệu lực — bạn cần đặt mật khẩu mới để tiếp tục sử dụng."
            : isResend
                ? "Quản trị viên gửi lại liên kết kích hoạt vì tài khoản của bạn trên hệ thống quản lý tài sản CNTT EAIMS chưa được kích hoạt."
                : $"Quản trị viên vừa tạo cho bạn một tài khoản trên hệ thống quản lý tài sản CNTT EAIMS {purpose}.";

        var html = $$"""
<!DOCTYPE html>
<html lang="vi"><head><meta charset="utf-8"><title>{{Esc(subject)}}</title></head>
<body style="font-family:Segoe UI,Arial,sans-serif;color:#1f2937;line-height:1.5;background:#f3f4f6;margin:0;padding:24px">
  <div style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:28px;border:1px solid #e5e7eb">
    <h2 style="margin:0 0 12px;color:#1e3a8a">{{Esc(heading)}}</h2>
    <p>Xin chào <strong>{{Esc(fullName)}}</strong>,</p>
    <p>{{Esc(notice)}}</p>
    <p>Thông tin tài khoản của bạn:</p>
    <table style="border-collapse:collapse;width:100%;margin:12px 0">
      {{rows}}
    </table>
    <p>Để bắt đầu sử dụng, hãy bấm nút bên dưới rồi <strong>tự đặt mật khẩu</strong> cho tài khoản. Tài khoản chỉ dùng được sau khi bạn hoàn tất bước này.</p>
    <p style="text-align:center;margin:24px 0">
      <a href="{{url}}" style="background:#2563eb;color:#ffffff;text-decoration:none;padding:12px 26px;border-radius:6px;display:inline-block;font-weight:600">{{Esc(buttonText)}}</a>
    </p>
    <p style="font-size:13px;color:#6b7280">Nếu nút không bấm được, hãy sao chép liên kết sau vào trình duyệt:<br><span style="word-break:break-all">{{url}}</span></p>
    <hr style="border:none;border-top:1px solid #e5e7eb;margin:20px 0">
    <p style="margin:0 0 6px"><strong>Hướng dẫn</strong></p>
    <ol style="margin-top:0;font-size:14px">
      <li>Bấm nút <strong>{{Esc(buttonText)}}</strong> và kiểm tra lại thông tin trên trang mở ra.</li>
      <li>Nhập mật khẩu mới (từ 8 ký tự, gồm cả chữ và số) và nhập lại để xác nhận.</li>
      <li>Sau khi kích hoạt, đăng nhập bằng email ở trên và mật khẩu bạn vừa đặt.</li>
    </ol>
    <p style="margin:0 0 6px"><strong>Lưu ý bảo mật</strong></p>
    <ul style="margin-top:0;font-size:14px;color:#374151">
      <li>Liên kết có hiệu lực <strong>{{validDays}} ngày</strong> (đến {{expires}}) và chỉ dùng được <strong>một lần</strong>.</li>
      <li>Không chia sẻ liên kết hoặc mật khẩu của bạn cho bất kỳ ai, kể cả người tự nhận là quản trị viên.</li>
      <li>EAIMS không bao giờ gửi mật khẩu qua email hay yêu cầu bạn cung cấp mật khẩu qua email, điện thoại.</li>
      <li>Nếu bạn không nhận ra email này, hãy bỏ qua và không bấm vào liên kết — tài khoản sẽ không được kích hoạt.</li>
      <li>Liên kết hết hạn hoặc không hoạt động? Liên hệ Admin IT để được gửi lại.</li>
    </ul>
  </div>
</body></html>
""";

        return new Content(subject, html);
    }
}
