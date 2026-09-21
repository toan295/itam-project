const session = requireAuth("login.html");
renderNav({ active: "maintenance", basePath: "../" });

const isAdmin = session.role === "Admin IT";
const isManager = session.role === "Manager";
const isTechnician = session.role === "Technician";
const canViewStats = isAdmin || isManager; // UC-13
let currentPage = 1;
let currentItems = []; // Tra cứu lại theo id khi mở modal — không nhúng JSON vào onclick (tránh XSS, như assets.js).
let ticketModal;
let statusModal;
let assignModal;
let technicians = [];
let assetSearchTimer;

const STATUS_LABELS = { Pending: "Chờ xử lý", Resolved: "Đã xử lý", Failed: "Không xử lý được" };
const STATUS_BADGE_CLASSES = { Pending: "warning", Resolved: "success", Failed: "danger" };
const STATUS_ICONS = { Pending: "bi-hourglass-split", Resolved: "bi-check-circle", Failed: "bi-x-circle" };

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

function showAlertIn(id, message) {
  const el = document.getElementById(id);
  el.textContent = message;
  el.classList.remove("d-none");
}

function formatDateTime(value) {
  return value ? new Date(value + (value.endsWith("Z") ? "" : "Z")).toLocaleString("vi-VN") : "-";
}

async function loadFilterOptions() {
  // Manager/Technician bị backend tự giới hạn theo phòng ban mình — ẩn bộ lọc phòng ban cho khỏi gây hiểu nhầm.
  if (!isAdmin) {
    document.getElementById("departmentFilterWrapper").style.display = "none";
    return;
  }
  try {
    const departments = await api.get("/departments");
    const select = document.getElementById("departmentId");
    select.innerHTML = `<option value="">-- Tất cả --</option>` +
      departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");
  } catch {
    // Không chặn trang nếu không tải được danh sách phòng ban.
  }
}

async function loadTechnicians() {
  if (!isAdmin) return; // Chỉ Admin IT có quyền gọi GET /users.
  try {
    const result = await api.get("/users?page=1&pageSize=100");
    technicians = result.items.filter((u) => u.roleName === "Technician" && u.isActive);
  } catch {
    technicians = [];
  }
}

function filterParams() {
  const params = new URLSearchParams();
  for (const field of ["status", "departmentId", "fromDate", "toDate"]) {
    const value = document.getElementById(field).value.trim();
    if (value) params.set(field, value);
  }
  return params;
}

async function loadTickets(page = 1) {
  currentPage = page;
  clearError();
  try {
    const params = filterParams();
    params.set("page", page);
    params.set("pageSize", 10);
    const result = await api.get(`/maintenance-tickets?${params}`);
    renderTable(result.items);
    renderPagination(result.page, result.totalPages);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} phiếu`;
  } catch (err) {
    showError(err.message);
    renderTable([]);
    renderPagination(1, 0);
    document.getElementById("totalItemsLabel").textContent = "";
  }
}

function actionButtons(t) {
  if (t.status !== "Pending") return ""; // Phiếu đã đóng là trạng thái cuối — không có thao tác nào nữa.

  const buttons = [];
  // Technician tự nhận phiếu chưa ai nhận (UC-12 bước 1).
  if (isTechnician && t.technicianId === null) {
    buttons.push(`<button class="btn btn-sm btn-outline-primary me-1" onclick="claimTicket(${t.id})"><i class="bi bi-hand-index"></i> Nhận xử lý</button>`);
  }
  // Admin IT gán/đổi kỹ thuật viên.
  if (isAdmin) {
    buttons.push(`<button class="btn btn-sm btn-outline-secondary me-1" onclick="openAssignModal(${t.id})"><i class="bi bi-person-gear"></i> Gán KTV</button>`);
  }
  // Chỉ Technician đang được gán cho phiếu này hoặc Admin IT được đóng phiếu (UC-12 E1).
  if (isAdmin || (isTechnician && t.technicianId === session.userId)) {
    buttons.push(`<button class="btn btn-sm btn-outline-success" onclick="openStatusModal(${t.id})"><i class="bi bi-check2-square"></i> Cập nhật kết quả</button>`);
  }
  return buttons.join("");
}

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("ticketTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7"><div class="empty-state"><i class="bi bi-inbox"></i><div class="title">Không có phiếu bảo trì</div>Thử điều chỉnh bộ lọc hoặc tạo phiếu mới.</div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((t) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(t.assetCode)}<div class="text-muted small">${escapeHtml(t.assetName)}</div></td>
      <td>${escapeHtml(t.issueDescription)}${t.notes ? `<div class="text-muted small">Ghi chú: ${escapeHtml(t.notes)}</div>` : ""}</td>
      <td><span class="badge-soft ${STATUS_BADGE_CLASSES[t.status] || "slate"}"><i class="bi ${STATUS_ICONS[t.status] || "bi-question-circle"}"></i> ${STATUS_LABELS[t.status] || escapeHtml(t.status)}</span></td>
      <td>${t.technicianName ? escapeHtml(t.technicianName) : '<span class="text-muted">Chưa gán</span>'}</td>
      <td>${formatDateTime(t.reportedDate)}</td>
      <td>${formatDateTime(t.resolvedDate)}</td>
      <td class="text-end">${actionButtons(t)}</td>
    </tr>`).join("");
}

