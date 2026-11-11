const session = requireAuth("login.html");
renderNav({ active: "employees", basePath: "../" });

const isAdmin = session.role === "Admin IT";
let currentPage = 1;
let currentItems = []; // Tra cứu lại theo id khi Sửa — không nhúng JSON vào onclick (tránh XSS).
let departments = [];
let employeeModal;

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

function showModalError(message) {
  const el = document.getElementById("employeeModalError");
  el.textContent = message;
  el.classList.remove("d-none");
}

async function loadDepartments() {
  try {
    departments = await api.get("/departments");
  } catch (err) {
    showError(err.message);
    return;
  }

  const options = departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");
  document.getElementById("departmentId").innerHTML = `<option value="">-- Tất cả --</option>${options}`;
  document.getElementById("employeeDepartmentId").innerHTML = options;

  // Manager chỉ làm việc trong phòng ban của mình (backend cũng ép như vậy).
  if (!isAdmin) {
    document.getElementById("departmentFilterWrapper").style.display = "none";
    document.getElementById("roleFilterWrapper").style.display = "none";   // Manager chỉ thấy nhân viên thường
    document.getElementById("employeeDepartmentId").value = String(session.departmentId);
    document.getElementById("employeeDepartmentId").disabled = true;
  }
}

const FILTER_FIELDS = ["keyword", "departmentId", "isActive", "accountState", "roleName", "holdingAssets", "sort"];
const FILTER_COUNTED = ["keyword", "departmentId", "isActive", "accountState", "roleName", "holdingAssets"];

function buildParams(page) {
  const params = new URLSearchParams({ page, pageSize: 15 });
  for (const field of FILTER_FIELDS) {
    const value = document.getElementById(field).value.trim();
    if (value) params.set(field, value);
  }
  return params;
}

// Số bộ lọc đang bật (không tính sắp xếp) hiện trên nút "Bộ lọc nâng cao".
function updateFilterBadge() {
  const n = FILTER_COUNTED.filter((f) => document.getElementById(f).value.trim() !== "").length;
  const el = document.getElementById("filterCount");
  if (!el) return;
  el.textContent = n;
  el.classList.toggle("d-none", n === 0);
}

async function loadPositionSuggestions() {
  try {
    const list = await api.get("/employees/positions");
    document.getElementById("positionSuggestions").innerHTML = list.map((p) => `<option value="${escapeHtml(p)}"></option>`).join("");
  } catch {
    /* gợi ý chỉ là tiện ích — lỗi thì bỏ qua */
  }
}

