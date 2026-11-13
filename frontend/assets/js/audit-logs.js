const session = requireAuth("../pages/login.html");
renderNav({ active: "auditLogs", basePath: "../" });

const pageSize = 20;
let currentPage = 1;
let loadSeq = 0; // chỉ nhận kết quả của lần tải mới nhất (đổi bộ lọc nhanh không làm kết quả cũ ghi đè kết quả mới)
let catalog = { entities: [], actions: [], outcomes: [] };
const detailModal = bootstrap.Modal.getOrCreateInstance(document.getElementById("auditDetailModal"));

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

// Hiển thị theo giờ Việt Nam: khớp với cách lọc theo ngày (ngày lịch Việt Nam), không phụ thuộc múi giờ máy người xem.
function formatDateTime(value) {
  if (!value) return "-";
  // API trả giờ UTC không kèm "Z" -> thêm "Z" để trình duyệt đổi đúng.
  const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
}

// Nhãn hiển thị lấy từ DANH MỤC của máy chủ (một nguồn duy nhất, đối chiếu toàn bộ chức năng hệ thống).
const labelOf = (list, code) => list.find((x) => x.code === code)?.label || code;
const actionLabel = (code) => labelOf(catalog.actions, code);
const entityLabel = (code) => labelOf(catalog.entities, code);

const OUTCOME_BADGE = {
  Success: ["text-bg-success", "Thành công", "bi-check-circle"],
  Rejected: ["text-bg-warning", "Bị từ chối", "bi-slash-circle"],
  Failed: ["text-bg-danger", "Thất bại", "bi-x-octagon"],
};

function outcomeBadge(item) {
  const [css, label, icon] = OUTCOME_BADGE[item.outcome] || ["text-bg-secondary", item.outcome, "bi-question-circle"];
  const title = item.outcome === "Success" ? "Nghiệp vụ đã thực hiện"
    : item.outcome === "Rejected" ? "Yêu cầu bị từ chối — KHÔNG có thay đổi dữ liệu" : "Lỗi hệ thống — thao tác không hoàn tất";
  return `<span class="badge ${css}" title="${escapeHtml(title)}"><i class="bi ${icon}"></i> ${escapeHtml(label)}${item.statusCode && item.outcome !== "Success" ? ` · ${item.statusCode}` : ""}</span>`;
}

// Màu theo tính chất hành động: xanh lá = thêm/duyệt/hoàn tất, vàng = sửa/thay đổi, đỏ = xoá/từ chối/thất bại, xanh dương = truy cập/xem/xuất.
const ACTION_STYLE = {
  success: ["Create", "Approve", "Complete", "Generate", "Assign", "Backup", "Upload"],
  warning: ["Update", "UpdateStatus", "UpdatePriority", "Upsert", "UpdatePermissions", "SetSubStatus", "ResetPassword", "ResendActivation", "Return", "Propose"],
  danger: ["Delete", "Reject", "LoginFailed", "Unassign", "ResetPermissions", "RestoreDefaults", "Restore"],
  info: ["Login", "Logout", "ActivateAccount", "ChangePassword", "Print", "Export", "Download"],
};
const ACTION_ICON = { success: "bi-plus-circle", warning: "bi-pencil", danger: "bi-x-circle", info: "bi-box-arrow-in-right" };

function actionBadge(action) {
  const tone = Object.keys(ACTION_STYLE).find((t) => ACTION_STYLE[t].includes(action)) || "slate";
  return `<span class="badge-soft ${tone}"><i class="bi ${ACTION_ICON[tone] || "bi-circle"}"></i> ${escapeHtml(actionLabel(action))}</span>`;
}

function fillOptions(selectId, items, withCount = false) {
  document.getElementById(selectId).innerHTML = '<option value="">-- Tất cả --</option>' + items.map((x) =>
    `<option value="${escapeHtml(x.code)}">${escapeHtml(x.label)}${x.known === false ? " (mã cũ)" : ""}${withCount ? ` · ${x.count}` : ""}</option>`).join("");
}

