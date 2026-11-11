const session = requireAuth("login.html");
renderNav({ active: "users", basePath: "../" });

let userModal;
let allDepartments = [];
let allRoles = [];
// Danh sách người dùng đang hiển thị — tra cứu lại theo id khi Sửa/Khoá/Đặt lại mật khẩu, thay vì
// nhúng thẳng JSON.stringify(u) vào thuộc tính onclick='...' (Họ tên là dữ liệu tự do, người dùng tự
// đăng ký chọn được — chứa dấu nháy đơn sẽ phá vỡ thuộc tính HTML, cho phép chèn script XSS lưu trữ
// và chạy trong phiên của Admin IT khi xem trang này).
let currentItems = [];

if (!can("users.manage")) {
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
  document.getElementById("roleId").addEventListener("change", updateRoleHint);
  document.getElementById("candidateKeyword").addEventListener("input", (e) => {
    clearTimeout(candidateTimer);
    candidateTimer = setTimeout(() => loadCandidates(e.target.value.trim()), 300);
  });

  try {
    allRoles = await api.get("/roles");
    document.getElementById("roleFilter").innerHTML = '<option value="">-- Tất cả --</option>'
      + allRoles.map((r) => `<option value="${r.id}">${escapeHtml(roleLabel(r.name))}</option>`).join("");
    const departments = await api.get("/departments");
    allDepartments = departments;
    document.getElementById("editDepartmentId").innerHTML = departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");
    document.getElementById("departmentFilter").innerHTML = '<option value="">-- Tất cả --</option>'
      + departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");
  } catch (err) {
    showError(err.message);
  }

  for (const id of ["roleFilter", "isActiveFilter", "departmentFilter", "activationFilter", "sortFilter"]) {
    document.getElementById(id).addEventListener("change", () => loadUsers(1));
  }
  let searchTimer;
  document.getElementById("search").addEventListener("input", () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => loadUsers(1), 350);
  });
  document.getElementById("filterForm").addEventListener("submit", (e) => { e.preventDefault(); loadUsers(1); });
  document.getElementById("resetFilterBtn").addEventListener("click", () => {
    document.getElementById("filterForm").reset();
    loadUsers(1);
  });
  loadUsers(1);
}

let candidateTimer;
let candidates = [];

// Tạo mới: đủ mọi vai trò, gồm cả Nhân viên (khi đó bắt buộc chọn một nhân viên có sẵn). Sửa: tài khoản nhân viên chỉ hiện vai trò
// Nhân viên (khoá các trường); tài khoản khác không đổi sang Nhân viên được.
const ROLE_DESCRIPTIONS = {
  Employee: "Chỉ xem thiết bị được cấp cho mình và gửi yêu cầu bảo trì.",
  Technician: "Xử lý các phiếu bảo trì do Admin IT phân công.",
  Manager: "Quản lý nhân viên và tài sản trong phòng ban của mình.",
  "Admin IT": "Toàn quyền: quản lý người dùng, phân quyền, tài sản, thanh lý và xem nhật ký hệ thống.",
};

function fillRoleSelect(mode) {
  // Tạo mới: "Nhân viên" (quyền thấp nhất) đứng đầu và được chọn sẵn; vai trò cao hơn phải chọn chủ động.
  const roles = mode === "create"
    ? [...allRoles].sort((a, b) => (b.name === "Employee") - (a.name === "Employee"))
    : allRoles.filter((r) => (r.name === "Employee") === (mode === "editEmployee"));
  document.getElementById("roleId").innerHTML = roles.map((r) => `<option value="${r.id}">${escapeHtml(roleLabel(r.name))}</option>`).join("");
  updateRoleHint();
}

function updateRoleHint() {
  const name = selectedRoleName();
  document.getElementById("roleHint").textContent = ROLE_DESCRIPTIONS[name] || "";
  const id = Number(document.getElementById("employeeId").value);
  if (id && !document.getElementById("userId").value) renderCandidatePreview(candidates.find((x) => x.id === id));
}

