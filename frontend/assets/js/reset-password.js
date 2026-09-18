const token = new URLSearchParams(window.location.search).get("token");

const form = document.getElementById("resetForm");
const errorEl = document.getElementById("formError");
const successEl = document.getElementById("formSuccess");

function showError(message) {
  errorEl.textContent = message;
  errorEl.classList.remove("d-none");
  successEl.classList.add("d-none");
}

if (!token) {
  showError("Thiếu token đặt lại mật khẩu. Vui lòng dùng đúng liên kết được cấp từ bước \"Quên mật khẩu?\".");
  form.querySelectorAll("input, button").forEach((el) => (el.disabled = true));
}

form.addEventListener("submit", async (e) => {
  e.preventDefault();
  errorEl.classList.add("d-none");

  const newPassword = document.getElementById("newPassword").value;
  const confirmPassword = document.getElementById("confirmPassword").value;

  if (newPassword !== confirmPassword) {
    showError("Xác nhận mật khẩu không khớp.");
    return;
  }

  const submitBtn = document.getElementById("submitBtn");
  submitBtn.disabled = true;
  submitBtn.textContent = "Đang xử lý...";

  try {
    // api.post() chỉ trả về phần "data" của response envelope — endpoint này không có message
    // riêng trong data (data rỗng), nên dùng message cố định thay vì đọc result.message.
    await api.post("/auth/reset-password", { token, newPassword });
    successEl.textContent = "Đặt lại mật khẩu thành công. Đang chuyển tới trang đăng nhập...";
    successEl.classList.remove("d-none");
    form.querySelectorAll("input, button").forEach((el) => (el.disabled = true));
    setTimeout(() => (window.location.href = "login.html"), 2000);
  } catch (err) {
    showError(err.message);
    submitBtn.disabled = false;
    submitBtn.textContent = "Đặt lại mật khẩu";
  }
});
