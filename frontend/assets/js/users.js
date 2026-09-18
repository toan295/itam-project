const session = requireAuth("login.html");
renderNav({ active: "users", basePath: "../" });

let userModal;
let roleOptions = [];
let departmentOptions = [];
// Danh sách người dùng đang hiển thị — tra cứu lại theo id khi Sửa/Khoá/Đặt lại mật khẩu, thay vì
// nhúng thẳng JSON.stringify(u) vào thuộc tính onclick='...' (Họ tên là dữ liệu tự do, người dùng tự
// đăng ký chọn được — chứa dấu nháy đơn sẽ phá vỡ thuộc tính HTML, cho phép chèn script XSS lưu trữ
// và chạy trong phiên của Admin IT khi xem trang này).
let currentItems = [];

if (session.role !== "Admin IT") {
  document.getElementById("forbiddenAlert").textContent =
    "Module Quản lý người dùng chỉ dành cho Admin IT. Tài khoản của bạn không có quyền truy cập.";
  document.getElementById("forbiddenAlert").classList.remove("d-none");
  document.getElementById("usersContent").classList.add("d-none");
  document.getElementById("openCreateBtn").style.display = "none";
} else {
  initPage();
}

async function initPage() {
  userModal = new bootstrap.Modal(document.getElementById("userModal"));
  document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);
  document.getElementById("userForm").addEventListener("submit", submitUserForm);

  try {
    const [roles, departments] = await Promise.all([api.get("/roles"), api.get("/departments")]);
    roleOptions = roles;
    departmentOptions = departments;
    fillSelect("roleId", roles);
    fillSelect("departmentId", departments);
  } catch (err) {
    showError(err.message);
  }

  loadUsers();
}

function fillSelect(elementId, items) {
  document.getElementById(elementId).innerHTML =
    items.map((item) => `<option value="${item.id}">${escapeHtml(item.name)}</option>`).join("");
}

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

const ROLE_BADGE_CLASSES = { "Admin IT": "danger", "Manager": "info", "Technician": "slate" };

async function loadUsers() {
  clearError();
  document.getElementById("setupLinkResult").classList.add("d-none");
  try {
    const result = await api.get("/users?page=1&pageSize=100");
    renderTable(result.items);
  } catch (err) {
    showError(err.message);
  }
}

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("userTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6"><div class="empty-state"><i class="bi bi-people"></i><div class="title">Chưa có người dùng nào.</div></div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((u) => {
    const isSelf = u.id === session.userId;
    return `
    <tr>
      <td class="fw-semibold">${escapeHtml(u.fullName)} ${isSelf ? '<span class="badge-soft slate">Bạn</span>' : ""}</td>
      <td>${escapeHtml(u.email)}</td>
      <td><span class="badge-soft ${ROLE_BADGE_CLASSES[u.roleName] || "slate"}">${escapeHtml(u.roleName)}</span></td>
      <td>${escapeHtml(u.departmentName)}</td>
      <td>${u.isActive
          ? '<span class="badge-soft success"><i class="bi bi-check-circle"></i> Đang hoạt động</span>'
          : '<span class="badge-soft danger"><i class="bi bi-lock"></i> Đã khoá</span>'}</td>
      <td class="text-end">
        <button class="btn btn-sm btn-outline-primary me-1" onclick="openEditModal(${u.id})"><i class="bi bi-pencil"></i> Sửa</button>
        ${isSelf ? "" : `
          <button class="btn btn-sm btn-outline-secondary me-1" onclick="toggleActive(${u.id})">
            <i class="bi bi-${u.isActive ? "lock" : "unlock"}"></i> ${u.isActive ? "Khoá" : "Mở khoá"}
          </button>
          <button class="btn btn-sm btn-outline-warning" onclick="resetPassword(${u.id})">
            <i class="bi bi-key"></i> Đặt lại mật khẩu
          </button>`}
      </td>
    </tr>`;
  }).join("");
}

function openCreateModal() {
  document.getElementById("userForm").reset();
  document.getElementById("userId").value = "";
  document.getElementById("userModalTitle").textContent = "Thêm người dùng";
  document.getElementById("isActiveWrapper").classList.add("d-none");
  document.getElementById("createHint").classList.remove("d-none");
  document.getElementById("userModalError").classList.add("d-none");
  userModal.show();
}

