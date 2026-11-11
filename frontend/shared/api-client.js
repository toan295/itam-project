const API_BASE_URL = "http://localhost:5140/api/v1"; // Khớp profile "http" trong launchSettings.json — đổi thành https://localhost:7070/api/v1 nếu chạy profile "https".

const SESSION_KEY = "eaims_session"; // { token, expiresAt, userId, fullName, email, role, departmentId }

// Dùng chung cho mọi trang (file này luôn được load trước script riêng của từng trang) — tránh chèn
// thẳng dữ liệu tự do (Tên, Họ tên, Ghi chú...) vào innerHTML mà không escape, gây XSS lưu trữ.
function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

function getSession() {
  try {
    const raw = localStorage.getItem(SESSION_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function setSession(authResponse) {
  localStorage.setItem(SESSION_KEY, JSON.stringify(authResponse));
}

// Quyền của vai trò hiện tại (ma trận phân quyền, do server trả khi đăng nhập). Backend vẫn kiểm tra mọi request —
// hàm này chỉ để ẩn/hiện nút và menu.
function can(permission) {
  return Array.isArray(getSession()?.permissions) && getSession().permissions.includes(permission);
}

function clearSession() {
  localStorage.removeItem(SESSION_KEY);
}

function getToken() {
  return getSession()?.token || null;
}

// Gọi ở đầu mỗi trang cần đăng nhập. Trả về session nếu hợp lệ, ngược lại chuyển hướng sang login.
// pathToLoginPage: đường dẫn tương đối tới login.html tính từ trang đang gọi.
function requireAuth(pathToLoginPage) {
  const session = getSession();
  // Phiên tạo trước khi có phân quyền động chưa có danh sách quyền -> đăng nhập lại một lần.
  if (!session?.token || !Array.isArray(session.permissions)) {
    clearSession();
    window.location.href = pathToLoginPage;
    return null;
  }
  return session;
}

// Báo server ghi nhật ký "Đăng xuất" (best-effort, tối đa ~1,5 giây) rồi xoá phiên và về trang đăng nhập.
async function logout(pathToLoginPage) {
  try {
    await Promise.race([
      fetch(`${API_BASE_URL}/auth/logout`, { method: "POST", headers: { Authorization: `Bearer ${getToken() || ""}` }, keepalive: true }),
      new Promise((resolve) => setTimeout(resolve, 1500)),
    ]);
  } catch {
    // Mất mạng/hết hạn phiên: vẫn đăng xuất phía client.
  }
  clearSession();
  window.location.href = pathToLoginPage;
}

async function apiRequest(path, options = {}) {
  let res;
  try {
    res = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${getToken() || ""}`,
        ...(options.headers || {}),
      },
    });
  } catch {
    throw new Error("Không thể kết nối tới API. Kiểm tra API đã chạy (dotnet run) và đúng cổng trong api-client.js.");
  }

  if (res.status === 204) {
    return null;
  }

  const body = await res.json().catch(() => null);
  if (!res.ok) {
    let message = body?.message;
    if (!message) {
      // API không luôn trả JSON cho lỗi hạ tầng (vd JWT hết hạn) — tự suy ra thông báo tiếng Việt
      // rõ ràng theo mã lỗi thay vì hiển thị "Lỗi HTTP 401/403" khó hiểu cho người dùng.
      if (res.status === 401) {
        message = "Phiên đăng nhập đã hết hạn hoặc bạn chưa đăng nhập. Vui lòng đăng nhập lại.";
      } else if (res.status === 403) {
        message = "Bạn không có quyền thực hiện thao tác này.";
      } else if (res.status >= 500) {
        message = "Hệ thống đang gặp sự cố. Vui lòng thử lại sau.";
      } else {
        message = `Đã xảy ra lỗi (mã ${res.status}). Vui lòng thử lại.`;
      }
    }
    const errors = body?.errors || [];
    const error = new Error(errors.length ? `${message}: ${errors.join(", ")}` : message);
    error.status = res.status;

    if (res.status === 401) {
      // Token hết hạn/không hợp lệ giữa phiên làm việc — dọn session để lần thao tác kế tiếp
      // (hoặc lần requireAuth() ở trang sau) bắt về đăng nhập lại thay vì lặp lại lỗi 401.
      clearSession();
      // Phiên không còn được công nhận (vd máy chủ vừa chạy lại) -> về trang đăng nhập ngay, trừ khi đang ở chính trang đăng nhập/kích hoạt.
      if (!/(login|activate-account)\.html$/i.test(window.location.pathname)) {
        window.location.href = /\/pages\//.test(window.location.pathname) ? "login.html" : "pages/login.html";
      }
    }

    throw error;
  }
  return body?.data;
}

// Tải file (xuất báo cáo): trả { blob, filename, rows }. Lỗi trả JSON {message} như các API khác nên đọc ra thông báo tiếng Việt.
async function apiDownload(path) {
  let res;
  try {
    res = await fetch(`${API_BASE_URL}${path}`, { headers: { Authorization: `Bearer ${getToken() || ""}` } });
  } catch {
    throw new Error("Không thể kết nối tới API. Kiểm tra API đã chạy (dotnet run) và đúng cổng trong api-client.js.");
  }

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    let message = body?.message;
    if (!message) {
      message = res.status === 401 ? "Phiên đăng nhập đã hết hạn hoặc bạn chưa đăng nhập. Vui lòng đăng nhập lại."
        : res.status === 403 ? "Bạn không có quyền thực hiện thao tác này."
        : res.status >= 500 ? "Hệ thống đang gặp sự cố. Vui lòng thử lại sau."
        : `Đã xảy ra lỗi (mã ${res.status}). Vui lòng thử lại.`;
    }
    const errors = body?.errors || [];
    const error = new Error(errors.length ? `${message}: ${errors.join(", ")}` : message);
    error.status = res.status;
    if (res.status === 401) clearSession();
    throw error;
  }

  const disposition = res.headers.get("Content-Disposition") || "";
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  return { blob: await res.blob(), filename: match ? decodeURIComponent(match[1]) : "bao-cao", rows: res.headers.get("X-Report-Rows") === null ? null : Number(res.headers.get("X-Report-Rows")) };
}

// Lưu blob thành file qua thẻ <a> tạm (không mở tab mới, không để lại URL).
function saveBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

const api = {
  download: apiDownload,
  get: (path) => apiRequest(path, { method: "GET" }),
  post: (path, data) => apiRequest(path, { method: "POST", body: JSON.stringify(data) }),
  put: (path, data) => apiRequest(path, { method: "PUT", body: JSON.stringify(data) }),
  patch: (path, data) => apiRequest(path, { method: "PATCH", body: JSON.stringify(data) }),
  del: (path) => apiRequest(path, { method: "DELETE" }),
};
