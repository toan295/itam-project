const session = requireAuth("login.html");
renderNav({ active: "allocations", basePath: "../" });

const canManage = can("allocations.manage");
let currentPage = 1;
let currentItems = [];
let assetSuggestionMap = new Map();
let employeeSuggestionMap = new Map();
let employeeSearchTimer;
let departments = [];
let searchTimer;
const createModal = new bootstrap.Modal(document.getElementById("createAllocationModal"));
const returnModal = new bootstrap.Modal(document.getElementById("returnAllocationModal"));
const createGuard = guardUnsavedChanges(document.getElementById("createAllocationModal"), document.getElementById("createAllocationForm"));
const returnGuard = guardUnsavedChanges(document.getElementById("returnAllocationModal"), document.getElementById("returnAllocationForm"));
const quickEmployeeModal = new bootstrap.Modal(document.getElementById("quickEmployeeModal"));

function todayIso() {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 10);
}

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

function showModalError(id, message) {
  const el = document.getElementById(id);
  el.textContent = message;
  el.classList.remove("d-none");
}

function hideModalError(id) {
  document.getElementById(id).classList.add("d-none");
}

async function loadDepartments() {
  try {
    departments = await api.get("/departments");
    const filter = document.getElementById("departmentId");
    const options = departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");
    filter.innerHTML = '<option value="">-- Tất cả --</option>' + options;
    document.getElementById("quickEmployeeDepartment").innerHTML = options;

    if (session.role === "Manager") {
      filter.value = String(session.departmentId);
      filter.disabled = true;
      document.getElementById("quickEmployeeDepartment").value = String(session.departmentId);
      document.getElementById("quickEmployeeDepartment").disabled = true;
    }
  } catch (err) {
    showError(err.message);
  }
}

const ALLOCATION_FILTERS = ["keyword", "departmentId", "status", "filterCategoryId", "filterAllocatedFrom", "filterAllocatedTo",
  "filterHoldingOverDays", "filterHandoverCondition", "filterReturnCondition", "filterSort"];
const ALLOCATION_PARAMS = {
  keyword: "keyword", departmentId: "departmentId", status: "status", filterCategoryId: "categoryId",
  filterAllocatedFrom: "allocatedFrom", filterAllocatedTo: "allocatedTo", filterHoldingOverDays: "holdingOverDays",
  filterHandoverCondition: "handoverCondition", filterReturnCondition: "returnCondition", filterSort: "sort",
};
let filterBar;

function buildParams(page) {
  return filterBar.params(ALLOCATION_PARAMS, { page, pageSize: 10 });
}

async function loadCategoryFilter() {
  try {
    const categories = await api.get("/asset-categories");
    document.getElementById("filterCategoryId").innerHTML = '<option value="">-- Tất cả --</option>' +
      categories.map((c) => `<option value="${c.id}">${escapeHtml(c.name)}</option>`).join("");
  } catch {
    // Bộ lọc phụ — không chặn trang nếu không tải được.
  }
}

let loadSeq = 0; // chỉ nhận kết quả của lần tải mới nhất