function roleLabel(name) {
  return (typeof ROLE_LABELS !== "undefined" && ROLE_LABELS[name]) || name;
}

function selectedRoleName() {
  return allRoles.find((r) => r.id === Number(document.getElementById("roleId").value))?.name;
}

// Tạo mới: nút Lưu chỉ bật khi đã chọn nhân viên.
function updateSubmitState() {
  const creating = !document.getElementById("userId").value;
  document.getElementById("userSubmitBtn").disabled = creating && !document.getElementById("employeeId").value;
  document.getElementById("userSubmitBtn").textContent = creating ? "Tạo tài khoản & gửi email kích hoạt" : "Lưu";
}

async function loadCandidates(keyword) {
  const list = document.getElementById("candidateList");
  try {
    const params = new URLSearchParams();
    if (keyword) params.set("keyword", keyword);
    candidates = await api.get(`/users/employee-candidates?${params}`);
  } catch (err) {
    list.innerHTML = `<div class="list-group-item text-danger small">${escapeHtml(err.message)}</div>`;
    return;
  }

  if (candidates.length === 0) {
    list.innerHTML = `<div class="list-group-item text-muted small">Không có nhân viên nào chưa có tài khoản${keyword ? " khớp từ khoá" : ""}. Hãy thêm nhân viên ở Danh mục nhân viên trước.</div>`;
    return;
  }

  const selected = Number(document.getElementById("employeeId").value);
  list.innerHTML = candidates.map((c) => {
    const noEmail = !c.email;
    return `<button type="button" class="list-group-item list-group-item-action ${c.id === selected ? "active" : ""} ${noEmail ? "disabled" : ""}"
        data-id="${c.id}" ${noEmail ? 'aria-disabled="true" tabindex="-1"' : ""}>
      <div class="fw-semibold">${escapeHtml(c.employeeCode)} · ${escapeHtml(c.fullName)}</div>
      <div class="small ${c.id === selected ? "" : "text-muted"}">${escapeHtml(c.departmentName)}${c.position ? ` · ${escapeHtml(c.position)}` : ""} ·
        ${noEmail ? "chưa có email — bổ sung ở Danh mục nhân viên" : escapeHtml(c.email)}</div>
    </button>`;
  }).join("");
}

document.getElementById("candidateList").addEventListener("click", (e) => {
  const item = e.target.closest("[data-id]");
  if (!item || item.classList.contains("disabled")) return;
  const c = candidates.find((x) => x.id === Number(item.dataset.id));
  if (!c) return;
  document.getElementById("employeeId").value = c.id;
  renderCandidatePreview(c);
  loadCandidates(document.getElementById("candidateKeyword").value.trim());
  updateSubmitState();
});

function renderCandidatePreview(c) {
  const el = document.getElementById("candidatePreview");
  el.classList.toggle("d-none", !c);
  if (!c) return;
  el.innerHTML = `<div class="fw-semibold mb-1">Tài khoản sẽ được tạo với thông tin của nhân viên:</div>
    <div>Họ tên: <strong>${escapeHtml(c.fullName)}</strong> (${escapeHtml(c.employeeCode)})</div>
    <div>Email đăng nhập / nhận liên kết kích hoạt: <strong>${escapeHtml(c.email)}</strong></div>
    <div>Số điện thoại: ${c.phone ? escapeHtml(c.phone) : "—"} · Phòng ban: ${escapeHtml(c.departmentName)}</div>
    <div>Chức danh: ${c.position ? escapeHtml(c.position) : "—"}</div>
    <div>Vai trò hệ thống sẽ cấp: <strong>${escapeHtml(roleLabel(selectedRoleName() || ""))}</strong></div>`;
}

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

const ROLE_BADGE_CLASSES = { "Admin IT": "danger", "Manager": "info", "Technician": "slate", "Employee": "success" };

let currentPage = 1;
const FILTER_COUNTED = ["search", "roleFilter", "isActiveFilter", "departmentFilter", "activationFilter"];

