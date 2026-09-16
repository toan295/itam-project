const API_BASE_URL = "http://localhost:5140/api/v1"; // Khớp profile "http" trong launchSettings.json — đổi thành https://localhost:7070/api/v1 nếu chạy profile "https".

function getToken() {
  return localStorage.getItem("eaims_token");
}

function setToken(token) {
  localStorage.setItem("eaims_token", token);
}

async function apiRequest(path, options = {}) {
  let res;
  try {
    res = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${getToken()}`,
        ...(options.headers || {}),
      },
    });
  } catch {
    throw new Error("Không thể kết nối tới API. Kiểm tra API đã chạy (dotnet run) và đúng cổng trong api-client.js.");
  }

  const body = await res.json().catch(() => null);
  if (!res.ok) {
    const message = body?.message || `Lỗi HTTP ${res.status}`;
    const errors = body?.errors || [];
    throw new Error(errors.length ? `${message}: ${errors.join(", ")}` : message);
  }
  return body.data;
}

const api = {
  get: (path) => apiRequest(path, { method: "GET" }),
  post: (path, data) => apiRequest(path, { method: "POST", body: JSON.stringify(data) }),
  put: (path, data) => apiRequest(path, { method: "PUT", body: JSON.stringify(data) }),
  del: (path) => apiRequest(path, { method: "DELETE" }),
};
