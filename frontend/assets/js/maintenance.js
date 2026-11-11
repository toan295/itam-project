const session = requireAuth("login.html");
renderNav({ active: "maintenance", basePath: "../" });

const isAdmin = session.role === "Admin IT";
const isManager = session.role === "Manager";
const isTechnician = session.role === "Technician";
const canViewStats = can("maintenance.stats"); // UC-13
let currentPage = 1;
let activeTab = "open"; // "open" = cần xử lý (Pending) | "closed" = đã xử lý (Resolved/Failed)
let currentItems = []; // Tra cứu lại theo id khi mở modal — không nhúng JSON vào onclick (tránh XSS, như assets.js).
let ticketModal;
let statusModal;
let assignModal;
let priorityModal;
const guards = {};
let technicians = [];
let filterBar;
let assetSearchTimer;

const STATUS_LABELS = { Pending: "Chờ xử lý", Resolved: "Đã xử lý", Failed: "Không xử lý được" };
const STATUS_BADGE_CLASSES = { Pending: "warning", Resolved: "success", Failed: "danger" };
const PRIORITY_LABELS = { Urgent: "Khẩn cấp", High: "Cao", Normal: "Bình thường", Low: "Thấp" };
const PRIORITY_BADGE_CLASSES = { Urgent: "danger", High: "warning", Normal: "info", Low: "slate" };
const PRIORITY_ICONS = { Urgent: "bi-exclamation-octagon-fill", High: "bi-exclamation-triangle-fill", Normal: "bi-dash-circle", Low: "bi-arrow-down-circle" };

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
  try {
    const categories = await api.get("/asset-categories");
    document.getElementById("categoryId").innerHTML = `<option value="">-- Tất cả --</option>` +
      categories.map((c) => `<option value="${c.id}">${escapeHtml(c.name)}</option>`).join("");
  } catch {
    // Không chặn trang nếu không tải được danh sách loại tài sản.
  }

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
  if (!can("users.manage")) return; // GET /users cần quyền quản lý người dùng.
  try {
    const result = await api.get("/users?page=1&pageSize=100");
    technicians = result.items.filter((u) => u.roleName === "Technician" && u.isActive);
    document.getElementById("technicianId").innerHTML = `<option value="">-- Tất cả --</option>` +
      technicians.map((u) => `<option value="${u.id}">${escapeHtml(u.fullName)}</option>`).join("");
    document.getElementById("technicianFilterWrapper").classList.remove("d-none");
  } catch {
    technicians = [];
  }
}

const FILTER_IDS = ["keyword", "status", "priority", "departmentId", "categoryId", "technicianId", "unassigned",
  "pendingOverDays", "fromDate", "toDate", "sort"];

function filterParams() {
  return filterBar.params(FILTER_IDS);
}

