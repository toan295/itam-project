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
    document.getElementById("employeeDepartmentId").value = String(session.departmentId);
    document.getElementById("employeeDepartmentId").disabled = true;
  }
}

function buildParams(page) {
  const params = new URLSearchParams({ page, pageSize: 15 });
  for (const field of ["keyword", "departmentId", "isActive"]) {
    const value = document.getElementById(field).value.trim();
    if (value) params.set(field, value);
  }
  return params;
}

async function loadEmployees(page = 1) {
  currentPage = page;
  clearError();
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

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("employeeTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6"><div class="empty-state"><i class="bi bi-person-badge"></i><div class="title">Chưa có nhân viên phù hợp</div>Thêm nhân viên mới hoặc điều chỉnh bộ lọc.</div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((e) => `
    <tr>
      <td class="text-muted">${escapeHtml(e.employeeCode)}</td>
      <td class="fw-semibold">${escapeHtml(e.fullName)}</td>
      <td>${e.position ? escapeHtml(e.position) : '<span class="text-muted">-</span>'}</td>
      <td>${escapeHtml(e.departmentName)}</td>
      <td>${e.isActive
        ? '<span class="badge-soft success"><i class="bi bi-check-circle"></i> Đang hoạt động</span>'
        : '<span class="badge-soft slate"><i class="bi bi-slash-circle"></i> Ngừng hoạt động</span>'}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-outline-primary me-1" onclick="openEditModal(${e.id})"><i class="bi bi-pencil"></i> Sửa</button>
        <button class="btn btn-sm btn-outline-danger" onclick="deleteEmployee(${e.id})"><i class="bi bi-trash"></i> Xoá</button>
      </td>
    </tr>`).join("");
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
  document.getElementById("employeeActiveWrapper").classList.add("d-none");
  document.getElementById("employeeModalError").classList.add("d-none");
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
    position: document.getElementById("employeePosition").value.trim() || null,
    departmentId: Number(document.getElementById("employeeDepartmentId").value),
    isActive: id ? document.getElementById("employeeIsActive").checked : true,
  };
  try {
    if (id) await api.put(`/employees/${id}`, payload);
    else await api.post("/employees", payload);
    employeeModal.hide();
    loadEmployees(id ? currentPage : 1);
  } catch (err) {
    showModalError(err.message);
  }
}

async function deleteEmployee(id) {
  const e = currentItems.find((x) => x.id === id);
  if (!e || !confirm(`Xoá nhân viên "${e.fullName}"?\n\nChỉ xoá được nhân viên chưa từng nhận tài sản; nếu không, hãy chuyển sang "Ngừng hoạt động".`)) return;
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

employeeModal = new bootstrap.Modal(document.getElementById("employeeModal"));
loadDepartments().then(() => loadEmployees(1));
