const session = requireAuth("../pages/login.html");
renderNav({ active: "auditLogs", basePath: "../" });

const pageSize = 20;
let currentPage = 1;
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
  // API trả giờ UTC không kèm "Z" -> thêm "Z" để trình duyệt đổi đúng sang giờ địa phương.
  const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("vi-VN");
}

// Nhãn tiếng Việt cho mã hành động / đối tượng ghi trong nhật ký (mã gốc vẫn hiển thị nhỏ để đối chiếu).
const ACTION_LABELS = {
  Login: "Đăng nhập", Logout: "Đăng xuất", LoginFailed: "Đăng nhập thất bại", ChangePassword: "Đổi mật khẩu",
  ResetPassword: "Đặt lại mật khẩu",
  Create: "Thêm mới", Update: "Cập nhật", Delete: "Xoá",
  UpdateStatus: "Cập nhật trạng thái", UpdatePriority: "Đổi mức độ khẩn",
  Assign: "Gán", Unassign: "Gỡ gán", Return: "Thu hồi tài sản", Print: "In biên bản",
  Propose: "Đề xuất thanh lý", Approve: "Duyệt", Reject: "Từ chối", Complete: "Thanh lý (hoàn tất)",
  SetSubStatus: "Gán trạng thái phụ", RestoreDefaults: "Khôi phục bước mặc định",
};

const ENTITY_LABELS = {
  User: "Người dùng", Asset: "Tài sản", Department: "Phòng ban", AssetCategory: "Loại tài sản",
  SoftwareLicense: "Giấy phép phần mềm", MaintenanceTicket: "Phiếu bảo trì", AssetAllocation: "Phân bổ tài sản",
  Employee: "Nhân viên", DisposalRequest: "Phiếu thanh lý", DisposalStatus: "Trạng thái thanh lý",
  DefaultPassword: "Mật khẩu mặc định",
};

function actionBadge(action) {
  const danger = ["Delete", "Unassign", "Reject", "LoginFailed", "ResetPassword"];
  const success = ["Create", "Login", "Approve"];
  const warning = ["Update", "UpdateStatus", "UpdatePriority", "Assign", "ChangePassword", "SetSubStatus", "RestoreDefaults"];
  const primary = ["Complete", "Return", "Print", "Propose"];
  const css = danger.includes(action) ? "text-bg-danger"
    : success.includes(action) ? "text-bg-success"
      : warning.includes(action) ? "text-bg-warning"
        : primary.includes(action) ? "text-bg-primary"
          : "text-bg-secondary";
  const label = ACTION_LABELS[action];
  return `<span class="badge ${css}" title="${escapeHtml(action)}">${escapeHtml(label || action)}</span>`;
}

function entityLabel(name) {
  return ENTITY_LABELS[name] || name;
}

function fillFilterOptions() {
  document.getElementById("action").innerHTML = '<option value="">-- Tất cả --</option>' +
    Object.entries(ACTION_LABELS).map(([value, label]) => `<option value="${value}">${escapeHtml(label)} (${value})</option>`).join("");
  document.getElementById("entityName").innerHTML = '<option value="">-- Tất cả --</option>' +
    Object.entries(ENTITY_LABELS).map(([value, label]) => `<option value="${value}">${escapeHtml(label)}</option>`).join("");
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

function renderActor(item) {
  return item.userName
    ? `<div class="fw-semibold">${escapeHtml(item.userName)}</div><div class="text-muted small">#${item.userId}</div>`
    : `<span class="text-muted">User #${item.userId}</span>`;
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
      <td>${renderActor(item)}</td>
      <td>${actionBadge(item.action)}</td>
      <td><span class="fw-semibold">${escapeHtml(entityLabel(item.entityName))}</span>${item.entityId ? ` #${item.entityId}` : ""}</td>
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
    document.getElementById("detailUser").textContent = item.userName ? `${item.userName} (#${item.userId})` : `User #${item.userId}`;
    document.getElementById("detailAction").innerHTML = actionBadge(item.action);
    document.getElementById("detailEntity").textContent = `${entityLabel(item.entityName)}${item.entityId ? ` #${item.entityId}` : ""}`;
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
  fillFilterOptions();
  loadUsers().then(() => loadAuditLogs());
}