async function loadTickets(page = 1) {
  currentPage = page;
  clearError();
  try {
    const params = filterParams();
    params.set("closed", activeTab === "closed" ? "true" : "false");
    if (activeTab === "open") params.delete("status"); // tab "Cần xử lý" chỉ gồm phiếu Pending.
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
  if (t.status !== "Pending") return '<span class="text-muted">—</span>'; // Phiếu đã đóng là trạng thái cuối — không còn thao tác.

  const buttons = [];
  // Admin IT/Manager điều chỉnh mức độ khẩn của việc đang chờ xử lý.
  if (can("maintenance.priority")) {
    buttons.push(`<button class="btn btn-sm btn-outline-secondary" onclick="openPriorityModal(${t.id})"><i class="bi bi-flag"></i> Mức độ</button>`);
  }
  // Admin IT gán/đổi kỹ thuật viên.
  // Danh sách kỹ thuật viên lấy từ GET /users (cần quyền quản lý người dùng).
  if (can("maintenance.assign") && can("users.manage")) {
    buttons.push(`<button class="btn btn-sm btn-outline-primary" onclick="openAssignModal(${t.id})"><i class="bi bi-person-gear"></i> ${t.technicianId ? "Đổi KTV" : "Phân công KTV"}</button>`);
  }
  // Chỉ Technician đang được gán cho phiếu này hoặc Admin IT được đóng phiếu (UC-12 E1).
  if (can("maintenance.update-status") && (isAdmin || t.technicianId === session.userId)) {
    buttons.push(`<button class="btn btn-sm btn-outline-success" onclick="openStatusModal(${t.id})"><i class="bi bi-check2-square"></i> Cập nhật kết quả</button>`);
  }
  return `<div class="ticket-actions">${buttons.join("")}</div>`;
}

// Số phiếu theo (mã tài sản, năm báo) lấy từ API thống kê — chỉ có với Admin IT/Manager.
let ticketCountByAssetYear = null;

// Mức độ luôn có màu theo cấp (cả tab Đã xử lý); riêng dòng chờ xử lý Khẩn cấp/Cao còn được tô nền nổi bật.
function priorityBadge(t) {
  const p = t.priority || "Normal";
  const cls = PRIORITY_BADGE_CLASSES[p] || "slate";
  return `<span class="badge-soft ${cls}"><i class="bi ${PRIORITY_ICONS[p] || "bi-dash-circle"}"></i> ${PRIORITY_LABELS[p] || escapeHtml(p)}</span>`;
}

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("ticketTableBody");
  if (items.length === 0) {
    const hint = isTechnician ? "Phiếu sẽ xuất hiện ở đây khi Admin IT phân công cho bạn." : "Thử điều chỉnh bộ lọc.";
    tbody.innerHTML = `<tr><td colspan="${activeTab === "open" ? 6 : 5}"><div class="empty-state"><i class="bi bi-inbox"></i><div class="title">Không có phiếu bảo trì</div>${hint}</div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((t) => `
    <tr${t.status === "Pending" && (t.priority === "Urgent" || t.priority === "High")
        ? ` class="row-${t.priority === "Urgent" ? "urgent" : "high"}"` : ""}>
      <td>${priorityBadge(t)}</td>
      <td class="col-asset fw-semibold">${escapeHtml(t.assetCode)}<div class="text-muted small fw-normal">${escapeHtml(t.assetName)}</div></td>
      <td class="col-content">${escapeHtml(t.issueDescription)}${t.reportedByName ? `<div class="text-muted small mt-1"><i class="bi bi-person"></i> Người báo: ${escapeHtml(t.reportedByName)}</div>` : ""}${t.notes ? `<div class="text-muted small mt-1"><i class="bi bi-wrench-adjustable"></i> Kết quả xử lý: ${escapeHtml(t.notes)}</div>` : ""}</td>
      <td><span class="badge-soft ${STATUS_BADGE_CLASSES[t.status] || "slate"}"><i class="bi ${STATUS_ICONS[t.status] || "bi-question-circle"}"></i> ${STATUS_LABELS[t.status] || escapeHtml(t.status)}</span>
        <div class="small mt-1">${t.technicianName ? `<i class="bi bi-person-gear"></i> ${escapeHtml(t.technicianName)}` : '<span class="text-muted">Chưa phân công</span>'}</div></td>
      <td class="col-time"><div><span class="text-muted">Báo:</span> ${formatDateTime(t.reportedDate)}</div>${t.resolvedDate ? `<div><span class="text-muted">Xử lý:</span> ${formatDateTime(t.resolvedDate)}</div>` : ""}</td>
      ${activeTab === "open" ? `<td class="text-end">${actionButtons(t)}</td>` : ""}
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

function statCard(label, value, badge, hint = "") {
  return `<div class="col-6 col-md-3">
    <div class="eaims-card"><div class="card-body py-3">
      <div class="text-muted small">${label}</div>
      <div class="fs-4 fw-bold"><span class="badge-soft ${badge}" style="font-size:1rem">${value}</span></div>
      ${hint ? `<div class="text-muted" style="font-size:.7rem">${hint}</div>` : ""}
    </div></div></div>`;
}

// Dưới 1 giờ hiển thị theo phút — "0.0 giờ" không có ý nghĩa với phiếu xử lý nhanh.
function formatDuration(hours) {
  if (hours === null || hours === undefined) return "-";
  return hours < 1 ? `${Math.round(hours * 60)} phút` : `${hours.toFixed(1)} giờ`;
}

// Ba thẻ đếm phiếu lấy từ CHÍNH API danh sách với cùng bộ lọc nên luôn khớp tổng ở các tab/danh sách;
// riêng thời gian xử lý trung bình và số phiếu/năm đến từ API thống kê (chỉ theo phòng ban + khoảng ngày).
let lastStats = null;
let statusCounts = { Pending: null, Resolved: null, Failed: null };

function renderStatCards() {
  if (!canViewStats) return;
  const n = (v) => (v === null ? "…" : v);
  document.getElementById("statsCards").innerHTML =
    statCard("Chờ xử lý", n(statusCounts.Pending), "warning") +
    statCard("Đã xử lý", n(statusCounts.Resolved), "success") +
    statCard("Không xử lý được", n(statusCounts.Failed), "danger") +
    statCard("TB thời gian xử lý", formatDuration(lastStats?.averageResolutionHours), "info");
}

function renderStats(stats) {
  lastStats = stats;
  renderStatCards();

  ticketCountByAssetYear = new Map(
    (stats.byAssetPerYear || []).map((r) => [`${r.assetCode}|${r.year}`, r.ticketCount]));
  renderTable(currentItems); // vẽ lại để điền cột "Số phiếu/năm" dù thống kê về trước hay sau danh sách.
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

document.getElementById("ticketForm").addEventListener("submit", (e) => {
  e.preventDefault();
  const form = e.currentTarget;
  runLocked(form, async () => {
    document.getElementById("ticketModalError").classList.add("d-none");
    const assetId = Number(document.getElementById("assetSelect").value);
    if (!assetId) {
      showAlertIn("ticketModalError", "Vui lòng chọn tài sản.");
      return;
    }
    const issueDescription = document.getElementById("issueDescription").value.trim();
    if (!issueDescription) {
      showAlertIn("ticketModalError", "Vui lòng mô tả sự cố.");
      return;
    }
    try {
      await api.post("/maintenance-tickets", {
        assetId,
        issueDescription,
        priority: document.getElementById("ticketPriority").value,
      });
      guards.ticket.markClean();
      ticketModal.hide();
      refresh(1);
    } catch (err) {
      showAlertIn("ticketModalError", err.message);
      refresh(1); // phiếu có thể đã được tạo/tài sản đã đổi trạng thái — làm mới để danh sách phản ánh dữ liệu mới nhất.
    }
  });
});

// ===== Đổi mức độ khẩn =====

function openPriorityModal(id) {
  const ticket = currentItems.find((x) => x.id === id);
  if (!ticket) return;
  document.getElementById("priorityModalError").classList.add("d-none");
  document.getElementById("priorityTicketId").value = id;
  document.getElementById("priorityVersion").value = ticket.version ?? "";
  document.getElementById("priorityValue").value = ticket.priority || "Normal";
  document.getElementById("priorityModalTitle").textContent = `Mức độ khẩn - ${ticket.assetCode}`;
  priorityModal.show();
}

document.getElementById("priorityForm").addEventListener("submit", (e) => {
  e.preventDefault();
  runLocked(e.currentTarget, async () => {
    const id = document.getElementById("priorityTicketId").value;
    const version = document.getElementById("priorityVersion").value;
    try {
      await api.patch(`/maintenance-tickets/${id}/priority`, {
        priority: document.getElementById("priorityValue").value,
        version: version ? Number(version) : null,
      });
      guards.priority.markClean();
      priorityModal.hide();
      refresh(currentPage);
    } catch (err) {
      showAlertIn("priorityModalError", err.message);
      if (err.status === 409) refresh(currentPage);
    }
  });
});

// ===== Phân công kỹ thuật viên (Admin IT) =====

function openAssignModal(id) {
  const ticket = currentItems.find((x) => x.id === id);
  if (!ticket) return;
  document.getElementById("assignModalError").classList.add("d-none");
  document.getElementById("assignTicketId").value = id;
  document.getElementById("assignVersion").value = ticket.version ?? "";
  document.getElementById("assignReason").value = "";
  const select = document.getElementById("assignTechnicianId");
  select.innerHTML = technicians.map((u) => `<option value="${u.id}">${escapeHtml(u.fullName)} (${escapeHtml(u.departmentName)})</option>`).join("");
  const reassign = Boolean(ticket.technicianId);
  // Đổi người: chọn mặc định người khác với người đang phụ trách và bắt buộc nêu lý do.
  if (reassign) select.value = String((technicians.find((u) => u.id !== ticket.technicianId) || {}).id ?? "");
  document.getElementById("assignReasonWrapper").classList.toggle("d-none", !reassign);
  document.getElementById("assignReason").required = reassign;
  document.getElementById("assignModalTitle").textContent = reassign ? "Đổi kỹ thuật viên phụ trách" : "Phân công kỹ thuật viên";
  document.getElementById("assignCurrent").innerHTML = reassign
    ? `Đang phụ trách: <strong>${escapeHtml(ticket.technicianName || "")}</strong>`
    : "Phiếu chưa có người phụ trách.";
  assignModal.show();
}

document.getElementById("assignForm").addEventListener("submit", (e) => {
  e.preventDefault();
  runLocked(e.currentTarget, async () => {
    const id = document.getElementById("assignTicketId").value;
    const ticket = currentItems.find((x) => x.id === Number(id));
    const technicianId = Number(document.getElementById("assignTechnicianId").value);
    const reason = document.getElementById("assignReason").value.trim();
    if (!technicianId) {
      showAlertIn("assignModalError", "Vui lòng chọn kỹ thuật viên.");
      return;
    }
    if (ticket?.technicianId === technicianId) {
      showAlertIn("assignModalError", "Phiếu đang do kỹ thuật viên này phụ trách. Hãy chọn người khác.");
      return;
    }
    if (ticket?.technicianId && !reason) {
      showAlertIn("assignModalError", "Vui lòng nhập lý do đổi người phụ trách.");
      return;
    }
    const version = document.getElementById("assignVersion").value;
    try {
      await api.patch(`/maintenance-tickets/${id}/assign`, {
        technicianId,
        reason: reason || null,
        version: version ? Number(version) : null,
      });
      guards.assign.markClean();
      assignModal.hide();
      refresh(currentPage);
    } catch (err) {
      showAlertIn("assignModalError", err.message);
      if (err.status === 409) refresh(currentPage);
    }
  });
});

// ===== Cập nhật kết quả (UC-12) =====

function openStatusModal(id) {
  const ticket = currentItems.find((x) => x.id === id);
  if (!ticket) return;
  document.getElementById("statusForm").reset();
  document.getElementById("statusModalError").classList.add("d-none");
  document.getElementById("statusTicketId").value = id;
  document.getElementById("statusVersion").value = ticket.version ?? "";
  updateStatusNotesHint();
  document.getElementById("statusModalTitle").textContent = `Cập nhật kết quả - ${ticket.assetCode}`;
  statusModal.show();
}

// Gợi ý nhập kết quả theo trạng thái chọn: "đã xử lý" cần việc đã làm, "không xử lý được" cần lý do.
function updateStatusNotesHint() {
  const failed = document.getElementById("newStatus").value === "Failed";
  document.getElementById("statusNotes").placeholder = failed
    ? "Nêu lý do không xử lý được: nguyên nhân, vì sao không khắc phục được, đề xuất tiếp theo..."
    : "Mô tả việc đã làm: nguyên nhân, cách khắc phục, linh kiện đã thay...";
}
document.getElementById("newStatus").addEventListener("change", updateStatusNotesHint);

document.getElementById("statusForm").addEventListener("submit", (e) => {
  e.preventDefault();
  runLocked(e.currentTarget, async () => {
    const id = document.getElementById("statusTicketId").value;
    const notes = document.getElementById("statusNotes").value.trim();
    if (notes.length < 5) {
      showAlertIn("statusModalError", "Vui lòng nhập kết quả xử lý (ít nhất 5 ký tự) trước khi đóng phiếu.");
      return;
    }
    const version = document.getElementById("statusVersion").value;
    try {
      await api.patch(`/maintenance-tickets/${id}/status`, {
        status: document.getElementById("newStatus").value,
        notes,
        version: version ? Number(version) : null,
      });
      guards.status.markClean();
      statusModal.hide();
      refresh(currentPage);
    } catch (err) {
      showAlertIn("statusModalError", err.message);
      if (err.status === 409) refresh(currentPage);
    }
  });
});

// Số phiếu ở mỗi tab (theo cùng bộ lọc phòng ban/ngày), để người dùng thấy ngay còn bao nhiêu việc chưa xử lý.
async function loadTabCounts() {
  const base = filterParams();
  base.delete("status");
  base.delete("sort");
  base.set("page", 1);
  base.set("pageSize", 1);
  const count = async (closed, status) => {
    const p = new URLSearchParams(base);
    p.set("closed", closed);
    if (status) p.set("status", status);
    if (closed === "true") {   // hai bộ lọc này chỉ áp dụng cho phiếu chờ xử lý
      p.delete("unassigned");
      p.delete("pendingOverDays");
    }
    try {
      return (await api.get(`/maintenance-tickets?${p}`)).totalItems;
    } catch {
      return null;
    }
  };
  statusCounts = { Pending: null, Resolved: null, Failed: null };
  renderStatCards();
  const [open, closed, resolved, failed] = await Promise.all([
    count("false"), count("true"), count("true", "Resolved"), count("true", "Failed")]);
  document.getElementById("openCount").textContent = open ?? "-";
  document.getElementById("closedCount").textContent = closed ?? "-";
  statusCounts = { Pending: open, Resolved: resolved, Failed: failed };
  renderStatCards();
}

function setTab(tab, reload = true) {
  activeTab = tab;
  const open = tab === "open";
  document.getElementById("tabOpen").classList.toggle("active", open);
  document.getElementById("tabClosed").classList.toggle("active", !open);
  document.getElementById("statusFilterWrapper").classList.toggle("d-none", open);
  document.getElementById("actionsHeader").classList.toggle("d-none", !open); // tab Đã xử lý không có thao tác nào
  if (open) document.getElementById("status").value = "";
  // Bộ lọc "chưa phân công" / "chờ quá N ngày" chỉ có nghĩa với phiếu đang chờ xử lý.
  for (const id of ["unassigned", "pendingOverDays"]) {
    document.getElementById(`${id}Wrapper`).classList.toggle("d-none", !open);
    if (!open) document.getElementById(id).value = "";
  }
  if (filterBar) filterBar.refreshBadge();
  document.getElementById("sortHint").innerHTML = open
    ? '<i class="bi bi-sort-down"></i> Việc <strong>gấp nhất lên đầu</strong> (Khẩn cấp → Cao → Bình thường → Thấp); cùng mức thì phiếu chờ lâu nhất trước.'
    : '<i class="bi bi-clock-history"></i> Các phiếu đã xử lý xong (hoặc không xử lý được), mới nhất lên đầu — tách riêng khỏi việc đang chờ.';
  if (reload) loadTickets(1);
}

function refresh(page) {
  loadTickets(page);
  loadStats();
  loadTabCounts();
}

// Kỹ thuật viên không tạo phiếu: người dùng được cấp tài sản báo lỗi (trang "Tài sản của tôi"), Admin IT phân công.
if (isTechnician || !can("maintenance.create")) {
  document.getElementById("openCreateBtn").classList.add("d-none");
  document.getElementById("pageSubtitle").textContent = "Các phiếu Admin IT đã phân công cho bạn: kiểm tra tài sản và cập nhật kết quả xử lý.";
} else {
  document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);
}
document.getElementById("tabOpen").addEventListener("click", () => setTab("open"));
document.getElementById("tabClosed").addEventListener("click", () => setTab("closed"));
filterBar = initFilterBar({
  fields: FILTER_IDS, uncounted: ["sort"], textFields: ["keyword", "pendingOverDays"], onChange: () => refresh(1),
});

ticketModal = new bootstrap.Modal(document.getElementById("ticketModal"));
statusModal = new bootstrap.Modal(document.getElementById("statusModal"));
assignModal = new bootstrap.Modal(document.getElementById("assignModal"));
priorityModal = new bootstrap.Modal(document.getElementById("priorityModal"));
guards.ticket = guardUnsavedChanges(document.getElementById("ticketModal"), document.getElementById("ticketForm"));
guards.status = guardUnsavedChanges(document.getElementById("statusModal"), document.getElementById("statusForm"));
guards.assign = guardUnsavedChanges(document.getElementById("assignModal"), document.getElementById("assignForm"));
guards.priority = guardUnsavedChanges(document.getElementById("priorityModal"), document.getElementById("priorityForm"));
loadFilterOptions();
loadTechnicians();
setTab("open", false);
refresh(1);
