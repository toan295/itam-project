const session = requireAuth("login.html");
renderNav({ active: "disposals", basePath: "../" });

const isAdmin = session.role === "Admin IT";
const isManager = session.role === "Manager";
const isTechnician = session.role === "Technician";

// Mã các bước chính của luồng — khớp DisposalStatusCodes ở backend (bất biến).
const CODE = { Inspected: "Inspected", Proposed: "Proposed", Approved: "Approved", Rejected: "Rejected", Completed: "Completed" };
const FINAL_CODES = [CODE.Rejected, CODE.Completed];

let currentPage = 1;
let candidatePage = 1;
let candidateItems = [];
let currentItems = [];
let statuses = [];
let createModal, actionModal, detailModal, statusModal;
let assetSearchTimer = null;
let currentAction = null; // { type, id }

const ACTION_TITLES = {
  propose: "Đề xuất thanh lý",
  approve: "Duyệt đề xuất thanh lý",
  reject: "Từ chối đề xuất thanh lý",
  complete: "Thực hiện thanh lý",
  subStatus: "Gán trạng thái phụ",
};

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearMessages() {
  document.getElementById("errorAlert").classList.add("d-none");
  document.getElementById("infoAlert").classList.add("d-none");
}

function showInfo(message) {
  const el = document.getElementById("infoAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function showAlertIn(id, message) {
  const el = document.getElementById(id);
  el.textContent = message;
  el.classList.remove("d-none");
}

function formatDateTime(value) {
  return value ? new Date(value + (value.endsWith("Z") ? "" : "Z")).toLocaleString("vi-VN") : "-";
}

function badge(name, color) {
  return `<span class="badge-soft ${escapeHtml(color || "slate")}">${escapeHtml(name)}</span>`;
}

// ===== Khởi tạo =====

async function init() {
  createModal = new bootstrap.Modal(document.getElementById("createModal"));
  actionModal = new bootstrap.Modal(document.getElementById("actionModal"));
  detailModal = new bootstrap.Modal(document.getElementById("detailModal"));
  statusModal = new bootstrap.Modal(document.getElementById("statusModal"));

  if (isTechnician) document.getElementById("openCreateBtn").classList.remove("d-none");
  if (isAdmin) document.getElementById("tabStatusesItem").classList.remove("d-none");

  document.getElementById("openCreateBtn").addEventListener("click", () => openCreateModal());
  document.getElementById("openStatusCreateBtn").addEventListener("click", () => openStatusModal(null));
  document.getElementById("tabRequests").addEventListener("click", () => switchTab("requests"));
  document.getElementById("tabStatuses").addEventListener("click", () => switchTab("statuses"));
  document.getElementById("tabCandidates").addEventListener("click", () => switchTab("candidates"));
  document.getElementById("restoreDefaultsBtn").addEventListener("click", restoreDefaults);
  document.getElementById("filterForm").addEventListener("submit", (e) => { e.preventDefault(); loadRequests(1); });
  document.getElementById("resetFilterBtn").addEventListener("click", () => {
    document.getElementById("filterStatus").value = "";
    loadRequests(1);
  });
  document.getElementById("createForm").addEventListener("submit", submitCreate);
  document.getElementById("actionForm").addEventListener("submit", submitAction);
  document.getElementById("statusForm").addEventListener("submit", submitStatus);
  document.getElementById("assetKeyword").addEventListener("input", (e) => {
    clearTimeout(assetSearchTimer);
    assetSearchTimer = setTimeout(() => searchAssets(e.target.value.trim()), 300);
  });

  await loadStatuses();
  await loadRequests(1);
  refreshCandidateCount();

  // Từ trang Tài sản: ?newAssetId=ID mở sẵn form kiểm tra cho tài sản đó.
  const newAssetId = new URLSearchParams(location.search).get("newAssetId");
  if (newAssetId && isTechnician) openCreateModal(Number(newAssetId));
}

function switchTab(tab) {
  for (const t of ["requests", "candidates", "statuses"]) {
    const key = t[0].toUpperCase() + t.slice(1);
    document.getElementById(`${t}Section`).classList.toggle("d-none", t !== tab);
    document.getElementById(`tab${key}`).classList.toggle("active", t === tab);
  }
  document.getElementById("openStatusCreateBtn").classList.toggle("d-none", tab !== "statuses" || !isAdmin);
  document.getElementById("openCreateBtn").classList.toggle("d-none", tab === "statuses" || !isTechnician);
  clearMessages();
  if (tab === "candidates") loadCandidates(1);
}

// ===== Trạng thái (danh mục) =====

async function loadStatuses() {
  try {
    statuses = await api.get("/disposal-statuses");
  } catch (err) {
    statuses = [];
    showError(err.message);
  }

  // Bộ lọc theo bước chính (backend lọc theo Code).
  const select = document.getElementById("filterStatus");
  const selected = select.value;
  select.innerHTML = `<option value="">-- Tất cả --</option>` +
    statuses.filter((s) => s.isSystem).map((s) => `<option value="${escapeHtml(s.code)}">${escapeHtml(s.name)}</option>`).join("");
  select.value = selected;

  if (isAdmin) renderStatusTable();
}

const DEFAULT_STEP_CODES = ["Inspected", "Proposed", "Approved", "Rejected", "Completed"];
const DEFAULT_STEP_NAMES = { Inspected: "Đã kiểm tra", Proposed: "Đã đề xuất", Approved: "Đã duyệt", Rejected: "Từ chối", Completed: "Hoàn tất" };

function renderMissingSteps() {
  const present = new Set(statuses.map((x) => x.code));
  const missing = DEFAULT_STEP_CODES.filter((c) => !present.has(c));
  document.getElementById("missingStepsAlert").classList.toggle("d-none", missing.length === 0);
  document.getElementById("missingStepsList").textContent = missing.map((c) => DEFAULT_STEP_NAMES[c]).join(", ");
}

async function restoreDefaults() {
  clearMessages();
  try {
    statuses = await api.post("/disposal-statuses/restore-defaults");
    renderStatusTable();
    await loadStatuses();
    showInfo("Đã khôi phục các bước mặc định của quy trình thanh lý.");
  } catch (err) {
    showError(err.message);
  }
}

function renderStatusTable() {
  renderMissingSteps();
  const tbody = document.getElementById("statusTableBody");
  tbody.innerHTML = statuses.map((s) => `
    <tr>
      <td class="text-muted">${s.sortOrder}</td>
      <td>${badge(s.name, s.color)}</td>
      <td>${s.isSystem
        ? '<span class="badge-soft slate"><i class="bi bi-diagram-3"></i> Bước chính</span>'
        : '<span class="badge-soft info">Trạng thái phụ</span>'}</td>
      <td>${escapeHtml(s.description || "")}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-outline-primary me-1" onclick="openStatusModal(${s.id})"><i class="bi bi-pencil"></i> Sửa</button>
        <button class="btn btn-sm btn-outline-danger" onclick="deleteStatus(${s.id})"><i class="bi bi-trash"></i> Xoá</button>
      </td>
    </tr>`).join("");
}

function openStatusModal(id) {
  const status = id === null ? null : statuses.find((s) => s.id === id);
  document.getElementById("statusForm").reset();
  document.getElementById("statusModalError").classList.add("d-none");
  document.getElementById("statusId").value = status ? status.id : "";
  document.getElementById("statusModalTitle").textContent = status ? `Sửa trạng thái: ${status.name}` : "Thêm trạng thái";
  if (status) {
    document.getElementById("statusName").value = status.name;
    document.getElementById("statusDescription").value = status.description || "";
    document.getElementById("statusColor").value = status.color;
    document.getElementById("statusSortOrder").value = status.sortOrder;
  }
  statusModal.show();
}

async function submitStatus(e) {
  e.preventDefault();
  const id = document.getElementById("statusId").value;
  const payload = {
    name: document.getElementById("statusName").value.trim(),
    description: document.getElementById("statusDescription").value.trim() || null,
    color: document.getElementById("statusColor").value,
    sortOrder: Number(document.getElementById("statusSortOrder").value),
  };
  try {
    if (id) await api.put(`/disposal-statuses/${id}`, payload);
    else await api.post("/disposal-statuses", payload);
    statusModal.hide();
    await loadStatuses();
    await loadRequests(currentPage); // tên/màu trạng thái đổi -> cập nhật cả bảng phiếu.
  } catch (err) {
    showAlertIn("statusModalError", err.message);
  }
}

async function deleteStatus(id) {
  const status = statuses.find((s) => s.id === id);
  if (!status) return;
  const warning = status.isSystem
    ? `"${status.name}" là BƯỚC CHÍNH của quy trình thanh lý.\n\nSau khi xoá, thao tác dùng bước này sẽ bị chặn cho tới khi bấm "Khôi phục bước mặc định".\nChỉ xoá được khi chưa có phiếu nào đang dùng.\n\nVẫn xoá?`
    : `Xoá trạng thái "${status.name}"?`;
  if (!confirm(warning)) return;
  clearMessages();
  try {
    await api.del(`/disposal-statuses/${id}`);
    await loadStatuses();
  } catch (err) {
    showError(err.message);
  }
}

// ===== Tài sản hỏng chờ kiểm tra thanh lý =====

async function refreshCandidateCount() {
  try {
    const result = await api.get("/disposal-requests/candidates?page=1&pageSize=1");
    document.getElementById("candidateCount").textContent = result.totalItems;
  } catch {
    document.getElementById("candidateCount").textContent = "-";
  }
}

async function loadCandidates(page = 1) {
  candidatePage = page;
  try {
    const result = await api.get(`/disposal-requests/candidates?page=${page}&pageSize=10`);
    candidateItems = result.items;
    document.getElementById("candidateCount").textContent = result.totalItems;
    document.getElementById("candidateTotalLabel").textContent = `Tổng cộng: ${result.totalItems} tài sản`;
    const tbody = document.getElementById("candidateTableBody");
    tbody.innerHTML = result.items.length === 0
      ? `<tr><td colspan="5"><div class="empty-state"><i class="bi bi-check-circle"></i><div class="title">Không có tài sản hỏng nào đang chờ kiểm tra</div></div></td></tr>`
      : result.items.map((a) => `
        <tr>
          <td class="fw-semibold">${escapeHtml(a.assetCode)}<div class="text-muted small">${escapeHtml(a.assetName)}</div></td>
          <td>${escapeHtml(a.categoryName)}</td>
          <td>${escapeHtml(a.departmentName)}</td>
          <td>${a.serialNumber ? escapeHtml(a.serialNumber) : '<span class="text-muted">-</span>'}</td>
          <td class="text-end">${isTechnician
            ? `<button class="btn btn-sm btn-primary" onclick="openCreateModal(${a.assetId})"><i class="bi bi-clipboard-check"></i> Kiểm tra thanh lý</button>`
            : '<span class="text-muted small">Chờ Technician kiểm tra</span>'}</td>
        </tr>`).join("");

    let html = "";
    for (let i = 1; i <= result.totalPages; i++) {
      html += `<li class="page-item ${i === result.page ? "active" : ""}"><a class="page-link" href="#" onclick="loadCandidates(${i}); return false;">${i}</a></li>`;
    }
    document.getElementById("candidatePagination").innerHTML = html;
  } catch (err) {
    showError(err.message);
  }
}

// ===== Danh sách phiếu =====

async function loadRequests(page = 1) {
  currentPage = page;
  clearMessages();
  try {
    const params = new URLSearchParams({ page, pageSize: 10 });
    const status = document.getElementById("filterStatus").value;
    if (status) params.set("status", status);
    const result = await api.get(`/disposal-requests?${params}`);
    renderTable(result.items);
    renderPagination(result.page, result.totalPages);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} phiếu`;
  } catch (err) {
    showError(err.message);
    renderTable([]);
    renderPagination(1, 0);
  }
}

// Nút thao tác theo (vai trò, bước hiện tại) — khớp phân quyền backend.
function actionButtons(r) {
  const buttons = [`<button class="btn btn-sm btn-outline-secondary me-1" onclick="openDetail(${r.id})"><i class="bi bi-eye"></i> Chi tiết</button>`];
  if (isTechnician && r.statusCode === CODE.Inspected) {
    buttons.push(`<button class="btn btn-sm btn-primary me-1" onclick="openAction('propose', ${r.id})"><i class="bi bi-send"></i> Đề xuất</button>`);
  }
  if (isManager && r.statusCode === CODE.Proposed) {
    buttons.push(`<button class="btn btn-sm btn-success me-1" onclick="openAction('approve', ${r.id})"><i class="bi bi-check2"></i> Duyệt</button>`);
    buttons.push(`<button class="btn btn-sm btn-outline-danger me-1" onclick="openAction('reject', ${r.id})"><i class="bi bi-x-lg"></i> Từ chối</button>`);
  }
  if (isAdmin && r.statusCode === CODE.Approved) {
    buttons.push(`<button class="btn btn-sm btn-danger me-1" onclick="openAction('complete', ${r.id})"><i class="bi bi-archive"></i> Thanh lý</button>`);
  }
  if (isAdmin && !FINAL_CODES.includes(r.statusCode)) {
    buttons.push(`<button class="btn btn-sm btn-outline-secondary" onclick="openAction('subStatus', ${r.id})" title="Gán trạng thái phụ"><i class="bi bi-tag"></i></button>`);
  }
  return buttons.join("");
}

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("requestTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6"><div class="empty-state"><i class="bi bi-archive"></i><div class="title">Không có phiếu thanh lý</div></div></td></tr>`;
    return;
  }
  tbody.innerHTML = items.map((r) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(r.assetCode)}<div class="text-muted small">${escapeHtml(r.assetName)}</div></td>
      <td>${escapeHtml(r.departmentName)}</td>
      <td>${badge(r.statusName, r.statusColor)}${r.subStatusName ? ` ${badge(r.subStatusName, r.subStatusColor)}` : ""}</td>
      <td>${escapeHtml(r.proposedByName || r.inspectedByName)}</td>
      <td>${formatDateTime(r.createdAt)}</td>
      <td class="text-end text-nowrap">${actionButtons(r)}</td>
    </tr>`).join("");
}

function renderPagination(page, totalPages) {
  let html = "";
  for (let i = 1; i <= totalPages; i++) {
    html += `<li class="page-item ${i === page ? "active" : ""}">
      <a class="page-link" href="#" onclick="loadRequests(${i}); return false;">${i}</a></li>`;
  }
  document.getElementById("pagination").innerHTML = html;
}

// ===== Chi tiết =====

function openDetail(id) {
  const r = currentItems.find((x) => x.id === id);
  if (!r) return;

  const step = (title, who, when, note, extra = "") => who || when
    ? `<div class="mb-3"><div class="fw-semibold">${title}</div>
        <div class="text-muted small">${escapeHtml(who || "-")} · ${formatDateTime(when)}</div>
        ${extra}${note ? `<div>${escapeHtml(note)}</div>` : ""}</div>`
    : "";

  document.getElementById("detailBody").innerHTML = `
    <div class="mb-3">
      <div class="fs-5 fw-bold">${escapeHtml(r.assetCode)} - ${escapeHtml(r.assetName)}</div>
      <div class="text-muted">${escapeHtml(r.departmentName)} · Tài sản: ${escapeHtml(r.assetStatus)}</div>
      <div class="mt-1">${badge(r.statusName, r.statusColor)}${r.subStatusName ? ` ${badge(r.subStatusName, r.subStatusColor)}` : ""}</div>
    </div>
    ${step("1. Kiểm tra", r.inspectedByName, r.inspectedAt, r.inspectionNote)}
    ${step("2. Đề xuất", r.proposedByName, r.proposedAt, r.reason,
        r.disposalMethod ? `<div class="small">Hình thức: ${escapeHtml(r.disposalMethod)}</div>` : "")}
    ${step("3. Manager xem xét", r.reviewedByName, r.reviewedAt, r.reviewNote)}
    ${step("4. Admin IT thanh lý", r.completedByName, r.completedAt, r.completionNote)}`;
  detailModal.show();
}

// ===== Tạo phiếu (bước 1) =====

async function searchAssets(keyword) {
  const select = document.getElementById("assetSelect");
  try {
    const params = new URLSearchParams({ page: 1, pageSize: 20 });
    if (keyword) params.set("keyword", keyword);
    const result = await api.get(`/assets/search?${params}`);
    const keep = select.value;
    select.innerHTML = result.items
      .filter((a) => a.status !== "Disposed")
      .map((a) => `<option value="${a.id}">${escapeHtml(a.assetCode)} - ${escapeHtml(a.name)} (${escapeHtml(a.departmentName)})</option>`)
      .join("");
    if (keep) select.value = keep;
  } catch (err) {
    showAlertIn("createModalError", err.message);
  }
}

async function openCreateModal(preselectAssetId) {
  document.getElementById("createForm").reset();
  document.getElementById("createModalError").classList.add("d-none");
  await searchAssets("");

  if (preselectAssetId) {
    const select = document.getElementById("assetSelect");
    if (![...select.options].some((o) => Number(o.value) === preselectAssetId)) {
      try {
        const a = await api.get(`/assets/${preselectAssetId}`);
        select.insertAdjacentHTML("afterbegin",
          `<option value="${a.id}">${escapeHtml(a.assetCode)} - ${escapeHtml(a.name)} (${escapeHtml(a.departmentName)})</option>`);
      } catch { /* bỏ qua: người dùng vẫn chọn tay được */ }
    }
    select.value = String(preselectAssetId);
  }
  createModal.show();
}

async function submitCreate(e) {
  e.preventDefault();
  const assetId = Number(document.getElementById("assetSelect").value);
  if (!assetId) {
    showAlertIn("createModalError", "Vui lòng chọn tài sản.");
    return;
  }
  try {
    await api.post("/disposal-requests", {
      assetId,
      inspectionNote: document.getElementById("inspectionNote").value.trim(),
    });
    createModal.hide();
    showInfo("Đã ghi nhận kết quả kiểm tra. Bấm \"Đề xuất\" trên phiếu để gửi Manager duyệt.");
    loadRequests(1);
    refreshCandidateCount();
    if (!document.getElementById("candidatesSection").classList.contains("d-none")) loadCandidates(candidatePage);
  } catch (err) {
    showAlertIn("createModalError", err.message);
  }
}

// ===== Thao tác trên phiếu =====

function openAction(type, id) {
  const r = currentItems.find((x) => x.id === id);
  if (!r) return;
  currentAction = { type, id };

  document.getElementById("actionModalTitle").textContent = ACTION_TITLES[type];
  document.getElementById("actionModalAsset").textContent = `${r.assetCode} - ${r.assetName}`;
  document.getElementById("actionModalError").classList.add("d-none");
  document.getElementById("actionForm").reset();

  const show = (wrapperId, visible) => document.getElementById(wrapperId).classList.toggle("d-none", !visible);
  show("reasonWrapper", type === "propose");
  show("methodWrapper", type === "propose");
  show("subStatusWrapper", type === "subStatus");
  show("noteWrapper", type === "approve" || type === "reject" || type === "complete");
  document.getElementById("actionReason").required = type === "propose";
  document.getElementById("actionNote").required = type === "reject";
  document.getElementById("actionNoteLabel").textContent = type === "reject" ? "Lý do từ chối (bắt buộc)" : "Ghi chú (tuỳ chọn)";

  if (type === "subStatus") {
    const custom = statuses.filter((s) => !s.isSystem);
    document.getElementById("actionSubStatus").innerHTML =
      `<option value="">-- Không có --</option>` +
      custom.map((s) => `<option value="${s.id}">${escapeHtml(s.name)}</option>`).join("");
    document.getElementById("actionSubStatus").value = r.subStatusId ?? "";
  }

  const submit = document.getElementById("actionSubmitBtn");
  submit.className = `btn ${type === "reject" || type === "complete" ? "btn-danger" : "btn-primary"}`;
  submit.textContent = type === "complete" ? "Thanh lý tài sản" : "Xác nhận";
  actionModal.show();
}

async function submitAction(e) {
  e.preventDefault();
  const { type, id } = currentAction;
  const note = document.getElementById("actionNote").value.trim() || null;

  if (type === "complete" && !confirm(
    "Thực hiện thanh lý?\n\n• Tài sản chuyển sang \"Đã thanh lý\" (ngừng sử dụng), KHÔNG bị xoá.\n" +
    "• Lịch sử bảo trì, phân bổ vẫn được giữ.\n• Tài sản đang được phân bổ phải thu hồi trước.")) return;

  try {
    if (type === "propose") {
      await api.post(`/disposal-requests/${id}/propose`, {
        reason: document.getElementById("actionReason").value.trim(),
        disposalMethod: document.getElementById("actionMethod").value.trim() || null,
      });
    } else if (type === "approve") {
      await api.post(`/disposal-requests/${id}/approve`, { note });
    } else if (type === "reject") {
      await api.post(`/disposal-requests/${id}/reject`, { note });
    } else if (type === "complete") {
      await api.post(`/disposal-requests/${id}/complete`, { note });
    } else if (type === "subStatus") {
      const value = document.getElementById("actionSubStatus").value;
      await api.patch(`/disposal-requests/${id}/sub-status`, { subStatusId: value ? Number(value) : null });
    }
    actionModal.hide();
    loadRequests(currentPage);
  } catch (err) {
    showAlertIn("actionModalError", err.message);
  }
}

init();