function buildParams(page) {
  const params = new URLSearchParams({ page, pageSize: 20 });
  const map = { search: "search", roleFilter: "roleId", isActiveFilter: "isActive", departmentFilter: "departmentId", activationFilter: "activation", sortFilter: "sort" };
  for (const [id, name] of Object.entries(map)) {
    const value = document.getElementById(id).value.trim();
    if (value) params.set(name, value);
  }
  return params;
}

function updateFilterBadge() {
  const n = FILTER_COUNTED.filter((id) => document.getElementById(id).value.trim() !== "").length;
  const el = document.getElementById("filterCount");
  if (!el) return;
  el.textContent = n;
  el.classList.toggle("d-none", n === 0);
}

async function loadUsers(page = currentPage) {
  currentPage = page;
  clearError();
  updateFilterBadge();
  document.getElementById("setupLinkResult").classList.add("d-none");
  try {
    const result = await api.get(`/users?${buildParams(page)}`);
    // Trang cuối bị rỗng sau khi xoá/lọc -> lùi về trang trước.
    if (result.items.length === 0 && page > 1) return loadUsers(page - 1);
    renderTable(result.items, document.getElementById("sortFilter").value === "");
    renderPagination(result.page, Math.ceil(result.totalItems / result.pageSize));
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} tài khoản`;
  } catch (err) {
    showError(err.message);
  }
}

function renderPagination(page, totalPages) {
  let html = "";
  for (let i = 1; i <= totalPages; i++) {
    html += `<li class="page-item ${i === page ? "active" : ""}"><a class="page-link" href="#" onclick="loadUsers(${i}); return false;">${i}</a></li>`;
  }
  document.getElementById("pagination").innerHTML = html;
}

// Cột "Kích hoạt": chỉ nói về liên kết kích hoạt (mốc thời gian, lỗi gửi); trạng thái tài khoản đã có ở cột bên cạnh.
function statusCell(u) {
  const a = u.activation || { status: "None" };
  const sendError = a.sendError
    ? `<div class="small text-danger" title="${escapeHtml(a.sendError)}"><i class="bi bi-exclamation-triangle"></i> Gửi email lỗi</div>` : "";
  switch (a.status) {
    case "Activated":
      return `<div class="small"><i class="bi bi-patch-check text-success"></i> Đã kích hoạt</div><div class="small text-muted">${escapeHtml(formatDateTime(a.activatedAt))}</div>`;
    case "Pending":
      return `<div class="small text-muted">${a.sentAt ? `Gửi ${escapeHtml(formatDateTime(a.sentAt))}` : "Chưa gửi được"}</div>`
        + `${a.expiresAt ? `<div class="small text-muted">Hạn ${escapeHtml(formatDateTime(a.expiresAt))}</div>` : ""}${sendError}`;
    case "Expired":
      return `<div class="small text-danger"><i class="bi bi-clock-history"></i> Liên kết hết hạn</div><div class="small text-muted">Gửi tới ${escapeHtml(a.emailTo || u.email)}</div>${sendError}`;
    default:
      return '<span class="text-muted small">—</span>';
  }
}

// "Sửa" luôn hiện; các thao tác còn lại gom vào menu "⋯" cho cột gọn, cân đối. Tài khoản chưa kích hoạt chưa có mật khẩu nên không có
// "Đặt lại mật khẩu". Dòng của chính mình chỉ có "Sửa" (không tự khoá/đặt lại/xoá).
function userActions(u, isSelf) {
  const edit = `<button class="btn btn-sm btn-outline-primary" onclick="openEditModal(${u.id})"><i class="bi bi-pencil"></i> Sửa</button>`;
  // Dòng của chính mình: chừa chỗ trống bằng nút "⋯" để nút Sửa thẳng hàng với các dòng khác.
  if (isSelf) return `<div class="row-actions">${edit}<span class="btn btn-sm btn-more invisible" aria-hidden="true"></span></div>`;

  const items = [];
  if (u.pendingActivation && u.isActive) {
    items.push(`<li><button class="dropdown-item" onclick="resendActivation(${u.id})" title="Gửi lại liên kết kích hoạt tới ${escapeHtml(u.email)}"><i class="bi bi-envelope"></i>Gửi lại email kích hoạt</button></li>`);
  }
  items.push(`<li><button class="dropdown-item" onclick="toggleActive(${u.id})"><i class="bi bi-${u.isActive ? "lock" : "unlock"}"></i>${u.isActive ? "Khoá tài khoản" : "Mở khoá tài khoản"}</button></li>`);
  if (!u.pendingActivation) {
    items.push(`<li><button class="dropdown-item" onclick="resetPassword(${u.id})"><i class="bi bi-key"></i>Đặt lại mật khẩu</button></li>`);
  }
  items.push('<li><hr class="dropdown-divider"></li>');
  items.push(`<li><button class="dropdown-item text-danger" onclick="deleteUser(${u.id})" title="Xoá tài khoản đăng nhập (giữ lịch sử, hồ sơ nhân viên không bị xoá)"><i class="bi bi-trash"></i>Xoá tài khoản</button></li>`);
  return `<div class="row-actions">${edit}
    <div class="dropdown">
      <button class="btn btn-sm btn-outline-secondary btn-more" type="button" data-bs-toggle="dropdown" aria-expanded="false" title="Thao tác khác"><i class="bi bi-three-dots"></i></button>
      <ul class="dropdown-menu dropdown-menu-end">${items.join("")}</ul>
    </div></div>`;
}

function renderTable(items, grouped = true) {
  currentItems = items;
  const tbody = document.getElementById("userTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7"><div class="empty-state"><i class="bi bi-people"></i><div class="title">Chưa có người dùng nào.</div></div></td></tr>`;
    return;
  }

  // Nhóm theo vai trò (backend đã sắp xếp theo vai trò): chèn dòng tiêu đề mỗi khi sang vai trò mới.
  const countByRole = items.reduce((acc, u) => ({ ...acc, [u.roleName]: (acc[u.roleName] || 0) + 1 }), {});
  let lastRole = null;

  tbody.innerHTML = items.map((u) => {
    const isSelf = u.id === session.userId;
    const groupHeader = grouped && u.roleName !== lastRole
      ? `<tr class="table-light">
          <td colspan="7" class="fw-semibold">
            <span class="badge-soft ${ROLE_BADGE_CLASSES[u.roleName] || "slate"}">${escapeHtml(roleLabel(u.roleName))}</span>
            <span class="text-muted small ms-1">${countByRole[u.roleName]} tài khoản</span>
          </td>
        </tr>`
      : "";
    lastRole = u.roleName;
    return `${groupHeader}
    <tr>
      <td class="fw-semibold">${escapeHtml(u.fullName)} ${isSelf ? '<span class="badge-soft slate">Bạn</span>' : ""}
        ${u.mustChangePassword ? '<span class="badge-soft warning" title="Đang dùng mật khẩu khởi tạo — phải đổi mật khẩu trước khi dùng"><i class="bi bi-key"></i> Chờ đổi mật khẩu</span>' : ""}</td>
      <td class="col-email">${escapeHtml(u.email)}<div class="small text-muted">${u.phone ? escapeHtml(u.phone) : "Chưa có số điện thoại"}</div></td>
      <td><span class="badge-soft ${ROLE_BADGE_CLASSES[u.roleName] || "slate"}">${escapeHtml(roleLabel(u.roleName))}</span></td>
      <td>${escapeHtml(u.departmentName)}</td>
      <td>${!u.isActive
          ? '<span class="status-dot off">Đã khoá</span>'
          : u.pendingActivation
            ? '<span class="status-dot wait" title="Chưa đăng nhập được cho tới khi nhân viên kích hoạt">Chờ kích hoạt</span>'
            : '<span class="status-dot ok">Đang hoạt động</span>'}</td>
      <td>${statusCell(u)}</td>
      <td class="text-end col-actions">${userActions(u, isSelf)}</td>
    </tr>`;
  }).join("");
  // Menu "⋯" nằm trong vùng cuộn ngang của bảng: định vị fixed để không bị cắt.
  tbody.querySelectorAll('[data-bs-toggle="dropdown"]').forEach((el) => new bootstrap.Dropdown(el, { popperConfig: { strategy: "fixed" } }));
}