function renderPagination(page, totalPages) {
  let html = "";
  for (let i = 1; i <= totalPages; i++) {
    html += `<li class="page-item ${i === page ? "active" : ""}">
      <a class="page-link" href="#" onclick="loadTickets(${i}); return false;">${i}</a></li>`;
  }
  document.getElementById("pagination").innerHTML = html;
}

// ===== Thống kê (UC-13) =====

async function loadStats() {
  if (!canViewStats) return;
  document.getElementById("statsSection").classList.remove("d-none");
  const params = filterParams();
  params.delete("status"); // Thống kê luôn đếm theo mọi trạng thái.
  try {
    const stats = await api.get(`/maintenance-tickets/stats?${params}`);
    renderStats(stats);
  } catch (err) {
    showError(err.message);
  }
}

function statCard(label, value, badge) {
  return `<div class="col-6 col-md-3">
    <div class="eaims-card"><div class="card-body py-3">
      <div class="text-muted small">${label}</div>
      <div class="fs-4 fw-bold"><span class="badge-soft ${badge}" style="font-size:1rem">${value}</span></div>
    </div></div></div>`;
}

// Dưới 1 giờ hiển thị theo phút — "0.0 giờ" không có ý nghĩa với phiếu xử lý nhanh.
function formatDuration(hours) {
  if (hours === null || hours === undefined) return "-";
  return hours < 1 ? `${Math.round(hours * 60)} phút` : `${hours.toFixed(1)} giờ`;
}

function renderStats(stats) {
  const c = stats.countByStatus || {};
  const avg = stats.averageResolutionHours;
  document.getElementById("statsCards").innerHTML =
    statCard("Chờ xử lý", c.Pending ?? 0, "warning") +
    statCard("Đã xử lý", c.Resolved ?? 0, "success") +
    statCard("Không xử lý được", c.Failed ?? 0, "danger") +
    statCard("TB thời gian xử lý", formatDuration(avg), "info");

  const tbody = document.getElementById("statsByAssetBody");
  const rows = stats.byAssetPerYear || [];
  if (rows.length === 0) {
    tbody.innerHTML = `<tr><td colspan="3" class="text-center text-muted py-3">Không có dữ liệu trong khoảng lọc.</td></tr>`;
    return;
  }
  tbody.innerHTML = rows.map((r) => `
    <tr><td class="fw-semibold">${escapeHtml(r.assetCode)}</td><td>${r.year}</td><td class="text-end">${r.ticketCount}</td></tr>`).join("");
}

// ===== Tạo phiếu (UC-11) =====

async function searchAssets(keyword) {
  const select = document.getElementById("assetSelect");
  try {
    const params = new URLSearchParams({ page: 1, pageSize: 20 });
    if (keyword) params.set("keyword", keyword);
    const result = await api.get(`/assets/search?${params}`);
    const eligible = result.items.filter((a) => a.status === "InUse" || a.status === "Broken");
    select.innerHTML = eligible
      .map((a) => `<option value="${a.id}">${escapeHtml(a.assetCode)} - ${escapeHtml(a.name)}</option>`)
      .join("");
  } catch (err) {
    showAlertIn("ticketModalError", err.message);
  }
}

