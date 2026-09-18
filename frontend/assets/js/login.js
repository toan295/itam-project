// Ghi chú: GET /api/v1/departments yêu cầu đăng nhập (đúng quy ước "mọi API trừ /auth/login,
// /auth/register đều cần JWT"), nên form đăng ký (chưa có token) không thể gọi API để lấy danh
// sách phòng ban động. Dùng tạm danh sách cố định khớp với dữ liệu seed (DbSeeder.cs) cho mục đích demo.
const SEED_DEPARTMENTS = [
  { id: 1, name: "Phong IT" },
  { id: 2, name: "Phong Ke toan" },
  { id: 3, name: "Phong Nhan su" },
  { id: 4, name: "Phong Kinh doanh" },
];

// Nếu đã đăng nhập sẵn (session còn hạn), vào thẳng trang chủ thay vì bắt đăng nhập lại.
const existingSession = getSession();
if (existingSession?.token && (!existingSession.expiresAt || new Date(existingSession.expiresAt) > new Date())) {
  window.location.href = "../index.html";
}

const departmentSelect = document.getElementById("registerDepartmentId");
departmentSelect.innerHTML = SEED_DEPARTMENTS.map((d) => `<option value="${d.id}">${d.name}</option>`).join("");

function showFormError(message) {
  const el = document.getElementById("formError");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearFormError() {
  document.getElementById("formError").classList.add("d-none");
}

document.getElementById("loginForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  clearFormError();
  try {
    const result = await api.post("/auth/login", {
      email: document.getElementById("loginEmail").value.trim(),
      password: document.getElementById("loginPassword").value,
    });
    setSession(result);
    window.location.href = "../index.html";
  } catch (err) {
    showFormError(err.message);
  }
});

document.getElementById("registerForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  clearFormError();
  try {
    const result = await api.post("/auth/register", {
      fullName: document.getElementById("registerFullName").value.trim(),
      email: document.getElementById("registerEmail").value.trim(),
      password: document.getElementById("registerPassword").value,
      departmentId: Number(document.getElementById("registerDepartmentId").value),
    });
    setSession(result);
    window.location.href = "../index.html";
  } catch (err) {
    showFormError(err.message);
  }
});

// "Quên mật khẩu?" không phải một pill-tab thường (Đăng nhập/Đăng ký) — chỉ mở khi bấm link,
// nên chuyển tab-pane bằng tay thay vì qua data-bs-toggle của Bootstrap.
function showPane(paneId) {
  document.querySelectorAll("#authTabs .nav-link").forEach((btn) => btn.classList.remove("active"));
  document.querySelectorAll(".tab-pane").forEach((pane) => pane.classList.remove("show", "active"));
  document.getElementById(paneId).classList.add("show", "active");
  clearFormError();
}

document.getElementById("forgotPasswordLink").addEventListener("click", (e) => {
  e.preventDefault();
  document.getElementById("forgotResult").classList.add("d-none");
  document.getElementById("forgotForm").reset();
  showPane("forgotTab");
});

document.getElementById("backToLoginLink").addEventListener("click", (e) => {
  e.preventDefault();
  document.querySelector('#authTabs button[data-bs-target="#loginTab"]').classList.add("active");
  showPane("loginTab");
});

document.getElementById("forgotForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  clearFormError();
  const resultEl = document.getElementById("forgotResult");
  resultEl.classList.add("d-none");

  try {
    const result = await api.post("/auth/forgot-password", {
      email: document.getElementById("forgotEmail").value.trim(),
    });

    if (result.devOnlyResetToken) {
      // Dự án chưa có hạ tầng gửi email thật — ở môi trường Development, API trả thẳng token để
      // demo/test luồng reset. Production sẽ không có trường này (client phải kiểm tra email thật).
      const resetLink = `reset-password.html?token=${encodeURIComponent(result.devOnlyResetToken)}`;
      resultEl.innerHTML = `
        <div class="alert alert-success py-2 mb-2">${escapeHtml(result.message)}</div>
        <div class="alert alert-warning py-2 small mb-0">
          <i class="bi bi-info-circle"></i> Môi trường Development chưa có email thật — dùng liên kết bên dưới để demo
          (Production sẽ không hiển thị mục này, người dùng phải kiểm tra email).<br>
          <a href="${resetLink}" class="fw-semibold">Đặt lại mật khẩu ngay <i class="bi bi-box-arrow-up-right"></i></a>
        </div>`;
    } else {
      resultEl.innerHTML = `<div class="alert alert-success py-2 mb-0">${escapeHtml(result.message)}</div>`;
    }
    resultEl.classList.remove("d-none");
  } catch (err) {
    showFormError(err.message);
  }
});