function openCreateModal() {
  document.getElementById("userForm").reset();
  document.getElementById("userId").value = "";
  document.getElementById("employeeId").value = "";
  document.getElementById("userModalTitle").textContent = "Thêm người dùng";
  fillRoleSelect("create");
  document.getElementById("roleId").disabled = false;
  document.getElementById("employeePicker").classList.remove("d-none");
  document.getElementById("profileFields").classList.add("d-none");
  document.getElementById("isActiveWrapper").classList.add("d-none");
  document.getElementById("editHint").classList.add("d-none");
  document.getElementById("userModalError").classList.add("d-none");
  document.getElementById("candidateKeyword").value = "";
  renderCandidatePreview(null);
  loadCandidates("");
  updateSubmitState();
  userModal.show();
}

function openEditModal(id) {
  const user = currentItems.find((x) => x.id === id);
  if (!user) return;

  document.getElementById("userForm").reset();
  document.getElementById("employeeId").value = "";
  fillRoleSelect(user.isEmployeeAccount ? "editEmployee" : "editStaff");
  document.getElementById("userId").value = user.id;
  document.getElementById("userModalTitle").textContent = `Sửa tài khoản: ${user.fullName}`;
  document.getElementById("roleId").value = user.roleId;
  updateRoleHint();
  document.getElementById("roleId").disabled = !!user.isEmployeeAccount;   // vai trò Nhân viên không đổi được
  document.getElementById("isActive").checked = user.isActive;
  document.getElementById("employeePicker").classList.add("d-none");
  document.getElementById("editFullName").value = user.fullName;
  document.getElementById("editEmail").value = user.email;
  document.getElementById("editPhone").value = user.phone || "";
  document.getElementById("editDepartmentId").value = String(user.departmentId);
  document.getElementById("profileFields").classList.remove("d-none");
  document.getElementById("editHint").classList.remove("d-none");
  document.getElementById("userModalError").classList.add("d-none");

  // Không cho tự khoá chính mình ngay trên UI (khớp rule backend) — ẩn luôn lựa chọn gây nhầm lẫn.
  document.getElementById("isActiveWrapper").classList.toggle("d-none", user.id === session.userId);
  updateSubmitState();
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
      const updated = await api.put(`/users/${id}`, {
        fullName: document.getElementById("editFullName").value.trim(),
        email: document.getElementById("editEmail").value.trim(),
        phone: document.getElementById("editPhone").value.trim(),
        departmentId: Number(document.getElementById("editDepartmentId").value),
        roleId: Number(document.getElementById("roleId").value),
        isActive: isSelf ? true : document.getElementById("isActive").checked,
      });
      userModal.hide();
      await loadUsers();
      if (updated.warning) showWarning(updated.warning);
      else showResult(`Đã cập nhật tài khoản ${updated.fullName}.`);
    } else {
      // Họ tên/email/SĐT/phòng ban lấy từ hồ sơ nhân viên đã chọn.
      const employeeId = Number(document.getElementById("employeeId").value);
      if (!employeeId) throw new Error("Hãy chọn một nhân viên trong danh sách.");
      if (selectedRoleName() === "Admin IT") {
        const c = candidates.find((x) => x.id === employeeId);
        if (!confirm(`Bạn đang cấp quyền QUẢN TRỊ (Admin IT) cho ${c ? `${c.employeeCode} – ${c.fullName}` : "nhân viên này"}.

Người này sẽ có toàn quyền trên hệ thống. Tiếp tục?`)) return;
      }
      const result = await api.post("/users", { roleId: Number(document.getElementById("roleId").value), employeeId });
      userModal.hide();
      await loadUsers(); // await trước — loadUsers() tự ẩn khung kết quả cũ, phải xong rồi mới hiện khung mới.
      const notice = dispatchNotice(result.activationDispatch);
      showResult(
        `Đã tạo tài khoản ${roleLabel(result.roleName)} cho ${result.fullName} (chờ kích hoạt). ${notice ? notice.text : ""} `
        + "Người dùng sẽ bấm “Kích hoạt tài khoản” trong email và tự đặt mật khẩu — bạn không cần (và không thể) đặt mật khẩu cho họ.",
        notice ? notice.ok : true);
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
    const updated = await api.put(`/users/${user.id}`, { roleId: user.roleId, isActive: !user.isActive });
    await loadUsers();
    if (updated.warning) showWarning(updated.warning);
  } catch (err) {
    showError(err.message);
  }
}

