// Trang công khai: nhân viên mở liên kết trong email "Kích hoạt tài khoản", kiểm tra thông tin và tự đặt mật khẩu.
// Không dùng api.* của api-client vì các hàm đó gắn token đăng nhập và chuyển hướng về login khi 401.

const token = new URLSearchParams(window.location.search).get("token") || "";

const el = (id) => document.getElementById(id);
const show = (id) => el(id).classList.remove("d-none");
const hide = (id) => el(id).classList.add("d-none");

async function callApi(method, path, body) {
  let res;
  try {
    res = await fetch(`${API_BASE_URL}/account-activation/${encodeURIComponent(token)}${path}`, {
      method,
      headers: body ? { "Content-Type": "application/json" } : {},
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new Error("Không thể kết nối tới máy chủ. Vui lòng thử lại sau.");
  }

  let payload = null;
  try {
    payload = await res.json();
  } catch {
    // Phản hồi không phải JSON -> báo lỗi chung bên dưới.
  }
  if (!res.ok) {
    const detail = payload?.errors?.length ? ` ${payload.errors.join(" ")}` : "";
    const error = new Error((payload?.message || (res.status === 429 ? "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút." : "Có lỗi xảy ra. Vui lòng thử lại sau.")) + detail);
    error.status = res.status;
    throw error;
  }
  return payload.data;
}

function showProblem(message, offerLogin = false) {
  hide("loading");
  hide("form");
  hide("done");
  el("problemText").textContent = message;
  el("problemLogin").classList.toggle("d-none", !offerLogin);
  show("problem");
}

function showDone(info) {
  hide("loading");
  hide("form");
  hide("problem");
  el("doneEmail").textContent = info.email;
  show("done");
}

function formatDateTime(value) {
  if (!value) return "";
  const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleString("vi-VN");
}

function renderForm(info) {
  if (info.purpose === "Reset") {
    // Admin đặt lại mật khẩu: cùng trang, đổi chữ cho đúng ngữ cảnh.
    el("pageSubtitle").textContent = "Đặt lại mật khẩu";
    document.title = "Đặt lại mật khẩu - EAIMS";
    el("greetText").innerHTML = 'Xin chào <strong id="greetName"></strong>. Quản trị viên đã đặt lại mật khẩu tài khoản của bạn; mật khẩu cũ đã hết hiệu lực. Hãy đặt mật khẩu mới để tiếp tục sử dụng.';
    el("submitText").textContent = "Đặt mật khẩu mới";
    el("doneTitle").textContent = "Mật khẩu mới đã được đặt";
  }
  el("greetName").textContent = info.fullName;
  const rows = [
    ["Mã nhân viên", info.employeeCode],
    ["Họ và tên", info.fullName],
    ["Tên đăng nhập (email)", info.email],
    ["Vai trò", info.roleName],
    ["Phòng ban", info.departmentName],
    ["Liên kết hết hạn lúc", formatDateTime(info.expiresAt)],
  ].filter(([, value]) => value);
  el("infoRows").innerHTML = rows
    .map(([label, value]) => `<tr><th>${escapeHtml(label)}</th><td>${escapeHtml(value)}</td></tr>`)
    .join("");
  hide("loading");
  show("form");
}

function showFormError(message) {
  el("errorAlert").textContent = message;
  show("errorAlert");
}

async function init() {
  if (!token) {
    showProblem("Liên kết kích hoạt không hợp lệ. Hãy mở đúng liên kết trong email bạn nhận được.");
    return;
  }

  try {
    const info = await callApi("GET", "");
    switch (info.status) {
      case "Pending":
        renderForm(info);
        break;
      case "Activated":
        showProblem("Tài khoản này đã được kích hoạt. Bạn có thể đăng nhập.", true);
        break;
      case "Expired":
        showProblem("Liên kết đã hết hạn. Hãy nhờ Admin IT gửi lại email kích hoạt.");
        break;
      default:
        showProblem("Liên kết này đã bị thay bằng email kích hoạt mới hơn. Hãy dùng liên kết trong email mới nhất.");
    }
  } catch (err) {
    showProblem(err.status === 404
      ? "Liên kết kích hoạt không hợp lệ. Hãy mở đúng liên kết trong email bạn nhận được."
      : err.message);
  }
}

el("togglePasswordBtn").addEventListener("click", () => {
  const input = el("password");
  const reveal = input.type === "password";
  input.type = reveal ? "text" : "password";
  el("confirmPassword").type = reveal ? "text" : "password";
  el("togglePasswordBtn").querySelector("i").className = reveal ? "bi bi-eye-slash" : "bi bi-eye";
});

el("activateForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  hide("errorAlert");
  const password = el("password").value;
  const confirmPassword = el("confirmPassword").value;

  // Kiểm tra nhanh phía trình duyệt; máy chủ vẫn kiểm tra lại đầy đủ.
  if (password.length < 8 || password.length > 72) {
    showFormError("Mật khẩu phải từ 8 đến 72 ký tự.");
    return;
  }
  if (!/[A-Za-zÀ-ỹ]/.test(password) || !/\d/.test(password)) {
    showFormError("Mật khẩu phải gồm cả chữ và số.");
    return;
  }
  if (password !== confirmPassword) {
    showFormError("Mật khẩu nhập lại không khớp.");
    return;
  }

  el("submitBtn").disabled = true;
  try {
    const info = await callApi("POST", "/activate", { password, confirmPassword });
    showDone(info);
  } catch (err) {
    if (err.status === 410) {
      showProblem(err.message, /đã được kích hoạt/.test(err.message));
    } else {
      showFormError(err.message);
    }
  } finally {
    el("submitBtn").disabled = false;
  }
});

init();
