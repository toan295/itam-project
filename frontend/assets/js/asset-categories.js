const session = requireAuth("login.html");
renderNav({ active: "categories", basePath: "../" });

const isAdmin = session.role === "Admin IT";
let categoryModal;
let currentItems = []; // tra cứu lại theo id khi Sửa, thay vì nhúng JSON.stringify(c) vào onclick='...' (rủi ro XSS)

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

async function loadCategories() {
  clearError();
  try {
    const items = await api.get("/asset-categories");
    renderTable(items);
  } catch (err) {
    showError(err.message);
  }
}

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("categoryTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="3"><div class="empty-state"><i class="bi bi-tags"></i><div class="title">Chưa có loại tài sản nào</div></div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((c, i) => `
    <tr>
      <td class="text-muted">${i + 1}</td>
      <td class="fw-semibold">${escapeHtml(c.name)}</td>
      <td class="text-end">
        ${isAdmin ? `
          <button class="btn btn-sm btn-outline-primary me-1" onclick="openEditModal(${c.id})"><i class="bi bi-pencil"></i> Sửa</button>
          <button class="btn btn-sm btn-outline-danger" onclick="deleteCategory(${c.id})"><i class="bi bi-trash"></i> Xoá</button>
        ` : ""}
      </td>
    </tr>`).join("");
}

async function deleteCategory(id) {
  if (!confirm("Xoá loại tài sản này? Thao tác sẽ bị từ chối nếu còn tài sản đang dùng loại này.")) return;
  try {
    await api.del(`/asset-categories/${id}`);
    loadCategories();
  } catch (err) {
    showError(err.message);
  }
}

function openCreateModal() {
  document.getElementById("categoryForm").reset();
  document.getElementById("categoryId").value = "";
  document.getElementById("categoryModalTitle").textContent = "Thêm loại tài sản";
  clearModalError();
  categoryModal.show();
}

function openEditModal(id) {
  const category = currentItems.find((x) => x.id === id);
  if (!category) return;

  document.getElementById("categoryForm").reset();
  document.getElementById("categoryId").value = category.id;
  document.getElementById("categoryModalTitle").textContent = `Sửa loại tài sản #${category.id}`;
  document.getElementById("categoryName").value = category.name;
  clearModalError();
  categoryModal.show();
}

document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);
if (!isAdmin) {
  document.getElementById("openCreateBtn").style.display = "none";
}

document.getElementById("categoryForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  clearModalError();
  const id = document.getElementById("categoryId").value;
  const payload = { name: document.getElementById("categoryName").value.trim() };

  try {
    if (id) {
      await api.put(`/asset-categories/${id}`, payload);
    } else {
      await api.post("/asset-categories", payload);
    }
    categoryModal.hide();
    loadCategories();
  } catch (err) {
    showModalError(err.message);
  }
});

categoryModal = new bootstrap.Modal(document.getElementById("categoryModal"));
loadCategories();