async function loadEmployees(page = 1) {
  currentPage = page;
  clearError();
  updateFilterBadge();
  try {
    const result = await api.get(`/employees?${buildParams(page)}`);
    renderTable(result.items);
    renderPagination(result.page, result.totalPages);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} nhân viên`;
  } catch (err) {
    showError(err.message);
    renderTable([]);
    renderPagination(1, 0);
  }
}

const ROLE_BADGES = { "Admin IT": "danger", Manager: "info", Technician: "slate", Employee: "success" };
const ROLE_NAMES = { "Admin IT": "Admin IT", Manager: "Quản lý", Technician: "Nhân viên kỹ thuật", Employee: "Nhân viên" };

// Cột "Tài khoản": badge vai trò + chấm tình trạng trên cùng một hàng. Chưa có tài khoản thì không có vai trò.
function accountCell(e) {
  if (!e.roleName) {
    return '<span class="badge-soft slate" title="Chưa có tài khoản đăng nhập — cấp ở trang Quản lý người dùng">Chưa cấp tài khoản</span>';
  }
  const role = `<span class="badge-soft ${ROLE_BADGES[e.roleName] || "slate"}">${escapeHtml(ROLE_NAMES[e.roleName] || e.roleName)}</span>`;
  const state = e.pendingActivation ? '<span class="status-dot wait">Chờ kích hoạt</span>'
    : e.accountIsActive === false ? '<span class="status-dot off">Đã khoá</span>'
    : '<span class="status-dot ok">Đang dùng</span>';
  return `<div class="acct-cell">${role}${state}</div>`;
}

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("employeeTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="8"><div class="empty-state"><i class="bi bi-person-badge"></i><div class="title">Chưa có nhân viên phù hợp</div>Thêm nhân viên mới hoặc điều chỉnh bộ lọc.</div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((e) => `
    <tr>
      <td class="text-muted">${escapeHtml(e.employeeCode)}</td>
      <td class="fw-semibold col-name">${escapeHtml(e.fullName)}</td>
      <td class="col-email">${e.email ? escapeHtml(e.email) : '<span class="badge-soft warning" title="Nhân viên cũ chưa có email — bấm Sửa để bổ sung"><i class="bi bi-exclamation-triangle"></i> Chưa có email</span>'}
        <div class="small text-muted">${e.phone ? escapeHtml(e.phone) : "Chưa có số điện thoại"}</div></td>
      <td>${e.position ? escapeHtml(e.position) : '<span class="text-muted">-</span>'}</td>
      <td>${escapeHtml(e.departmentName)}</td>
      <td>${accountCell(e)}</td>
      <td>${e.isActive
        ? '<span class="badge-soft success"><i class="bi bi-check-circle"></i> Đang làm việc</span>'
        : '<span class="badge-soft slate"><i class="bi bi-slash-circle"></i> Đã nghỉ</span>'}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-outline-primary me-1" onclick="openEditModal(${e.id})"><i class="bi bi-pencil"></i> Sửa</button>
        <button class="btn btn-sm btn-outline-danger" onclick="deleteEmployee(${e.id})"><i class="bi bi-trash"></i> Xoá</button>
      </td>
    </tr>`).join("");
}

function showResult(message, ok = true) {
  const el = document.getElementById("resultAlert");
  el.innerHTML = `<div class="alert alert-${ok ? "success" : "warning"} py-2">${escapeHtml(message)}</div>`;
  el.classList.remove("d-none");
}

function renderPagination(page, totalPages) {
  let html = "";
  for (let i = 1; i <= totalPages; i++) {
    html += `<li class="page-item ${i === page ? "active" : ""}">
      <a class="page-link" href="#" onclick="loadEmployees(${i}); return false;">${i}</a></li>`;
  }
  document.getElementById("pagination").innerHTML = html;
}

function openCreateModal() {
  document.getElementById("employeeForm").reset();
  document.getElementById("employeeId").value = "";
  document.getElementById("employeeModalTitle").textContent = "Thêm nhân viên";
  document.getElementById("employeeAccountNote").classList.add("d-none");
  document.getElementById("employeeActiveWrapper").classList.add("d-none");
  document.getElementById("employeeModalError").classList.add("d-none");
  document.getElementById("resultAlert").classList.add("d-none");
  if (!isAdmin) document.getElementById("employeeDepartmentId").value = String(session.departmentId);
  employeeModal.show();
}

function openEditModal(id) {
  const e = currentItems.find((x) => x.id === id);
  if (!e) return;
  document.getElementById("employeeForm").reset();
  document.getElementById("employeeId").value = e.id;
  document.getElementById("employeeModalTitle").textContent = `Sửa nhân viên ${e.employeeCode}`;
  document.getElementById("employeeFullName").value = e.fullName;
  document.getElementById("employeeEmail").value = e.email || "";
  document.getElementById("employeePhone").value = e.phone || "";
  document.getElementById("employeeAccountNote").classList.toggle("d-none", !e.hasAccount);
  document.getElementById("employeePosition").value = e.position || "";
  document.getElementById("employeeDepartmentId").value = String(e.departmentId);
  document.getElementById("employeeIsActive").checked = e.isActive;
  document.getElementById("employeeActiveWrapper").classList.remove("d-none");
  document.getElementById("employeeModalError").classList.add("d-none");
  employeeModal.show();
}

async function submitEmployee(ev) {
  ev.preventDefault();
  document.getElementById("employeeModalError").classList.add("d-none");
  const id = document.getElementById("employeeId").value;
  const payload = {
    fullName: document.getElementById("employeeFullName").value.trim(),
    email: document.getElementById("employeeEmail").value.trim(),
    phone: document.getElementById("employeePhone").value.trim(),
    position: document.getElementById("employeePosition").value.trim() || null,
    departmentId: Number(document.getElementById("employeeDepartmentId").value),
    isActive: id ? document.getElementById("employeeIsActive").checked : true,
  };
  try {
    const saved = id ? await api.put(`/employees/${id}`, payload) : await api.post("/employees", payload);
    employeeModal.hide();
    await loadEmployees(id ? currentPage : 1);
    loadPositionSuggestions();
    showResult(`${id ? "Đã cập nhật" : "Đã thêm"} nhân viên ${saved.fullName}.${id ? "" : " Cần tài khoản đăng nhập? Hãy tạo ở trang Quản lý người dùng."}`);
  } catch (err) {
    showModalError(err.message);
  }
}

async function deleteEmployee(id) {
  const e = currentItems.find((x) => x.id === id);
  if (!e || !confirm(`Xoá nhân viên "${e.fullName}"?\n\nChỉ xoá được nhân viên chưa từng nhận tài sản; nếu không, hãy chuyển sang "Đã nghỉ".`)) return;
  clearError();
  try {
    await api.del(`/employees/${id}`);
    loadEmployees(currentPage);
  } catch (err) {
    showError(err.message);
  }
}

document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);
document.getElementById("employeeForm").addEventListener("submit", submitEmployee);
document.getElementById("filterForm").addEventListener("submit", (e) => { e.preventDefault(); loadEmployees(1); });
document.getElementById("resetFilterBtn").addEventListener("click", () => {
  document.getElementById("filterForm").reset();
  loadEmployees(1);
});
// Ô chọn lọc ngay khi đổi; ô tìm kiếm chờ ngừng gõ một chút để khỏi gọi API mỗi phím.
for (const id of FILTER_FIELDS.filter((f) => f !== "keyword")) {
  document.getElementById(id).addEventListener("change", () => loadEmployees(1));
}
let keywordTimer;
document.getElementById("keyword").addEventListener("input", () => {
  clearTimeout(keywordTimer);
  keywordTimer = setTimeout(() => loadEmployees(1), 350);
});

employeeModal = new bootstrap.Modal(document.getElementById("employeeModal"));
loadDepartments().then(() => loadEmployees(1));
loadPositionSuggestions();