function openCreateModal() {
  document.getElementById("ticketForm").reset();
  document.getElementById("ticketModalError").classList.add("d-none");
  document.getElementById("assetSelect").innerHTML = "";
  searchAssets("");
  ticketModal.show();
}

document.getElementById("assetKeyword").addEventListener("input", (e) => {
  clearTimeout(assetSearchTimer);
  assetSearchTimer = setTimeout(() => searchAssets(e.target.value.trim()), 300);
});

document.getElementById("ticketForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  document.getElementById("ticketModalError").classList.add("d-none");
  const assetId = Number(document.getElementById("assetSelect").value);
  if (!assetId) {
    showAlertIn("ticketModalError", "Vui lòng chọn tài sản.");
    return;
  }
  try {
    await api.post("/maintenance-tickets", {
      assetId,
      issueDescription: document.getElementById("issueDescription").value.trim(),
    });
    ticketModal.hide();
    refresh(1);
  } catch (err) {
    showAlertIn("ticketModalError", err.message);
  }
});

// ===== Nhận / gán kỹ thuật viên =====

async function claimTicket(id) {
  clearError();
  try {
    await api.patch(`/maintenance-tickets/${id}/assign`, { technicianId: session.userId });
    refresh(currentPage);
  } catch (err) {
    showError(err.message);
    refresh(currentPage);
  }
}

function openAssignModal(id) {
  document.getElementById("assignModalError").classList.add("d-none");
  document.getElementById("assignTicketId").value = id;
  const select = document.getElementById("assignTechnicianId");
  select.innerHTML = technicians.map((u) => `<option value="${u.id}">${escapeHtml(u.fullName)} (${escapeHtml(u.departmentName)})</option>`).join("");
  const ticket = currentItems.find((x) => x.id === id);
  if (ticket?.technicianId) select.value = String(ticket.technicianId);
  assignModal.show();
}

document.getElementById("assignForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  const id = document.getElementById("assignTicketId").value;
  try {
    await api.patch(`/maintenance-tickets/${id}/assign`, {
      technicianId: Number(document.getElementById("assignTechnicianId").value),
    });
    assignModal.hide();
    refresh(currentPage);
  } catch (err) {
    showAlertIn("assignModalError", err.message);
  }
});

// ===== Cập nhật kết quả (UC-12) =====

function openStatusModal(id) {
  const ticket = currentItems.find((x) => x.id === id);
  if (!ticket) return;
  document.getElementById("statusForm").reset();
  document.getElementById("statusModalError").classList.add("d-none");
  document.getElementById("statusTicketId").value = id;
  document.getElementById("statusModalTitle").textContent = `Cập nhật kết quả - ${ticket.assetCode}`;
  statusModal.show();
}

document.getElementById("statusForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  const id = document.getElementById("statusTicketId").value;
  const notes = document.getElementById("statusNotes").value.trim();
  try {
    await api.patch(`/maintenance-tickets/${id}/status`, {
      status: document.getElementById("newStatus").value,
      notes: notes || null,
    });
    statusModal.hide();
    refresh(currentPage);
  } catch (err) {
    showAlertIn("statusModalError", err.message);
  }
});

function refresh(page) {
  loadTickets(page);
  loadStats();
}

document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);
document.getElementById("filterForm").addEventListener("submit", (e) => {
  e.preventDefault();
  refresh(1);
});
document.getElementById("resetFilterBtn").addEventListener("click", () => {
  document.getElementById("filterForm").reset();
  refresh(1);
});

ticketModal = new bootstrap.Modal(document.getElementById("ticketModal"));
statusModal = new bootstrap.Modal(document.getElementById("statusModal"));
assignModal = new bootstrap.Modal(document.getElementById("assignModal"));
loadFilterOptions();
loadTechnicians();
refresh(1);
