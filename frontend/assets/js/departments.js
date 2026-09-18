const session = requireAuth("login.html");
renderNav({ active: "departments", basePath: "../" });

const isAdmin = session.role === "Admin IT";
let departmentModal;

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
  const tbody = document.getElementById("departmentTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="3"><div class="empty-state"><i class="bi bi-building"></i><div class="title">Chưa có phòng ban nào</div></div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((d) => `
    <tr>
      <td class="text-muted">#${d.id}</td>
      <td class="fw-semibold">${escapeHtml(d.name)}</td>
      <td>${escapeHtml(d.description || "")}</td>
    </tr>`).join("");
}

document.getElementById("openCreateBtn").addEventListener("click", () => {
  document.getElementById("departmentForm").reset();
  clearModalError();
  departmentModal.show();
});
if (!isAdmin) {
  document.getElementById("openCreateBtn").style.display = "none";
}

document.getElementById("departmentForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  clearModalError();
  try {
    await api.post("/departments", {
      name: document.getElementById("departmentName").value.trim(),
      description: document.getElementById("departmentDescription").value.trim() || null,
    });
    departmentModal.hide();
    loadDepartments();
  } catch (err) {
    showModalError(err.message);
  }
});

departmentModal = new bootstrap.Modal(document.getElementById("departmentModal"));
loadDepartments();
