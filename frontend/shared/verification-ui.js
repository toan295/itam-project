// Dùng chung cho trang Quản lý người dùng: định dạng thời điểm và kết quả gửi email kích hoạt.
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
