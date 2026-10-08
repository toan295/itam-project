const session = requireAuth("login.html");
renderNav({ active: "allocations", basePath: "../" });

const canManage = session.role === "Admin IT" || session.role === "Manager";
let currentPage = 1;
let currentItems = [];
let assetSuggestionMap = new Map();
let employeeSuggestionMap = new Map();
let employeeSearchTimer;
let departments = [];
let searchTimer;
const createModal = new bootstrap.Modal(document.getElementById("createAllocationModal"));
const returnModal = new bootstrap.Modal(document.getElementById("returnAllocationModal"));
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

function buildParams(page) {
  const params = new URLSearchParams({ page, pageSize: 10 });
  const departmentId = document.getElementById("departmentId").value;
  const status = document.getElementById("status").value;
  const keyword = document.getElementById("keyword").value.trim();
  if (keyword) params.set("keyword", keyword);
  if (departmentId) params.set("departmentId", departmentId);
  if (status) params.set("status", status);
  return params;
}

async function loadAllocations(page = 1) {
  currentPage = page;
  clearError();
  try {
    const result = await api.get(`/allocations?${buildParams(page)}`);
    currentItems = result.items || [];
    renderTable(currentItems);
    renderPagination(result.page, result.totalPages);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} lượt phân bổ`;
  } catch (err) {
    currentItems = [];
    renderTable([]);
    renderPagination(1, 0);
    showError(err.message);
  }
}

function renderTable(items) {
  const tbody = document.getElementById("allocationTableBody");
  if (!items.length) {
    tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><i class="bi bi-inbox"></i><div class="title">Chưa có lịch sử phân bổ</div></div></td></tr>';
    return;
  }

  tbody.innerHTML = items.map((item) => {
    const isOpen = item.status === "Allocated" && !item.returnedDate;
    const statusHtml = isOpen
      ? '<span class="badge-soft success"><i class="bi bi-box-arrow-up-right"></i> Đang phân bổ</span>'
      : '<span class="badge-soft slate"><i class="bi bi-box-arrow-in-down-left"></i> Đã thu hồi</span>';

    return `<tr>
      <td><div class="fw-semibold">${escapeHtml(item.assetCode)}</div><div class="small text-muted">${escapeHtml(item.assetName)}</div></td>
      <td>${escapeHtml(item.departmentName)}</td>
      <td>
        <div class="fw-semibold">${escapeHtml(item.recipientName)}</div>
        <div class="small text-muted">${item.employeeCode ? escapeHtml(item.employeeCode) : "Chưa gắn hồ sơ"}${item.employeePosition ? ` · ${escapeHtml(item.employeePosition)}` : ""}</div>
      </td>
      <td>${escapeHtml(item.allocatedDate)}</td>
      <td>${item.returnedDate ? escapeHtml(item.returnedDate) : '<span class="text-muted">-</span>'}</td>
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
  try {
    const items = await api.get("/allocations/overdue?thresholdDays=180");
    renderOverdue(items || []);
  } catch (err) {
    showError(err.message);
  }
}

function renderOverdue(items) {
  const section = document.getElementById("overdueSection");
  if (!items.length) {
    section.classList.add("d-none");
    return;
  }

  section.classList.remove("d-none");
  document.getElementById("overdueCount").textContent = `${items.length} tài sản cần chú ý`;
  document.getElementById("overdueList").innerHTML = items.map((item) => {
    const badge = item.daysSinceLastMaintenance >= 365 ? "danger" : "warning";
    const last = item.lastMaintenanceDate || "Chưa từng bảo trì";
    return `<div class="d-flex justify-content-between border-top py-2">
      <span><strong>${escapeHtml(item.assetCode)}</strong> - ${escapeHtml(item.assetName)} · ${escapeHtml(item.recipientName)} · ${escapeHtml(item.departmentName)}</span>
      <span class="badge text-bg-${badge}">${item.daysSinceLastMaintenance} ngày · ${escapeHtml(last)}</span>
    </div>`;
  }).join("");
}

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
  document.getElementById("allocatedDate").value = todayIso();
  document.getElementById("selectedEmployeeInfo").textContent = "Phòng ban nhận và chức danh lấy theo hồ sơ nhân viên.";
  hideModalError("createModalError");
  searchAssets();
  searchEmployees();
  createModal.show();
}

