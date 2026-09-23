const session = requireAuth("login.html");
renderNav({ active: "allocations", basePath: "../" });

const canManage = session.role === "Admin IT" || session.role === "Manager";
let currentPage = 1;
let currentItems = [];
let assetSuggestionMap = new Map();
let searchTimer;
const createModal = new bootstrap.Modal(document.getElementById("createAllocationModal"));
const returnModal = new bootstrap.Modal(document.getElementById("returnAllocationModal"));

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
    const departments = await api.get("/departments");
    const filter = document.getElementById("departmentId");
    const form = document.getElementById("allocationDepartmentId");
    filter.innerHTML = '<option value="">-- Tất cả --</option>' +
      departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");
    form.innerHTML = departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");

    if (session.role === "Manager") {
      filter.value = String(session.departmentId);
      filter.disabled = true;
      form.value = String(session.departmentId);
      form.disabled = true;
    }
  } catch (err) {
    showError(err.message);
  }
}

function buildParams(page) {
  const params = new URLSearchParams({ page, pageSize: 10 });
  const departmentId = document.getElementById("departmentId").value;
  const status = document.getElementById("status").value;
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
      <td>${escapeHtml(item.recipientName)}</td>
      <td>${escapeHtml(item.allocatedDate)}</td>
      <td>${item.returnedDate ? escapeHtml(item.returnedDate) : '<span class="text-muted">-</span>'}</td>
      <td>${statusHtml}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-outline-secondary me-1" onclick="printAllocation(${item.id})"><i class="bi bi-printer"></i> In biên bản</button>
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
  document.getElementById("allocatedDate").value = todayIso();
  if (session.role === "Manager") {
    document.getElementById("allocationDepartmentId").value = String(session.departmentId);
  }
  hideModalError("createModalError");
  searchAssets();
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

function printAllocation(id) {
  const item = currentItems.find((x) => x.id === id);
  if (!item) return;

  const popup = window.open("", "_blank", "width=850,height=650");
  if (!popup) return;
  popup.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>Biên bản bàn giao</title>
    <style>body{font-family:Arial,sans-serif;padding:36px;line-height:1.6}h2{text-align:center}.row{margin:10px 0}.sign{display:flex;justify-content:space-between;margin-top:70px;text-align:center}.sign>div{width:42%}</style>
    </head><body>
    <h2>BIÊN BẢN BÀN GIAO / THU HỒI TÀI SẢN</h2>
    <div class="row"><strong>Mã tài sản:</strong> ${escapeHtml(item.assetCode)}</div>
    <div class="row"><strong>Tên tài sản:</strong> ${escapeHtml(item.assetName)}</div>
    <div class="row"><strong>Phòng ban:</strong> ${escapeHtml(item.departmentName)}</div>
    <div class="row"><strong>Người nhận:</strong> ${escapeHtml(item.recipientName)}</div>
    <div class="row"><strong>Ngày bàn giao:</strong> ${escapeHtml(item.allocatedDate)}</div>
    <div class="row"><strong>Ghi chú bàn giao:</strong> ${escapeHtml(item.handoverNote || "-")}</div>
    <div class="row"><strong>Ngày thu hồi:</strong> ${escapeHtml(item.returnedDate || "-")}</div>
    <div class="row"><strong>Tình trạng nhận lại:</strong> ${escapeHtml(item.returnCondition || "-")}</div>
    <div class="row"><strong>Ghi chú thu hồi:</strong> ${escapeHtml(item.returnNote || "-")}</div>
    <div class="sign"><div><strong>Người bàn giao</strong><br><br><br>(Ký và ghi rõ họ tên)</div><div><strong>Người nhận</strong><br><br><br>(Ký và ghi rõ họ tên)</div></div>
    <script>window.onload=()=>window.print();<\/script></body></html>`);
  popup.document.close();
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

  const payload = {
    assetId,
    departmentId: Number(document.getElementById("allocationDepartmentId").value),
    recipientName: document.getElementById("recipientName").value.trim(),
    allocatedDate: document.getElementById("allocatedDate").value,
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
  if (session.role !== "Manager") document.getElementById("departmentId").value = "";
  loadAllocations(1);
});

loadDepartments().then(() => loadAllocations());
loadOverdue();
