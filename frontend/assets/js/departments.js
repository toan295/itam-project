const session = requireAuth("login.html");
renderNav({ active: "departments", basePath: "../" });

const isAdmin = session.role === "Admin IT";
let departmentModal;
let currentItems = []; // Tra cứu lại theo id khi Sửa — không nhúng JSON vào onclick (tránh XSS).

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

function showModalError(message) {
  const el = document.getElementById("modalError");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearModalError() {
  document.getElementById("modalError").classList.add("d-none");
}

async function loadDepartments() {
  clearError();
  try {
    const items = await api.get("/departments");
    renderTable(items);
  } catch (err) {
    showError(err.message);
  }
}

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("departmentTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="4"><div class="empty-state"><i class="bi bi-building"></i><div class="title">Chưa có phòng ban nào</div></div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((d, i) => `
    <tr>
      <td class="text-muted">${i + 1}</td>
      <td class="fw-semibold">${escapeHtml(d.name)}</td>
      <td>${escapeHtml(d.description || "")}</td>
      <td class="text-end text-nowrap">
        ${isAdmin ? `
          <button class="btn btn-sm btn-outline-primary me-1" onclick="openEditModal(${d.id})"><i class="bi bi-pencil"></i> Sửa</button>
          <button class="btn btn-sm btn-outline-danger" onclick="deleteDepartment(${d.id})"><i class="bi bi-trash"></i> Xoá</button>
        ` : ""}
      </td>
    </tr>`).join("");
}

async function deleteDepartment(id) {
  if (!confirm("Xoá phòng ban này? Thao tác sẽ bị từ chối nếu còn người dùng, tài sản hoặc bản ghi phân bổ thuộc phòng ban.")) return;
  clearError();
  try {
    await api.del(`/departments/${id}`);
    loadDepartments();
  } catch (err) {
    showError(err.message);
  }
}

function openEditModal(id) {
  const department = currentItems.find((x) => x.id === id);
  if (!department) return;

  document.getElementById("departmentForm").reset();
  document.getElementById("departmentId").value = department.id;
  document.getElementById("departmentModalTitle").textContent = `Sửa phòng ban #${department.id}`;
  document.getElementById("departmentName").value = department.name;
  document.getElementById("departmentDescription").value = department.description || "";
  clearModalError();
  departmentModal.show();
}

document.getElementById("openCreateBtn").addEventListener("click", () => {
  document.getElementById("departmentForm").reset();
  document.getElementById("departmentId").value = "";
  document.getElementById("departmentModalTitle").textContent = "Thêm phòng ban";
  clearModalError();
  departmentModal.show();
});
if (!isAdmin) {
  document.getElementById("openCreateBtn").style.display = "none";
}

document.getElementById("departmentForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  clearModalError();
  const id = document.getElementById("departmentId").value;
  const payload = {
    name: document.getElementById("departmentName").value.trim(),
    description: document.getElementById("departmentDescription").value.trim() || null,
  };
  try {
    if (id) {
      await api.put(`/departments/${id}`, payload);
    } else {
      await api.post("/departments", payload);
    }
    departmentModal.hide();
    loadDepartments();
  } catch (err) {
    showModalError(err.message);
  }
});

departmentModal = new bootstrap.Modal(document.getElementById("departmentModal"));
loadDepartments();