function openReturnModal(id) {
  document.getElementById("returnAllocationForm").reset();
  document.getElementById("returnAllocationId").value = String(id);
  document.getElementById("returnedDate").value = todayIso();
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

document.getElementById("quickAddEmployeeBtn").addEventListener("click", () => {
  document.getElementById("quickEmployeeForm").reset();
  hideModalError("quickEmployeeError");
  document.getElementById("quickEmployeeName").value = document.getElementById("employeeSearchInput").value.trim();
  if (session.role === "Manager") document.getElementById("quickEmployeeDepartment").value = String(session.departmentId);
  quickEmployeeModal.show();
});

document.getElementById("quickEmployeeForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  hideModalError("quickEmployeeError");
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
const ROLE_TITLES = { "Admin IT": "Quản trị viên IT", Manager: "Quản lý phòng ban", Technician: "Kỹ thuật viên" };

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
    .sign > div { width: 32%; } .sign .space { height: 90px; } .sign .italic { font-style: italic; }
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
  <p>Biên bản được lập thành 03 bản, mỗi bên giữ một bản.</p>
  <div class="sign">
    <div><p class="bold">Bên giao (Bên A)</p><p class="italic">(Ký, ghi rõ họ tên)</p><div class="space"></div><p class="bold">${escapeHtml(doc.giver.fullName || "")}</p></div>
    <div><p class="bold">Bên nhận (Bên B)</p><p class="italic">(Ký, ghi rõ họ tên)</p><div class="space"></div><p class="bold">${escapeHtml(doc.receiver.fullName || "")}</p></div>
    <div><p class="bold">Bên làm chứng</p><p class="italic">(Ký, ghi rõ họ tên)</p><div class="space"></div></div>
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

document.getElementById("createAllocationForm").addEventListener("submit", async (event) => {
  event.preventDefault();
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

  const payload = {
    assetId,
    employeeId,
    allocatedDate: document.getElementById("allocatedDate").value,
    handoverCondition: document.getElementById("handoverCondition").value,
    handoverReason: document.getElementById("handoverReason").value.trim() || "Cấp phát tài sản phục vụ công việc",
    handoverLocation: document.getElementById("handoverLocation").value.trim() || null,
    handoverNote: document.getElementById("handoverNote").value.trim() || null,
  };

  try {
    await api.post("/allocations", payload);
    createModal.hide();
    await Promise.all([loadAllocations(1), loadOverdue()]);
  } catch (err) {
    showModalError("createModalError", err.message);
  }
});

document.getElementById("returnAllocationForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  hideModalError("returnModalError");

  const id = Number(document.getElementById("returnAllocationId").value);
  const condition = document.querySelector('input[name="returnCondition"]:checked').value;
  const payload = {
    returnedDate: document.getElementById("returnedDate").value,
    condition,
    returnNote: document.getElementById("returnNote").value.trim() || null,
  };

  try {
    await api.post(`/allocations/${id}/return`, payload);
    returnModal.hide();
    await Promise.all([loadAllocations(currentPage), loadOverdue()]);
  } catch (err) {
    showModalError("returnModalError", err.message);
  }
});

document.getElementById("filterForm").addEventListener("submit", (event) => {
  event.preventDefault();
  loadAllocations(1);
});

document.getElementById("resetFilterBtn").addEventListener("click", () => {
  document.getElementById("status").value = "";
  document.getElementById("keyword").value = "";
  if (session.role !== "Manager") document.getElementById("departmentId").value = "";
  loadAllocations(1);
});

loadDepartments().then(() => loadAllocations());
loadOverdue();