async function loadAllocations(page = 1) {
  const seq = ++loadSeq;
  currentPage = page;
  clearError();
  document.getElementById("allocationTableBody").innerHTML =
    '<tr><td colspan="8" class="text-center text-muted py-4"><span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Đang tải...</td></tr>';
  try {
    const result = await api.get(`/allocations?${buildParams(page)}`);
    if (seq !== loadSeq) return;
    if ((result.items || []).length === 0 && page > 1 && result.totalPages > 0) {
      loadAllocations(result.totalPages);
      return;
    }
    currentItems = result.items || [];
    renderTable(currentItems);
    renderPagination(result.page, result.totalPages);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} lượt phân bổ`;
  } catch (err) {
    if (seq !== loadSeq) return;
    currentItems = [];
    renderTable([], true);
    renderPagination(1, 0);
    showError(err.message);
  }
}

function renderTable(items, loadFailed = false) {
  const tbody = document.getElementById("allocationTableBody");
  if (loadFailed) {
    tbody.innerHTML = '<tr><td colspan="8"><div class="empty-state"><i class="bi bi-exclamation-triangle"></i><div class="title">Không tải được danh sách phân bổ</div><button class="btn btn-sm btn-outline-primary mt-2" onclick="loadAllocations(currentPage)"><i class="bi bi-arrow-clockwise"></i> Thử lại</button></div></td></tr>';
    return;
  }
  if (!items.length) {
    tbody.innerHTML = '<tr><td colspan="8"><div class="empty-state"><i class="bi bi-inbox"></i><div class="title">Không có lượt phân bổ phù hợp</div><button class="btn btn-sm btn-outline-secondary mt-2" onclick="document.getElementById(\'resetFilterBtn\').click()"><i class="bi bi-x-circle"></i> Xoá bộ lọc</button></div></td></tr>';
    return;
  }

  tbody.innerHTML = items.map((item) => {
    const isOpen = item.status === "Allocated" && !item.returnedDate;
    const statusHtml = isOpen
      ? '<span class="badge-soft success"><i class="bi bi-box-arrow-up-right"></i> Đang phân bổ</span>'
      : '<span class="badge-soft slate"><i class="bi bi-box-arrow-in-down-left"></i> Đã thu hồi</span>';

    return `<tr>
      <td class="fw-semibold">${escapeHtml(item.assetCode)}</td>
      <td>${escapeHtml(item.departmentName)}
        ${isOpen && item.employeeCurrentDepartmentName && item.employeeCurrentDepartmentName !== item.departmentName ? `<div class="small text-muted" title="Phòng ban hiện tại của nhân viên khác phòng ban ghi nhận lúc giao">Nhân viên hiện ở: ${escapeHtml(item.employeeCurrentDepartmentName)}</div>` : ""}</td>
      <td>
        <div class="fw-semibold">${escapeHtml(item.recipientName)}</div>
        <div class="small text-muted">${item.employeeCode ? escapeHtml(item.employeeCode) : "Chưa gắn hồ sơ"}${item.employeePosition ? ` · ${escapeHtml(item.employeePosition)}` : ""}</div>
      </td>
      <td>${escapeHtml(DateVi.toDisplay(item.allocatedDate))}</td>
      <td>${item.returnedDate ? escapeHtml(DateVi.toDisplay(item.returnedDate)) : '<span class="text-muted">-</span>'}</td>
      <td class="text-nowrap">${item.holdingDays} ngày<div class="small text-muted">${isOpen ? "đang giữ" : "đã giữ"}</div></td>
      <td>${statusHtml}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-outline-secondary me-1" onclick="printAllocation(${item.id}, 'handover')"><i class="bi bi-printer"></i> BB bàn giao</button>
        ${item.returnedDate ? `<button class="btn btn-sm btn-outline-secondary me-1" onclick="printAllocation(${item.id}, 'return')"><i class="bi bi-printer"></i> BB thu hồi</button>` : ""}
        ${canManage && isOpen ? `<button class="btn btn-sm btn-outline-primary" onclick="openReturnModal(${item.id})"><i class="bi bi-arrow-return-left"></i> Thu hồi</button>` : ""}
      </td>
    </tr>`;
  }).join("");
}

function renderPagination(page, totalPages) {
  const el = document.getElementById("pagination");
  let html = "";
  for (let i = 1; i <= totalPages; i++) {
    html += `<li class="page-item ${i === page ? "active" : ""}"><a class="page-link" href="#" onclick="loadAllocations(${i}); return false;">${i}</a></li>`;
  }
  el.innerHTML = html;
}

async function loadOverdue() {
  if (!canManage) return;
  const threshold = document.getElementById("overdueThreshold").value;
  const severity = document.getElementById("overdueSeverity").value;
  try {
    const params = new URLSearchParams({ thresholdDays: threshold });
    if (severity) params.set("severity", severity);
    const items = await api.get(`/allocations/overdue?${params}`);
    renderOverdue(items || [], Number(threshold), Boolean(severity));
  } catch (err) {
    showError(err.message);
  }
}

const OVERDUE_SOURCE = { LastMaintenance: "lần bảo trì hoàn tất gần nhất", PurchaseDate: "ngày mua (chưa từng bảo trì)", AllocatedDate: "ngày giao (thiếu ngày mua và lịch sử bảo trì)" };

function renderOverdue(items, threshold, filtered) {
  const section = document.getElementById("overdueSection");
  // Còn bộ lọc mức ưu tiên đang bật thì vẫn hiện khung để người dùng bỏ lọc, kể cả khi kết quả rỗng.
  if (!items.length && !filtered && threshold === 180) {
    section.classList.add("d-none");
    return;
  }

  section.classList.remove("d-none");
  document.getElementById("overdueCount").textContent = `${items.length} tài sản cần chú ý`;
  document.getElementById("overdueRule").textContent =
    `Tài sản đang giao cho người dùng nhưng đã quá ${threshold} ngày chưa được bảo trì (tính từ lần bảo trì gần nhất, nếu chưa có thì từ ngày mua). Hãy nhờ kỹ thuật viên kiểm tra, bảo dưỡng định kỳ. Bấm vào một dòng để xem lịch sử phân bổ của tài sản đó.`;
  document.getElementById("overdueList").innerHTML = items.length === 0
    ? '<div class="border-top py-2 text-muted">Không có tài sản nào theo bộ lọc này.</div>'
    : items.map((item) => {
      const high = item.severity === "High";
      return `<button type="button" class="btn btn-link text-start text-decoration-none w-100 border-top rounded-0 p-0 py-2" onclick="focusAllocationByAsset('${escapeHtml(item.assetCode)}')">
        <div class="d-flex flex-column flex-md-row justify-content-between gap-1">
          <span class="text-truncate"><strong>${escapeHtml(item.assetCode)}</strong> - ${escapeHtml(item.assetName)} · ${escapeHtml(item.recipientName)} · ${escapeHtml(item.departmentName)}</span>
          <span class="badge text-bg-${high ? "danger" : "warning"} align-self-start flex-shrink-0">${high ? "Ưu tiên cao · " : ""}${item.daysSinceLastMaintenance} ngày</span>
        </div>
        <div class="text-muted">Mốc tính: ${escapeHtml(OVERDUE_SOURCE[item.referenceSource] || item.referenceSource)} — ${escapeHtml(DateVi.toDisplay(item.referenceDate))}</div>
      </button>`;
    }).join("");
}

// Bấm một cảnh báo: lọc danh sách phân bổ theo đúng tài sản (chỉ lượt đang giữ) và cuộn tới bảng.
function focusAllocationByAsset(assetCode) {
  document.getElementById("resetFilterBtn").click();
  document.getElementById("keyword").value = assetCode;
  document.getElementById("status").value = "Allocated";
  filterBar.refreshBadge();
  loadAllocations(1).then(() => document.getElementById("allocationTableBody").scrollIntoView({ behavior: "smooth", block: "start" }));
}

document.getElementById("overdueThreshold").addEventListener("change", loadOverdue);
document.getElementById("overdueSeverity").addEventListener("change", loadOverdue);

async function searchAssets(keyword = "") {
  try {
    const params = new URLSearchParams({ status: "InUse", page: 1, pageSize: 20 });
    if (keyword.trim()) params.set("keyword", keyword.trim());
    const result = await api.get(`/assets/search?${params}`);
    assetSuggestionMap = new Map();
    const list = document.getElementById("assetSuggestions");
    list.innerHTML = (result.items || []).map((asset) => {
      const label = `${asset.assetCode} — ${asset.name}`;
      assetSuggestionMap.set(label, asset.id);
      return `<option value="${escapeHtml(label)}"></option>`;
    }).join("");
    syncSelectedAsset();
  } catch {
    assetSuggestionMap = new Map();
  }
}

function syncSelectedAsset() {
  const label = document.getElementById("assetSearchInput").value;
  const id = assetSuggestionMap.get(label);
  document.getElementById("selectedAssetId").value = id ? String(id) : "";
}

function openCreateModal() {
  document.getElementById("createAllocationForm").reset();
  document.getElementById("selectedAssetId").value = "";
  document.getElementById("selectedEmployeeId").value = "";
  DateVi.set(document.getElementById("allocatedDate"), todayIso());
  document.getElementById("selectedEmployeeInfo").textContent = "Phòng ban nhận và chức danh lấy theo hồ sơ nhân viên.";
  hideModalError("createModalError");
  searchAssets();
  searchEmployees();
  createModal.show();
}

function openReturnModal(id) {
  document.getElementById("returnAllocationForm").reset();
  document.getElementById("returnAllocationId").value = String(id);
  DateVi.set(document.getElementById("returnedDate"), todayIso());
  document.getElementById("conditionGood").checked = true;
  hideModalError("returnModalError");
  returnModal.show();
}

// ===== Người nhận: chọn từ danh mục nhân viên =====

function employeeLabel(e) {
  return `${e.employeeCode} — ${e.fullName}${e.position ? ` · ${e.position}` : ""} (${e.departmentName})`;
}

async function searchEmployees(keyword = "") {
  try {
    const params = new URLSearchParams({ isActive: "true", page: 1, pageSize: 20 });
    if (keyword.trim()) params.set("keyword", keyword.trim());
    const result = await api.get(`/employees?${params}`);
    employeeSuggestionMap = new Map();
    document.getElementById("employeeSuggestions").innerHTML = (result.items || []).map((e) => {
      const label = employeeLabel(e);
      employeeSuggestionMap.set(label, e);
      return `<option value="${escapeHtml(label)}"></option>`;
    }).join("");
    syncSelectedEmployee();
  } catch {
    employeeSuggestionMap = new Map();
  }
}

function syncSelectedEmployee() {
  const employee = employeeSuggestionMap.get(document.getElementById("employeeSearchInput").value);
  document.getElementById("selectedEmployeeId").value = employee ? String(employee.id) : "";
  document.getElementById("selectedEmployeeInfo").textContent = employee
    ? `Bên nhận: ${employee.fullName} — ${employee.position || "chưa có chức danh"} — ${employee.departmentName}`
    : "Phòng ban nhận và chức danh lấy theo hồ sơ nhân viên.";
}

document.getElementById("employeeSearchInput").addEventListener("input", (event) => {
  clearTimeout(employeeSearchTimer);
  syncSelectedEmployee();
  if (document.getElementById("selectedEmployeeId").value) return; // đã chọn đúng một nhân viên -> không tìm lại.
  employeeSearchTimer = setTimeout(() => searchEmployees(event.target.value), 250);
});
document.getElementById("employeeSearchInput").addEventListener("change", syncSelectedEmployee);

// Chỉ người có quyền tạo nhân viên mới thấy nút thêm nhanh (backend vẫn chặn 403 nếu gọi API trực tiếp).
if (!can("employees.manage")) document.getElementById("quickAddEmployeeBtn").classList.add("d-none");

document.getElementById("quickAddEmployeeBtn").addEventListener("click", () => {
  document.getElementById("quickEmployeeForm").reset();
  hideModalError("quickEmployeeError");
  document.getElementById("quickEmployeeName").value = document.getElementById("employeeSearchInput").value.trim();
  if (session.role === "Manager") document.getElementById("quickEmployeeDepartment").value = String(session.departmentId);
  quickEmployeeModal.show();
});

document.getElementById("quickEmployeeForm").addEventListener("submit", (event) => {
  event.preventDefault();
  runLocked(event.currentTarget, async () => {
  hideModalError("quickEmployeeError");
  const fullName = document.getElementById("quickEmployeeName").value.trim();
  if (!fullName) {
    showModalError("quickEmployeeError", "Vui lòng nhập họ tên nhân viên.");
    return;
  }
  try {
    const created = await api.post("/employees", {
      fullName: document.getElementById("quickEmployeeName").value.trim(),
      position: document.getElementById("quickEmployeePosition").value.trim() || null,
      departmentId: Number(document.getElementById("quickEmployeeDepartment").value),
      isActive: true,
    });
    quickEmployeeModal.hide();
    await searchEmployees(created.fullName);
    document.getElementById("employeeSearchInput").value = employeeLabel(created);
    syncSelectedEmployee();
  } catch (err) {
    showModalError("quickEmployeeError", err.message);
  }
  });
});

// ===== In biên bản theo mẫu "Biên bản bàn giao tài sản" =====

const DOTS = "………………………………";

function dotted(value, fallback = DOTS) {
  return value && String(value).trim() ? escapeHtml(value) : fallback;
}

function formatVnDate(iso) {
  if (!iso) return { day: "…", month: "…", year: "……", short: "…/…/……" };
  const [y, m, d] = iso.split("-");
  return { day: d, month: m, year: y, short: `${d}/${m}/${y}` };
}

function formatMoney(value) {
  return value === null || value === undefined ? "" : `${Number(value).toLocaleString("vi-VN")} đ`;
}

// Chức danh bên phía công ty lấy từ vai trò hệ thống -> hiển thị tên gọi tiếng Việt trên biên bản.
const ROLE_TITLES = { "Admin IT": "Quản trị viên IT", Manager: "Quản lý", Technician: "Nhân viên kỹ thuật" };

function partyBlock(title, party) {
  party = { ...party, position: ROLE_TITLES[party.position] || party.position };
  return `
    <p class="bold">${title}</p>
    <p>Ông/Bà: ${dotted(party.fullName)}</p>
    <p>Chức danh: ${dotted(party.position, "………………………")} &nbsp;&nbsp;&nbsp; Bộ phận: ${dotted(party.departmentName, "………………………")}</p>`;
}

function buildDocumentHtml(doc) {
  const isReturn = doc.kind === "Return";
  const date = formatVnDate(doc.documentDate);
  const title = isReturn ? "BIÊN BẢN THU HỒI TÀI SẢN" : "BIÊN BẢN BÀN GIAO TÀI SẢN";
  // Lý do nằm giữa câu ("Vì lý do ... nên Bên A...") nên hạ chữ cái đầu cho liền mạch.
  const rawReason = doc.reason || (isReturn ? "thu hồi tài sản về đơn vị quản lý" : "");
  const reason = rawReason ? rawReason.charAt(0).toLowerCase() + rawReason.slice(1) : "";
  const giverName = doc.giver.fullName || "…………………";
  const receiverName = doc.receiver.fullName || "…………………";
  const a = doc.asset;

  return `<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>${title}</title>
  <style>
    @page { size: A4; margin: 18mm 18mm 18mm 22mm; }
    body { font-family: "Times New Roman", Times, serif; font-size: 14px; line-height: 1.55; color: #000; }
    p { margin: 4px 0; } .center { text-align: center; } .bold { font-weight: bold; }
    h2 { text-align: center; margin: 22px 0 10px; font-size: 17px; }
    table { width: 100%; border-collapse: collapse; margin: 10px 0; }
    th, td { border: 1px solid #000; padding: 6px 5px; text-align: center; vertical-align: middle; }
    td.left { text-align: left; }
    .sign { display: flex; justify-content: space-between; margin-top: 18px; text-align: center; }
    .sign > div { width: 40%; } .sign .space { height: 90px; } .sign .italic { font-style: italic; }
    .toolbar { position: fixed; top: 8px; right: 12px; } @media print { .toolbar { display: none; } }
  </style></head><body>
  <div class="toolbar"><button onclick="window.print()">In</button></div>
  <p class="center bold">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</p>
  <p class="center bold">Độc lập - Tự do - Hạnh phúc</p>
  <h2>${title}</h2>
  <p>Hôm nay, ngày ${date.day} tháng ${date.month} năm ${date.year}, tại ${dotted(doc.location, "……………………………………………………")}</p>
  <p>Chúng tôi gồm:</p>
  ${partyBlock("I. Bên giao (Bên A):", doc.giver)}
  ${partyBlock("II. Bên nhận (Bên B):", doc.receiver)}
  <p class="bold">III. Nội dung bàn giao</p>
  <p>Vì lý do ${dotted(reason, "………………………")} nên Bên A (${escapeHtml(giverName)}) đã tiến hành bàn giao tài sản cho
     Bên B (${escapeHtml(receiverName)}) tại: ${dotted(doc.location, "……………………………")} theo bảng thống kê chi tiết sau:</p>
  <table>
    <thead><tr><th style="width:36px">STT</th><th>Tên tài sản</th><th>Đơn vị</th><th>Số lượng</th><th>Tình trạng</th><th>Thành tiền</th><th style="width:90px">Chữ ký nhận</th></tr></thead>
    <tbody><tr>
      <td>1</td>
      <td class="left"><strong>${escapeHtml(a.assetName)}</strong><br>Mã tài sản: ${escapeHtml(a.assetCode)}</td>
      <td>${escapeHtml(a.unit)}</td><td>${a.quantity}</td><td>${dotted(a.condition, "")}</td>
      <td>${formatMoney(a.amount)}</td><td></td>
    </tr></tbody>
  </table>
  ${doc.note ? `<p>Ghi chú: ${escapeHtml(doc.note)}</p>` : ""}
  <p>Bên A cam đoan rằng toàn bộ tài sản đã được bàn giao đầy đủ, đúng số lượng, chất lượng. Kể từ ngày ${date.short}, số tài sản trên
     sẽ do Bên B (${escapeHtml(receiverName)}) chịu trách nhiệm quản lý.</p>
  <p>Biên bản được lập thành 02 bản, mỗi bên giữ một bản.</p>
  <div class="sign">
    <div><p class="bold">Bên giao (Bên A)</p><p class="italic">(Ký, ghi rõ họ tên)</p><div class="space"></div><p class="bold">${escapeHtml(doc.giver.fullName || "")}</p></div>
    <div><p class="bold">Bên nhận (Bên B)</p><p class="italic">(Ký, ghi rõ họ tên)</p><div class="space"></div><p class="bold">${escapeHtml(doc.receiver.fullName || "")}</p></div>
  </div>
  <script>window.onload = () => window.print();<\/script></body></html>`;
}

async function printAllocation(id, kind = "handover") {
  clearError();
  // Mở cửa sổ ngay trong sự kiện click (trình duyệt chặn popup mở sau khi await), rồi mới nạp dữ liệu.
  const popup = window.open("", "_blank", "width=900,height=800");
  if (!popup) {
    showError("Trình duyệt đã chặn cửa sổ in. Hãy cho phép popup cho trang này rồi thử lại.");
    return;
  }

  try {
    const doc = await api.get(`/allocations/${id}/document?kind=${kind}`);
    popup.document.open();
    popup.document.write(buildDocumentHtml(doc));
    popup.document.close();
  } catch (err) {
    popup.close();
    showError(err.message);
  }
}

if (!canManage) {
  document.getElementById("openCreateBtn").style.display = "none";
}

document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);

document.getElementById("assetSearchInput").addEventListener("input", (event) => {
  clearTimeout(searchTimer);

  // Kiểm tra xem giá trị hiện tại có đúng là một item đã chọn không
  syncSelectedAsset();

  const selectedAssetId =
    document.getElementById("selectedAssetId").value;

  // Nếu đã chọn đúng một asset trong danh sách
  // thì KHÔNG search lại, tránh làm mất selectedAssetId
  if (selectedAssetId) {
    return;
  }

  // Chỉ tìm kiếm khi người dùng đang gõ
  searchTimer = setTimeout(() => {
    searchAssets(event.target.value);
  }, 250);
});

document.getElementById("assetSearchInput").addEventListener("change", syncSelectedAsset);

// Thông báo lỗi dễ hiểu theo loại lỗi; mất kết nối (không có mã HTTP) thì nói rõ kết quả chưa xác định.
function failureMessage(err, action) {
  if (!err.status) {
    return `Không nhận được phản hồi từ máy chủ — lượt ${action} có thể ĐÃ được ghi. Danh sách đã được tải lại; hãy kiểm tra, nếu chưa có thì bấm lại (gửi lại cùng một yêu cầu là an toàn, không bị ghi trùng).`;
  }
  return err.message;
}

document.getElementById("createAllocationForm").addEventListener("submit", (event) => {
  event.preventDefault();
  runLocked(event.currentTarget, async () => {
  hideModalError("createModalError");
  syncSelectedAsset();

  const assetId = Number(document.getElementById("selectedAssetId").value);
  if (!assetId) {
    showModalError("createModalError", "Vui lòng chọn một tài sản từ danh sách gợi ý.");
    return;
  }

  syncSelectedEmployee();
  const employeeId = Number(document.getElementById("selectedEmployeeId").value);
  if (!employeeId) {
    showModalError("createModalError", "Vui lòng chọn người nhận từ danh sách nhân viên (hoặc bấm \"Thêm mới\" nếu chưa có).");
    return;
  }

  if (!DateVi.value(document.getElementById("allocatedDate"))) {
    showModalError("createModalError", "Vui lòng nhập ngày giao hợp lệ (dd/mm/yyyy).");
    return;
  }

  const payload = {
    assetId,
    employeeId,
    allocatedDate: DateVi.value(document.getElementById("allocatedDate")),
    handoverCondition: document.getElementById("handoverCondition").value,
    handoverReason: document.getElementById("handoverReason").value.trim() || "Cấp phát tài sản phục vụ công việc",
    handoverLocation: document.getElementById("handoverLocation").value.trim() || null,
    handoverNote: document.getElementById("handoverNote").value.trim() || null,
  };

  try {
    await api.post("/allocations", payload);
    createGuard.markClean();
    createModal.hide();
    await Promise.all([loadAllocations(1), loadOverdue()]);
  } catch (err) {
    showModalError("createModalError", failureMessage(err, "phân bổ"));
    // Dữ liệu có thể đã đổi (tài sản vừa được cấp cho người khác) hoặc yêu cầu đã được ghi trước khi mất kết nối:
    // tải lại để người dùng thấy trạng thái thật.
    searchAssets(document.getElementById("assetSearchInput").value);
    loadAllocations(currentPage);
    loadOverdue();
  }
  });
});

document.getElementById("returnAllocationForm").addEventListener("submit", (event) => {
  event.preventDefault();
  runLocked(event.currentTarget, async () => {
  hideModalError("returnModalError");

  const id = Number(document.getElementById("returnAllocationId").value);
  const condition = document.querySelector('input[name="returnCondition"]:checked').value;
  if (!DateVi.value(document.getElementById("returnedDate"))) {
    showModalError("returnModalError", "Vui lòng nhập ngày thu hồi hợp lệ (dd/mm/yyyy).");
    return;
  }

  const payload = {
    returnedDate: DateVi.value(document.getElementById("returnedDate")),
    condition,
    returnNote: document.getElementById("returnNote").value.trim() || null,
  };

  try {
    await api.post(`/allocations/${id}/return`, payload);
    returnGuard.markClean();
    returnModal.hide();
    await Promise.all([loadAllocations(currentPage), loadOverdue()]);
  } catch (err) {
    showModalError("returnModalError", failureMessage(err, "thu hồi"));
    loadAllocations(currentPage);
    loadOverdue();
  }
  });
});

filterBar = initFilterBar({
  fields: ALLOCATION_FILTERS, uncounted: ["filterSort"], textFields: ["keyword", "filterHoldingOverDays"],
  onChange: () => loadAllocations(1),
  // Manager luôn bị giữ ở phòng ban của mình: form.reset() không được xoá lựa chọn cố định đó.
  onReset: () => { if (session.role === "Manager") document.getElementById("departmentId").value = String(session.departmentId); },
});

loadCategoryFilter();
loadDepartments().then(() => loadAllocations());
loadOverdue();