async function deleteUser(id) {
  const user = currentItems.find((x) => x.id === id);
  if (!user) return;

  const message = [
    `Xoá tài khoản của "${user.fullName}" (${user.email})?`,
    "",
    "• Người này không đăng nhập được nữa (mọi phiên đang mở bị chặn ngay).",
    "• Hồ sơ trong Danh mục nhân viên và lịch sử thao tác, phiếu bảo trì, biên bản vẫn được giữ.",
    "• Có thể tạo lại tài khoản mới cho nhân viên này sau.",
    "",
    'Muốn tạm dừng và có thể mở lại thì dùng "Khoá" thay vì xoá.',
  ].join("\n");
  if (!confirm(message)) return;

  clearError();
  try {
    await api.del(`/users/${id}`);
    await loadUsers();
    showResult(`Đã xoá tài khoản của ${user.fullName}. Hồ sơ nhân viên vẫn còn; có thể tạo tài khoản mới khi cần.`);
  } catch (err) {
    showError(err.message);
  }
}

async function resetPassword(id) {
  const user = currentItems.find((x) => x.id === id);
  if (!user) return;

  if (!confirm(`Đặt lại mật khẩu của "${user.fullName}"?\n\nMật khẩu hiện tại sẽ mất hiệu lực NGAY và người dùng bị đăng xuất khỏi mọi phiên. `
    + `Hệ thống gửi liên kết tới ${user.email} để họ tự đặt mật khẩu mới.`)) return;

  clearError();
  try {
    const result = await api.post(`/users/${id}/reset-password`);
    await loadUsers();
    const notice = dispatchNotice(result.activationDispatch);
    showResult(`Đã vô hiệu mật khẩu cũ của ${result.fullName}. ${notice ? notice.text : ""}`, notice ? notice.ok : true);
  } catch (err) {
    showError(err.message);
  }
}

function showWarning(message) {
  const el = document.getElementById("setupLinkResult");
  el.innerHTML = `<div class="alert alert-warning py-2 mb-0"><i class="bi bi-exclamation-triangle"></i> ${escapeHtml(message)}</div>`;
  el.classList.remove("d-none");
}

function showResult(message, ok = true) {
  const el = document.getElementById("setupLinkResult");
  el.innerHTML = `<div class="alert alert-${ok ? "success" : "warning"} py-2 mb-0">${escapeHtml(message)}</div>`;
  el.classList.remove("d-none");
}

async function resendActivation(id) {
  const user = currentItems.find((x) => x.id === id);
  if (!user) return;
  if (!confirm(`Gửi lại email kích hoạt tới ${user.email}?\nLiên kết trong email cũ sẽ không còn dùng được.`)) return;

  clearError();
  try {
    const result = await api.post(`/users/${id}/resend-activation`);
    await loadUsers();
    const notice = dispatchNotice(result.activationDispatch);
    showResult(notice ? notice.text : "Đã gửi lại email kích hoạt.", notice ? notice.ok : true);
  } catch (err) {
    showError(err.message);
  }
}
