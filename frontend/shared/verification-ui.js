// Dùng chung cho trang Quản lý người dùng: hiển thị trạng thái kích hoạt tài khoản và kết quả gửi email.
// Phụ thuộc escapeHtml() của api-client.js (nạp api-client.js trước file này).

function formatDateTime(value) {
  if (!value) return "";
  // API trả giờ UTC không kèm "Z" -> thêm "Z" để trình duyệt đổi đúng sang giờ địa phương.
  const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleString("vi-VN");
}

// Kết quả lần gửi email vừa thực hiện: xanh = đã gửi thật; vàng = lưu thử hoặc gửi lỗi.
function dispatchNotice(dispatch) {
  if (!dispatch) return null;
  return { text: dispatch.message, ok: dispatch.sent && dispatch.mode === "smtp" };
}

// Trạng thái kích hoạt tài khoản nhân viên (a: đối tượng "activation" API trả về).
function activationCell(a, email) {
  a = a || { status: "None" };
  const sendError = a.sendError
    ? `<div class="small text-danger" title="${escapeHtml(a.sendError)}"><i class="bi bi-exclamation-triangle"></i> Gửi email lỗi</div>`
    : "";

  switch (a.status) {
    case "Activated":
      return `<span class="badge-soft success" title="Nhân viên đã tự đặt mật khẩu lúc ${escapeHtml(formatDateTime(a.activatedAt))}">
        <i class="bi bi-patch-check"></i> Đã kích hoạt</span>
        <div class="small text-muted">${escapeHtml(formatDateTime(a.activatedAt))}</div>`;
    case "Pending":
      return `<span class="badge-soft warning" title="Đã gửi liên kết tới ${escapeHtml(a.emailTo || email || "")}"><i class="bi bi-hourglass-split"></i> Chờ kích hoạt</span>
        <div class="small text-muted">${a.sentAt ? `Gửi ${escapeHtml(formatDateTime(a.sentAt))}` : "Chưa gửi được"}${a.expiresAt ? ` · hạn ${escapeHtml(formatDateTime(a.expiresAt))}` : ""}</div>${sendError}`;
    case "Expired":
      return `<span class="badge-soft danger" title="Liên kết đã hết hạn mà nhân viên chưa kích hoạt. Hãy gửi lại email kích hoạt."><i class="bi bi-clock-history"></i> Liên kết hết hạn</span>
        <div class="small text-muted">Gửi tới ${escapeHtml(a.emailTo || email || "")}</div>${sendError}`;
    default:
      return '<span class="badge-soft success" title="Tài khoản có sẵn, đang dùng bình thường"><i class="bi bi-check-circle"></i> Đang dùng</span>';
  }
}
