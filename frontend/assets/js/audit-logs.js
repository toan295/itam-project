const session = requireAuth("../pages/login.html");
renderNav({ active: "auditLogs", basePath: "../" });

const pageSize = 20;
let currentPage = 1;
const usersById = new Map();
const detailModal = bootstrap.Modal.getOrCreateInstance(document.getElementById("auditDetailModal"));

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

function formatDateTime(value) {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("vi-VN");
}

function actionBadge(action) {
  const css = action === "Create"
    ? "text-bg-success"
    : action === "Update"
      ? "text-bg-warning"
      : (action === "ResetPassword" || action === "Delete")
        ? "text-bg-danger"
        : "text-bg-secondary";
  return `<span class="badge ${css}">${escapeHtml(action)}</span>`;
}

function formatJson(raw) {
  if (!raw) return "(Không có)";
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

async function loadUsers() {
  try {
    const result = await api.get("/users?page=1&pageSize=100");
    const select = document.getElementById("userId");
    const items = result?.items || [];
    usersById.clear();
    items.forEach((user) => usersById.set(user.id, user));
    select.innerHTML = '<option value="">-- Tất cả --</option>' + items.map((user) =>
      `<option value="${user.id}">${escapeHtml(user.fullName)} — ${escapeHtml(user.email)}</option>`
    ).join("");
  } catch (err) {
    showError(`Không tải được danh sách người dùng: ${err.message}`);
  }
}

function buildQuery(page) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  const userId = document.getElementById("userId").value;
  const entityName = document.getElementById("entityName").value.trim();
  const action = document.getElementById("action").value;
  const fromDate = document.getElementById("fromDate").value;
  const toDate = document.getElementById("toDate").value;

  if (userId) params.set("userId", userId);
  if (entityName) params.set("entityName", entityName);
  if (action) params.set("action", action);
  if (fromDate) params.set("fromDate", fromDate);
  if (toDate) params.set("toDate", toDate);
  return params.toString();
}

async function loadAuditLogs(page = 1) {
  clearError();
  try {
    const result = await api.get(`/audit-logs?${buildQuery(page)}`);
    currentPage = result.page;
    renderTable(result.items || []);
    renderPagination(result);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} nhật ký`;
  } catch (err) {
    showError(err.message);
  }
}

function renderActor(userId) {
  const user = usersById.get(userId);
  if (!user) return `<span class="text-muted">User #${userId}</span>`;
  return `<div class="fw-semibold">${escapeHtml(user.fullName)}</div><div class="text-muted small">${escapeHtml(user.email)}</div>`;
}

function renderTable(items) {
  const body = document.getElementById("auditTableBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Không có dữ liệu nhật ký.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td>${escapeHtml(formatDateTime(item.timestamp))}</td>
      <td>${renderActor(item.userId)}</td>
      <td>${actionBadge(item.action)}</td>
      <td><span class="fw-semibold">${escapeHtml(item.entityName)}</span> #${item.entityId}</td>
      <td class="text-end">
        <button class="btn btn-sm btn-outline-primary" onclick="viewDetail(${item.id})">
          <i class="bi bi-eye"></i> Xem chi tiết
        </button>
      </td>
    </tr>`).join("");
}

function renderPagination(result) {
  const el = document.getElementById("pagination");
  el.innerHTML = "";
  for (let page = 1; page <= result.totalPages; page += 1) {
    const li = document.createElement("li");
    li.className = `page-item ${page === result.page ? "active" : ""}`;
    li.innerHTML = `<button class="page-link" type="button">${page}</button>`;
    li.querySelector("button").addEventListener("click", () => loadAuditLogs(page));
    el.appendChild(li);
  }
}

async function viewDetail(id) {
  clearError();
  try {
    const item = await api.get(`/audit-logs/${id}`);
    document.getElementById("detailTimestamp").textContent = formatDateTime(item.timestamp);
    const actor = usersById.get(item.userId);
    document.getElementById("detailUser").textContent = actor ? `${actor.fullName} (${actor.email})` : `User #${item.userId}`;
    document.getElementById("detailAction").innerHTML = actionBadge(item.action);
    document.getElementById("detailEntity").textContent = `${item.entityName} #${item.entityId}`;
    document.getElementById("detailOldValue").textContent = formatJson(item.oldValue);
    document.getElementById("detailNewValue").textContent = formatJson(item.newValue);
    detailModal.show();
  } catch (err) {
    showError(err.message);
  }
}

window.viewDetail = viewDetail;

document.getElementById("filterForm").addEventListener("submit", (event) => {
  event.preventDefault();
  loadAuditLogs(1);
});

document.getElementById("resetFilterBtn").addEventListener("click", () => {
  document.getElementById("fromDate").value = "";
  document.getElementById("toDate").value = "";
  document.getElementById("userId").value = "";
  document.getElementById("action").value = "";
  document.getElementById("entityName").value = "";
  loadAuditLogs(1);
});

if (session?.role !== "Admin IT") {
  document.getElementById("forbiddenAlert").textContent = "Chỉ Admin IT được phép xem Nhật ký hệ thống.";
  document.getElementById("forbiddenAlert").classList.remove("d-none");
  document.getElementById("auditContent").classList.add("d-none");
} else {
  loadUsers().then(() => loadAuditLogs());
}