async function loadCatalogAndActors() {
  try {
    const [cat, actors] = await Promise.all([api.get("/audit-logs/catalog"), api.get("/audit-logs/actors")]);
    catalog = cat;
    try {
      const matrix = await api.get("/permissions/matrix");
      permissionLabels = Object.fromEntries(matrix.groups.flatMap((g) => g.items.map((i) => [i.code, i.label])));
    } catch { /* không có quyền xem ma trận: hiện mã quyền */ }
    fillOptions("action", catalog.actions);
    fillOptions("entityName", catalog.entities);
    fillOptions("outcome", catalog.outcomes);
    // Email hiển thị ở đây vì chỉ người giữ quyền xem nhật ký (Admin IT) gọi được API này; tài khoản đã xoá không có email.
    document.getElementById("userId").innerHTML = '<option value="">-- Tất cả --</option>' + (actors || []).map((a) =>
      `<option value="${a.id}">${escapeHtml(a.name)}${a.isDeleted ? " (đã xoá)" : a.email ? ` — ${escapeHtml(a.email)}` : ""}</option>`).join("");
  } catch (err) {
    showError(`Không tải được danh mục bộ lọc: ${err.message}`);
  }
}

function formatJson(raw) {
  if (!raw) return null;
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

const AUDIT_FILTERS = ["keyword", "fromDate", "toDate", "userId", "outcome", "action", "entityName", "sort"];

function buildQuery(page) {
  return auditFilterBar.params(AUDIT_FILTERS, { page: String(page), pageSize: String(pageSize) }).toString();
}

async function loadAuditLogs(page = 1) {
  const seq = ++loadSeq;
  clearError();
  document.getElementById("auditTableBody").innerHTML =
    '<tr><td colspan="6" class="text-center text-muted py-4"><span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Đang tải...</td></tr>';
  try {
    const result = await api.get(`/audit-logs?${buildQuery(page)}`);
    if (seq !== loadSeq) return;
    // Đang ở trang cuối rồi đổi bộ lọc làm số trang giảm: nhảy về trang cuối còn dữ liệu thay vì hiện trang trống.
    if (!(result.items || []).length && page > 1 && result.totalPages > 0) {
      loadAuditLogs(result.totalPages);
      return;
    }
    currentPage = result.page;
    renderTable(result.items || []);
    renderPagination(result);
    document.getElementById("totalItemsLabel").textContent = result.totalItems
      ? `Tổng cộng: ${result.totalItems} nhật ký`
      : "";
  } catch (err) {
    if (seq !== loadSeq) return;
    showError(err.message);
    document.getElementById("auditTableBody").innerHTML =
      '<tr><td colspan="6"><div class="empty-state"><i class="bi bi-exclamation-triangle"></i><div class="title">Không tải được nhật ký</div><button class="btn btn-sm btn-outline-primary mt-2" onclick="loadAuditLogs(currentPage)"><i class="bi bi-arrow-clockwise"></i> Thử lại</button></div></td></tr>';
    document.getElementById("pagination").innerHTML = "";
  }
}

function renderActor(item) {
  if (!item.userName) return `<span class="text-muted">Tài khoản không xác định</span>`;
  const identity = item.userIsDeleted ? "tài khoản đã xoá" : item.userEmail || "";
  return `<div class="fw-semibold">${escapeHtml(item.userName)}${item.actorRole ? ` <span class="badge text-bg-light border fw-normal">${escapeHtml(item.actorRole)}</span>` : ""}</div>
    <div class="text-muted small">${escapeHtml(identity)}</div>`;
}

function renderTable(items) {
  const body = document.getElementById("auditTableBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="6"><div class="empty-state"><i class="bi bi-inbox"></i><div class="title">Không có nhật ký phù hợp</div>' +
      '<button class="btn btn-sm btn-outline-secondary mt-2" onclick="document.getElementById(\'resetFilterBtn\').click()"><i class="bi bi-x-circle"></i> Xoá bộ lọc</button></div></td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr class="${item.outcome === "Success" ? "" : "table-light"}">
      <td class="text-nowrap">${escapeHtml(formatDateTime(item.timestamp))}</td>
      <td>${renderActor(item)}</td>
      <td>${actionBadge(item.action)}</td>
      <td><span class="fw-semibold">${escapeHtml(entityLabel(item.entityName))}</span></td>
      <td>${outcomeBadge(item)}${item.message ? `<div class="small text-muted mt-1">${escapeHtml(item.message)}</div>` : ""}</td>
      <td class="text-end">
        <button class="btn btn-sm btn-outline-primary" onclick="viewDetail(${item.id})"><i class="bi bi-eye"></i> Chi tiết</button>
      </td>
    </tr>`).join("");
}

// Phân trang rút gọn (đầu, cuối, vài trang quanh trang hiện tại): không dựng hàng trăm nút khi có nhiều trang.
function renderPagination(result) {
  const el = document.getElementById("pagination");
  el.innerHTML = "";
  const total = result.totalPages;
  if (total <= 1) return;
  const pages = new Set([1, total]);
  for (let p = result.page - 2; p <= result.page + 2; p += 1) if (p >= 1 && p <= total) pages.add(p);
  let prev = 0;
  for (const page of [...pages].sort((a, b) => a - b)) {
    if (page - prev > 1) {
      const gap = document.createElement("li");
      gap.className = "page-item disabled";
      gap.innerHTML = '<span class="page-link">…</span>';
      el.appendChild(gap);
    }
    const li = document.createElement("li");
    li.className = `page-item ${page === result.page ? "active" : ""}`;
    li.innerHTML = `<button class="page-link" type="button">${page}</button>`;
    li.querySelector("button").addEventListener("click", () => loadAuditLogs(page));
    el.appendChild(li);
    prev = page;
  }
}

function integrityBadge(value) {
  const map = {
    Valid: ["text-bg-success", "Chữ ký hợp lệ — bản ghi chưa bị sửa"],
    Tampered: ["text-bg-danger", "BẢN GHI ĐÃ BỊ SỬA SAU KHI GHI (chữ ký không khớp)"],
    Unsigned: ["text-bg-secondary", "Bản ghi cũ, chưa có chữ ký"],
  };
  const [css, label] = map[value] || ["text-bg-secondary", value || "-"];
  return `<span class="badge ${css}">${escapeHtml(label)}</span>`;
}

// ---------- Hiển thị dữ liệu thay đổi / thông tin đã nhập theo ngôn ngữ người dùng ----------
const FIELD_LABELS = {
  name: "Tên", fullName: "Họ tên", email: "Email", phone: "Số điện thoại", description: "Mô tả", notes: "Ghi chú", note: "Ghi chú",
  departmentName: "Phòng ban", departmentId: "Phòng ban (mã)", categoryName: "Loại tài sản", categoryId: "Loại tài sản (mã)",
  roleName: "Vai trò", role: "Vai trò", roleId: "Vai trò (mã)", isActive: "Đang hoạt động", isDeleted: "Đã xoá", deleted: "Đã xoá",
  status: "Trạng thái", assetCode: "Mã tài sản", assetName: "Tên tài sản", serialNumber: "Số serial", purchaseDate: "Ngày mua",
  warrantyExpiry: "Hết bảo hành", position: "Chức danh", employeeCode: "Mã nhân viên", recipientName: "Người nhận",
  allocatedDate: "Ngày giao", returnedDate: "Ngày thu hồi", softwareName: "Tên phần mềm", licenseKey: "Khoá license", expiryDate: "Ngày hết hạn",
  maxUsage: "Số máy tối đa", priority: "Mức độ", technicianName: "Kỹ thuật viên", technicianId: "Kỹ thuật viên (mã)", title: "Tiêu đề",
  condition: "Tình trạng", reason: "Lý do", color: "Màu", sortOrder: "Thứ tự", code: "Mã", isSystem: "Trạng thái hệ thống",
  maxAgeYears: "Tuổi tối đa (năm)", maxFailureCount: "Số lần lỗi tối đa", additionalHeadcount: "Nhân sự thay đổi", ratio: "Định mức",
  unitPrice: "Đơn giá", year: "Năm", format: "Định dạng", permissions: "Quyền", added: "Quyền được thêm", removed: "Quyền bị gỡ",
  handoverNote: "Ghi chú bàn giao", handoverReason: "Lý do bàn giao", handoverLocation: "Địa điểm bàn giao", handoverCondition: "Tình trạng bàn giao",
  returnCondition: "Tình trạng khi thu hồi", returnNote: "Ghi chú thu hồi", currentPassword: "Mật khẩu hiện tại", newPassword: "Mật khẩu mới",
  assetId: "Tài sản (mã)", releasedLicenses: "License đã gỡ khỏi tài sản", userId: "Người dùng (mã)", licenseId: "License (mã)", employeeId: "Nhân viên (mã)", confirmText: "Xác nhận", file: "Tệp", length: "Dung lượng (byte)", report: "Báo cáo", comment: "Nhận xét", mustChangePassword: "Phải đổi mật khẩu",
};
const REPORT_NAMES = { assets: "Tồn kho thiết bị", "maintenance-due": "Thiết bị sắp bảo trì", "short-lifecycle": "Thiết bị vòng đời ngắn", licenses: "Phần mềm & Giấy phép" };
const VALUE_TEXT = {
  reason: { WrongPassword: "Sai mật khẩu", AccountLocked: "Tài khoản đang bị khoá do nhập sai nhiều lần" },
  format: { excel: "Excel", pdf: "PDF" },
  status: { InUse: "Đang sử dụng", Maintenance: "Đang bảo trì", Broken: "Hỏng", Disposed: "Đã thanh lý", Pending: "Chờ xử lý", Completed: "Hoàn tất", Failed: "Không sửa được", Allocated: "Đang phân bổ", Returned: "Đã thu hồi", Inspected: "Đã kiểm tra", Proposed: "Đã đề xuất", Approved: "Đã duyệt", Rejected: "Từ chối" },
  role: { "Admin IT": "Admin IT", Manager: "Quản lý", Technician: "Nhân viên kỹ thuật", Employee: "Nhân viên" },
};
// Nhãn quyền (mã -> tên) lấy từ ma trận phân quyền; không tải được thì hiện mã.
let permissionLabels = {};
const PERMISSION_KEYS = new Set(["added", "removed", "permissions"]);

// Khoá chỉ dùng cho kỹ thuật (mã nội bộ) — ẩn khỏi người dùng.
const HIDDEN_KEYS = new Set(["id", "requestId", "version", "revision", "activeAssetId", "passwordHash"]);

function humanizeKey(key) {
  if (FIELD_LABELS[key]) return FIELD_LABELS[key];
  const spaced = key.replace(/([a-z0-9])([A-Z])/g, "$1 $2").toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

function humanizeValue(key, value) {
  if (value === null || value === undefined || value === "") return "(trống)";
  if (typeof value === "boolean") return value ? "Có" : "Không";
  if (Array.isArray(value)) return value.length ? value.map((v) => humanizeValue(key, v)).join(", ") : "(không có)";
  if (typeof value === "object") {
    return Object.entries(value).filter(([k]) => !HIDDEN_KEYS.has(k)).map(([k, v]) => `${humanizeKey(k)}: ${humanizeValue(k, v)}`).join("; ") || "(trống)";
  }
  if (typeof value === "string") {
    if (key === "report") {
      const seg = value.split("/").filter(Boolean);
      const i = seg.indexOf("reports");
      return REPORT_NAMES[seg[i + 1]] || value;
    }
    if (PERMISSION_KEYS.has(key) && permissionLabels[value]) return permissionLabels[value];
    if (key === "roleName") key = "role";
    if (VALUE_TEXT[key] && VALUE_TEXT[key][value]) return VALUE_TEXT[key][value];
    const iso = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(value);
    if (iso) return iso[4] ? `${iso[3]}/${iso[2]}/${iso[1]} ${iso[4]}:${iso[5]}` : `${iso[3]}/${iso[2]}/${iso[1]}`;
  }
  return String(value);
}

// Gộp đối tượng lồng nhau (vd "request": {...}) thành các dòng phẳng; bỏ khoá kỹ thuật.
function flattenEntries(obj) {
  const rows = [];
  for (const [key, value] of Object.entries(obj || {})) {
    if (HIDDEN_KEYS.has(key)) continue;
    if (key === "request" && value && typeof value === "object" && !Array.isArray(value)) rows.push(...flattenEntries(value));
    else rows.push([key, value]);
  }
  return rows;
}

function parseJson(raw) {
  if (!raw) return null;
  try { return JSON.parse(raw); } catch { return undefined; }
}

function keyValueTable(rows, headers) {
  return `<div class="table-responsive"><table class="table table-sm mb-0"><thead><tr>${headers.map((h) => `<th>${escapeHtml(h)}</th>`).join("")}</tr></thead><tbody>${rows.join("")}</tbody></table></div>`;
}

function rawDetails(raw) {
  return raw ? `<details class="mt-2"><summary class="small text-muted" style="cursor:pointer">Xem dữ liệu gốc (dành cho kỹ thuật)</summary><pre class="bg-light border rounded p-3 small mt-2 mb-0" style="white-space:pre-wrap">${escapeHtml(formatJson(raw))}</pre></details>` : "";
}

// "Nội dung thay đổi": sửa -> bảng Trước/Sau chỉ gồm các mục thật sự đổi; thêm mới -> các mục đã nhập; xoá -> các mục của bản ghi đã xoá.
function renderChanges(item) {
  const box = document.getElementById("detailChanges");
  if (item.outcome !== "Success") {
    box.innerHTML = `<div class="alert alert-warning mb-0"><i class="bi bi-info-circle"></i> Thao tác ${item.outcome === "Rejected" ? "bị từ chối" : "thất bại"} nên <strong>không có dữ liệu nào bị thay đổi</strong>.</div>`;
    return;
  }
  const oldObj = parseJson(item.oldValue);
  const newObj = parseJson(item.newValue);
  const isObj = (v) => v && typeof v === "object" && !Array.isArray(v);

  if (!item.oldValue && !item.newValue) {
    box.innerHTML = '<div class="text-muted">Thao tác này không làm thay đổi dữ liệu (chỉ xem, in hoặc xuất file).</div>';
    return;
  }
  if (isObj(newObj) && (Array.isArray(newObj.added) || Array.isArray(newObj.removed))) {
    const rows = flattenEntries(newObj).map(([k, v]) => `<tr><td class="fw-semibold">${escapeHtml(humanizeKey(k))}</td><td>${escapeHtml(humanizeValue(k, v))}</td></tr>`);
    box.innerHTML = keyValueTable(rows, ["Mục", "Nội dung"]);
    return;
  }
  if (isObj(oldObj) && isObj(newObj)) {
    const same = (a, b) => JSON.stringify(a ?? null) === JSON.stringify(b ?? null);
    const onlyDeletedFlag = Object.keys(newObj).every((k) => k === "deleted" || HIDDEN_KEYS.has(k));
    if (onlyDeletedFlag) {
      const rows = flattenEntries(oldObj).map(([k, v]) => `<tr><td class="fw-semibold">${escapeHtml(humanizeKey(k))}</td><td>${escapeHtml(humanizeValue(k, v))}</td></tr>`);
      box.innerHTML = `<div class="small text-muted mb-1">Dữ liệu đã bị xoá (nội dung trước khi xoá):</div>${keyValueTable(rows, ["Mục", "Giá trị"])}${rawDetails(item.oldValue)}`;
      return;
    }
    const rows = flattenEntries(newObj).filter(([k, v]) => !same(oldObj[k], v)).map(([k, v]) =>
      `<tr><td class="fw-semibold">${escapeHtml(humanizeKey(k))}</td><td class="text-danger">${escapeHtml(humanizeValue(k, oldObj[k]))}</td><td class="text-success">${escapeHtml(humanizeValue(k, v))}</td></tr>`);
    box.innerHTML = rows.length
      ? `<div class="small text-muted mb-1">Chỉ hiển thị các mục đã thay đổi.</div>${keyValueTable(rows, ["Mục", "Trước khi sửa", "Sau khi sửa"])}${rawDetails(item.newValue)}`
      : '<div class="text-muted">Không có mục nào thay đổi giá trị.</div>';
    return;
  }
  if (isObj(newObj) && !item.oldValue) {
    const rows = flattenEntries(newObj).map(([k, v]) => `<tr><td class="fw-semibold">${escapeHtml(humanizeKey(k))}</td><td>${escapeHtml(humanizeValue(k, v))}</td></tr>`);
    box.innerHTML = `<div class="small text-muted mb-1">Dữ liệu được thêm mới:</div>${keyValueTable(rows, ["Mục", "Giá trị"])}${rawDetails(item.newValue)}`;
    return;
  }
  if (isObj(oldObj) && !item.newValue) {
    const rows = flattenEntries(oldObj).map(([k, v]) => `<tr><td class="fw-semibold">${escapeHtml(humanizeKey(k))}</td><td>${escapeHtml(humanizeValue(k, v))}</td></tr>`);
    box.innerHTML = `<div class="small text-muted mb-1">Dữ liệu đã bị xoá (nội dung trước khi xoá):</div>${keyValueTable(rows, ["Mục", "Giá trị"])}${rawDetails(item.oldValue)}`;
    return;
  }
  // Dạng khác (danh sách, giá trị đơn): hiển thị gọn trước/sau.
  const line = (raw, key) => {
    const v = parseJson(raw);
    return v === undefined ? raw : isObj(v) ? flattenEntries(v).map(([k, x]) => `${humanizeKey(k)}: ${humanizeValue(k, x)}`).join("; ") : humanizeValue(key, v);
  };
  box.innerHTML = `<div class="row g-3">
    <div class="col-md-6"><h6 class="small text-muted">Trước</h6><div class="border rounded p-2 small">${escapeHtml(item.oldValue ? line(item.oldValue, "old") : "(trống)")}</div></div>
    <div class="col-md-6"><h6 class="small text-muted">Sau</h6><div class="border rounded p-2 small">${escapeHtml(item.newValue ? line(item.newValue, "new") : "(trống)")}</div></div></div>${rawDetails(item.newValue || item.oldValue)}`;
}

// "Thông tin người dùng đã nhập": các mục người dùng gửi lên (mật khẩu và dữ liệu nhạy cảm đã được hệ thống che từ trước).
function renderRequest(item) {
  const el = document.getElementById("detailRequest");
  const obj = parseJson(item.detail);
  if (!item.detail) { el.innerHTML = '<div class="text-muted">Không có thông tin nhập thêm.</div>'; return; }
  if (obj && typeof obj === "object" && !Array.isArray(obj)) {
    const rows = flattenEntries(obj).map(([k, v]) => `<tr><td class="fw-semibold" style="width:35%">${escapeHtml(humanizeKey(k))}</td><td>${escapeHtml(humanizeValue(k, v))}</td></tr>`);
    if (rows.length) {
      el.innerHTML = `<div class="table-responsive"><table class="table table-sm mb-0"><tbody>${rows.join("")}</tbody></table></div>${rawDetails(item.detail)}`;
      return;
    }
  }
  el.innerHTML = `<div class="text-muted">Không có thông tin nhập thêm.</div>${rawDetails(item.detail)}`;
}

async function viewDetail(id) {
  clearError();
  try {
    const item = await api.get(`/audit-logs/${id}`);
    document.getElementById("detailTimestamp").textContent = formatDateTime(item.timestamp);
    document.getElementById("detailUser").innerHTML = `${escapeHtml(item.userName || "Tài khoản không xác định")}${item.actorRole ? ` (${escapeHtml(item.actorRole)})` : ""}
      <div class="small text-muted">${escapeHtml(item.userIsDeleted ? "tài khoản đã xoá" : item.userEmail || "")}</div>`;
    document.getElementById("detailAction").innerHTML = actionBadge(item.action);
    document.getElementById("detailEntity").textContent = entityLabel(item.entityName);
    document.getElementById("detailOutcome").innerHTML = `${outcomeBadge(item)}${item.statusCode ? ` <span class="text-muted small">HTTP ${item.statusCode}</span>` : ""}`;
    document.getElementById("detailMessage").textContent = item.message || "-";
    document.getElementById("detailRequestId").textContent = item.requestId || "-";
    document.getElementById("detailIntegrity").innerHTML = integrityBadge(item.integrity);
    renderChanges(item);
    renderRequest(item);
    detailModal.show();
  } catch (err) {
    showError(err.message);
  }
}

window.viewDetail = viewDetail;
window.loadAuditLogs = loadAuditLogs;

document.getElementById("verifyBtn").addEventListener("click", async () => {
  const box = document.getElementById("integrityResult");
  const btn = document.getElementById("verifyBtn");
  btn.disabled = true;
  try {
    const r = await api.get("/audit-logs/integrity");
    const bad = r.tamperedIds.length > 0 || !r.immutabilityTriggersPresent;
    box.className = `alert ${bad ? "alert-danger" : "alert-success"} mt-3`;
    box.innerHTML = `<strong>${bad ? "Phát hiện bất thường" : "Toàn vẹn"}</strong>: đã kiểm tra ${r.checked} bản ghi — ${r.valid} hợp lệ, ${r.unsigned} bản ghi cũ chưa ký, ${r.tamperedIds.length} bị sửa${r.tamperedIds.length ? ` (Id: ${r.tamperedIds.join(", ")})` : ""}.
      Chốt chặn sửa/xoá ở database: ${r.immutabilityTriggersPresent ? "còn nguyên" : "<strong>ĐÃ MẤT</strong>"}.${r.truncated ? " (Chỉ kiểm tra phần đầu do quá nhiều bản ghi.)" : ""}`;
  } catch (err) {
    box.className = "alert alert-danger mt-3";
    box.textContent = err.message;
  } finally {
    btn.disabled = false;
  }
});

const auditFilterBar = initFilterBar({
  fields: AUDIT_FILTERS, uncounted: ["sort"], textFields: ["keyword"], onChange: () => loadAuditLogs(1),
});

if (!can("audit-logs.view")) {
  document.getElementById("forbiddenAlert").textContent = "Chỉ Admin IT được phép xem Nhật ký hệ thống.";
  document.getElementById("forbiddenAlert").classList.remove("d-none");
  document.getElementById("auditContent").classList.add("d-none");
} else {
  loadCatalogAndActors().then(() => loadAuditLogs());
}