function openEditModal(id) {
  const user = currentItems.find((x) => x.id === id);
  if (!user) return;

  document.getElementById("userForm").reset();
  document.getElementById("userId").value = user.id;
  document.getElementById("userModalTitle").textContent = `Sửa người dùng: ${user.fullName}`;
  document.getElementById("fullName").value = user.fullName;
  document.getElementById("email").value = user.email;
  document.getElementById("roleId").value = user.roleId;
  document.getElementById("departmentId").value = user.departmentId;
  document.getElementById("isActive").checked = user.isActive;
  document.getElementById("isActiveWrapper").classList.remove("d-none");
  document.getElementById("createHint").classList.add("d-none");
  document.getElementById("userModalError").classList.add("d-none");

  // Không cho tự khoá chính mình ngay trên UI (khớp rule backend) — ẩn luôn lựa chọn gây nhầm lẫn.
  const isSelf = user.id === session.userId;
  document.getElementById("isActiveWrapper").classList.toggle("d-none", isSelf);

  userModal.show();
}

async function submitUserForm(e) {
  e.preventDefault();
  const errEl = document.getElementById("userModalError");
  errEl.classList.add("d-none");

  const id = document.getElementById("userId").value;
  const isSelf = id && Number(id) === session.userId;

  try {
    if (id) {
      await api.put(`/users/${id}`, {
        fullName: document.getElementById("fullName").value.trim(),
        email: document.getElementById("email").value.trim(),
        roleId: Number(document.getElementById("roleId").value),
        departmentId: Number(document.getElementById("departmentId").value),
        isActive: isSelf ? true : document.getElementById("isActive").checked,
      });
      userModal.hide();
      loadUsers();
    } else {
      const result = await api.post("/users", {
        fullName: document.getElementById("fullName").value.trim(),
        email: document.getElementById("email").value.trim(),
        roleId: Number(document.getElementById("roleId").value),
        departmentId: Number(document.getElementById("departmentId").value),
      });
      userModal.hide();
      await loadUsers(); // await trước — loadUsers() tự ẩn khung kết quả cũ, phải xong rồi mới hiện khung mới.
      showSetupLink(`Đã tạo tài khoản cho ${result.user.fullName}.`, result.devOnlySetupToken);
    }
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove("d-none");
  }
}

async function toggleActive(id) {
  const user = currentItems.find((x) => x.id === id);
  if (!user) return;

  const action = user.isActive ? "khoá" : "mở khoá";
  if (!confirm(`Xác nhận ${action} tài khoản "${user.fullName}"?`)) return;

  try {
    await api.put(`/users/${user.id}`, {
      fullName: user.fullName,
      email: user.email,
      roleId: user.roleId,
      departmentId: user.departmentId,
      isActive: !user.isActive,
    });
    loadUsers();
  } catch (err) {
    showError(err.message);
  }
}

async function resetPassword(id) {
  const user = currentItems.find((x) => x.id === id);
  if (!user) return;

  if (!confirm(`Tạo liên kết đặt lại mật khẩu cho "${user.fullName}"?`)) return;

  try {
    const result = await api.post(`/users/${id}/reset-password`);
    showSetupLink(`Đã tạo liên kết đặt lại mật khẩu cho ${result.user.fullName}.`, result.devOnlySetupToken);
  } catch (err) {
    showError(err.message);
  }
}

function showSetupLink(message, devOnlyToken) {
  const el = document.getElementById("setupLinkResult");
  if (devOnlyToken) {
    const link = `../pages/reset-password.html?token=${encodeURIComponent(devOnlyToken)}`;
    el.innerHTML = `
      <div class="alert alert-success py-2 mb-2">${escapeHtml(message)}</div>
      <div class="alert alert-warning py-2 small">
        <i class="bi bi-info-circle"></i> Môi trường Development chưa có email thật — gửi link bên dưới cho người dùng
        (qua điện thoại/chat nội bộ...) để họ tự thiết lập mật khẩu. Production sẽ gửi qua email, Admin không thấy link này.<br>
        <a href="${link}" target="_blank" class="fw-semibold">Mở liên kết thiết lập mật khẩu <i class="bi bi-box-arrow-up-right"></i></a>
      </div>`;
  } else {
    el.innerHTML = `<div class="alert alert-success py-2 mb-0">${escapeHtml(message)}</div>`;
  }
  el.classList.remove("d-none");
}
