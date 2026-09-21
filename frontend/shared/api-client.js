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
  if (!session?.token || (session.expiresAt && new Date(session.expiresAt) <= new Date())) {
    clearSession();
    window.location.href = pathToLoginPage;
    return null;
  }
  return session;
}

function logout(pathToLoginPage) {
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
    }

    throw error;
  }
  return body?.data;
}

const api = {
  get: (path) => apiRequest(path, { method: "GET" }),
  post: (path, data) => apiRequest(path, { method: "POST", body: JSON.stringify(data) }),
  put: (path, data) => apiRequest(path, { method: "PUT", body: JSON.stringify(data) }),
  patch: (path, data) => apiRequest(path, { method: "PATCH", body: JSON.stringify(data) }),
  del: (path) => apiRequest(path, { method: "DELETE" }),
};
