const session = requireAuth("login.html");
renderNav({ active: "licenses", basePath: "../" });

let licenseModal;
let licensePage = 1;
let loadSeq = 0; // bỏ qua phản hồi cũ về chậm (khai báo trước lời gọi initPage() bên dưới)
let assignedAssetIds = new Set();
let assetSearchTimer;
let licenseGuard;
let assignModal;
let assignMode = "assign"; // "assign" | "unassign"
let assignLicenseId = null;
const LICENSE_FILTER_IDS = ["searchText", "stateFilter", "assignment", "expiryFrom", "expiryTo", "sortBy"];
let filterBar;
let currentItems = []; // tra cứu lại theo id khi Sửa, thay vì nhúng JSON.stringify(item) vào onclick='...' (rủi ro XSS)

if (!can("licenses.manage")) {
  document.getElementById("forbiddenAlert").textContent =
    "Module Phần mềm & Giấy phép chỉ dành cho Admin IT. Tài khoản của bạn không có quyền truy cập.";
  document.getElementById("forbiddenAlert").classList.remove("d-none");
  document.getElementById("licenseContent").classList.add("d-none");
  document.getElementById("openCreateBtn").style.display = "none";
} else {
  initPage();
}

function initPage() {
  licenseModal = new bootstrap.Modal(document.getElementById("licenseModal"));
  licenseGuard = guardUnsavedChanges(document.getElementById("licenseModal"), document.getElementById("licenseForm"));
  assignModal = new bootstrap.Modal(document.getElementById("assignModal"));

  document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);
  filterBar = initFilterBar({
    formId: "searchForm", fields: LICENSE_FILTER_IDS, uncounted: ["sortBy"], textFields: ["searchText"], onChange: () => loadLicenses(1),
  });
  // Nút trong khung cảnh báo → lọc sẵn các license sắp hết hạn.
  document.getElementById("filterExpiringBtn").addEventListener("click", () => {
    document.getElementById("searchForm").reset();
    document.getElementById("stateFilter").value = "ExpiringSoon";
    document.getElementById("sortBy").value = "Expiry";
    filterBar.refreshBadge();
    loadLicenses(1);
  });
  document.getElementById("licenseForm").addEventListener("submit", submitLicenseForm);
  document.getElementById("assignForm").addEventListener("submit", submitAssignForm);

  loadWarnings();
  loadLicenses(1);
}

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

// Hai nhóm trạng thái độc lập: hạn dùng (Đã hết hạn / Sắp hết hạn) và mức sử dụng (Hết chỗ / Gần giới hạn).
// Màu không phải cách duy nhất phân biệt: mỗi trạng thái đều có nhãn chữ và biểu tượng.
function statusBadges(item) {
  const badges = [];
  if (item.isExpired) badges.push('<span class="badge-soft danger"><i class="bi bi-x-circle"></i> Đã hết hạn</span>');
  else if (item.isExpiringSoon) badges.push('<span class="badge-soft warning"><i class="bi bi-clock"></i> Sắp hết hạn</span>');
  if (item.isFull) badges.push('<span class="badge-soft danger"><i class="bi bi-slash-circle"></i> Hết chỗ</span>');
  else if (item.isNearUsageLimit) badges.push('<span class="badge-soft info"><i class="bi bi-speedometer2"></i> Gần giới hạn</span>');
  if (badges.length === 0) badges.push('<span class="badge-soft success"><i class="bi bi-check-circle"></i> Bình thường</span>');
  return badges.join(" ");
}

async function loadWarnings() {
  try {
    const items = await api.get("/software-licenses/expiring-soon?days=30");
    const el = document.getElementById("warningBanner");
    if (items.length === 0) {
      el.classList.add("d-none");
      return;
    }
    // Cảnh báo tự động: hệ thống so ngày hết hạn của từng license với hôm nay (UC-10), không do ai nhập tay.
    const renderItem = (item) => `<div class="d-flex flex-column flex-md-row justify-content-between gap-1 border-top py-2">
      <span class="text-truncate"><strong>${escapeHtml(item.softwareName)}</strong> · <code>${escapeHtml(item.licenseKey)}</code></span>
      <span class="badge text-bg-warning align-self-start flex-shrink-0">Hết hạn ${escapeHtml(DateVi.toDisplay(item.expiryDate))}</span>
    </div>`;
    document.getElementById("warningCount").textContent = `${items.length} license trong 30 ngày tới`;
    document.getElementById("warningPreview").innerHTML = items.map(renderItem).join(""); // nhiều thông báo -> cuộn trong khung
    el.classList.remove("d-none");
  } catch {
    // Không chặn trang nếu cảnh báo lỗi — chỉ bảng chính mới cần báo lỗi rõ.
  }
}

async function loadLicenses(page = 1) {
  const seq = ++loadSeq;
  licensePage = typeof page === "number" ? page : 1;
  clearError();
  const params = filterBar.params(
    { searchText: "search", stateFilter: "state", assignment: "assignment", expiryFrom: "expiryFrom", expiryTo: "expiryTo", sortBy: "sortBy" },
    { page: String(licensePage), pageSize: "20" });

  try {
    const result = await api.get(`/software-licenses?${params}`);
    if (seq !== loadSeq) return;
    if (result.items.length === 0 && licensePage > 1 && result.totalPages > 0) {
      loadLicenses(result.totalPages);
      return;
    }
    renderTable(result.items);
    renderPagination(result.page, result.totalPages);
    document.getElementById("resultLabel").textContent = `Tổng cộng: ${result.totalItems} license`;
  } catch (err) {
    if (seq !== loadSeq) return;
    showError(err.message);
    renderTable([], true);
    renderPagination(1, 0);
    document.getElementById("resultLabel").textContent = "";
  }
}

function renderPagination(page, totalPages) {
  let html = "";
  for (let i = 1; i <= totalPages; i++) {
    html += `<li class="page-item ${i === page ? "active" : ""}"><a class="page-link" href="#" onclick="loadLicenses(${i}); return false;">${i}</a></li>`;
  }
  document.getElementById("licensePagination").innerHTML = html;
}

function renderTable(items, loadFailed = false) {
  currentItems = items;
  const tbody = document.getElementById("licenseTableBody");
  if (loadFailed) {
    tbody.innerHTML = `<tr><td colspan="6"><div class="empty-state"><i class="bi bi-exclamation-triangle"></i><div class="title">Không tải được danh sách license</div><button class="btn btn-sm btn-outline-primary mt-2" onclick="loadLicenses(licensePage)"><i class="bi bi-arrow-clockwise"></i> Thử lại</button></div></td></tr>`;
    return;
  }
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6"><div class="empty-state"><i class="bi bi-key"></i><div class="title">Không có license phù hợp</div><button class="btn btn-sm btn-outline-secondary mt-2" onclick="document.getElementById('resetFilterBtn').click()"><i class="bi bi-x-circle"></i> Xoá bộ lọc</button></div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.softwareName)}</td>
      <td><code>${escapeHtml(item.licenseKey)}</code></td>
      <td class="text-nowrap">${escapeHtml(DateVi.toDisplay(item.expiryDate))}</td>
      <td>${item.currentUsage}/${item.maxUsage} <span class="text-muted small">(còn ${item.remainingSlots} chỗ)</span></td>
      <td>${statusBadges(item)}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-outline-primary me-1" onclick="openEditModal(${item.id})"><i class="bi bi-pencil"></i> Sửa</button>
        <button class="btn btn-sm btn-outline-success me-1" onclick="openAssignModal(${item.id}, 'assign')" ${item.isFull || item.isExpired ? `disabled title="${item.isExpired ? "License đã hết hạn" : "License đã hết chỗ"}"` : ""}><i class="bi bi-link-45deg"></i> Gán</button>
        <button class="btn btn-sm btn-outline-secondary me-1" onclick="openAssignModal(${item.id}, 'unassign')" ${item.currentUsage === 0 ? 'disabled title="Chưa có tài sản nào được gán"' : ""}><i class="bi bi-x-lg"></i> Gỡ</button>
        <button class="btn btn-sm btn-outline-danger" onclick="deleteLicense(${item.id})"><i class="bi bi-trash"></i> Xoá</button>
      </td>
    </tr>`).join("");
}

function openCreateModal() {
  document.getElementById("licenseForm").reset();
  document.getElementById("licenseId").value = "";
  document.getElementById("licenseModalTitle").textContent = "Thêm license";
  document.getElementById("licenseModalError").classList.add("d-none");
  licenseModal.show();
}

function openEditModal(id) {
  const item = currentItems.find((x) => x.id === id);
  if (!item) return;

  document.getElementById("licenseForm").reset();
  document.getElementById("licenseId").value = item.id;
  document.getElementById("licenseModalTitle").textContent = `Sửa license: ${item.softwareName}`;
  document.getElementById("softwareName").value = item.softwareName;
  document.getElementById("licenseKey").value = item.licenseKey;
  DateVi.set(document.getElementById("expiryDate"), item.expiryDate);
  document.getElementById("maxUsage").value = item.maxUsage;
  document.getElementById("notes").value = item.notes || "";
  document.getElementById("licenseModalError").classList.add("d-none");
  licenseModal.show();
}

function submitLicenseForm(e) {
  e.preventDefault();
  return runLocked(e.currentTarget, () => saveLicense());
}

async function saveLicense() {
  const errEl = document.getElementById("licenseModalError");
  errEl.classList.add("d-none");

  const expiryDate = DateVi.value(document.getElementById("expiryDate"));
  if (!expiryDate) {
    errEl.textContent = "Vui lòng nhập ngày hết hạn hợp lệ (dd/mm/yyyy).";
    errEl.classList.remove("d-none");
    return;
  }

  const id = document.getElementById("licenseId").value;
  const payload = {
    softwareName: document.getElementById("softwareName").value.trim(),
    licenseKey: document.getElementById("licenseKey").value.trim(),
    expiryDate,
    maxUsage: Number(document.getElementById("maxUsage").value),
    notes: document.getElementById("notes").value.trim() || null,
  };

  try {
    if (id) {
      await api.put(`/software-licenses/${id}`, payload);
    } else {
      await api.post("/software-licenses", payload);
    }
    licenseGuard.markClean();
    licenseModal.hide();
    loadLicenses(licensePage);
    loadWarnings();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove("d-none");
  }
}

async function deleteLicense(id) {
  // Backend chặn xoá license còn đang được gán (FK Restrict, trả 409) — báo trước cho Admin IT biết phải gỡ
  // hết lượt gán trước thay vì để họ xác nhận rồi mới nhận lỗi.
  const item = currentItems.find((x) => x.id === id);
  if (item && item.currentUsage > 0) {
    showError(`Không thể xoá: license đang được gán cho ${item.currentUsage} tài sản. Hãy gỡ hết các lượt gán trước.`);
    return;
  }
  if (!confirm("Xoá license này? Thao tác không thể hoàn tác.")) return;
  try {
    await api.del(`/software-licenses/${id}`);
    loadLicenses(licensePage);
    loadWarnings();
  } catch (err) {
    showError(err.message);
  }
}

// ===== Gán / gỡ license =====


const ASSET_STATUS_LABELS = { InUse: "Đang dùng", Maintenance: "Bảo trì", Broken: "Hỏng", Disposed: "Đã thanh lý" };

function licenseSummaryHtml(l) {
  const state = l.isExpired ? '<span class="badge-soft danger">Đã hết hạn</span>'
    : l.isFull ? '<span class="badge-soft danger">Hết chỗ</span>'
    : l.isNearUsageLimit ? '<span class="badge-soft info">Gần giới hạn</span>'
    : l.isExpiringSoon ? '<span class="badge-soft warning">Sắp hết hạn</span>'
    : '<span class="badge-soft success">Bình thường</span>';
  return `<div class="fw-semibold fs-6">${escapeHtml(l.softwareName)}</div>
    <div>Key: <code>${escapeHtml(l.licenseKey)}</code></div>
    <div>Đang dùng: <strong>${l.currentUsage}/${l.maxUsage}</strong> · còn <strong>${l.remainingSlots}</strong> chỗ · hết hạn <strong>${escapeHtml(DateVi.toDisplay(l.expiryDate))}</strong> ${state}</div>`;
}

// Số liệu license trong hộp thoại luôn lấy mới từ máy chủ (người khác có thể vừa gán/gỡ).
async function refreshAssignInfo() {
  const license = await api.get(`/software-licenses/${assignLicenseId}`);
  document.getElementById("assignLicenseInfo").innerHTML = licenseSummaryHtml(license);
  return license;
}

async function loadAssignable(keyword = "") {
  const select = document.getElementById("assignAssetId");
  const params = new URLSearchParams({ page: "1", pageSize: "100" });
  if (keyword) params.set("keyword", keyword);
  const result = await api.get(`/assets/search?${params}`);
  const options = result.items.filter((a) => a.status !== "Disposed" && !assignedAssetIds.has(a.id));
  select.innerHTML = options.map((a) => `<option value="${a.id}">${escapeHtml(a.assetCode)} — ${escapeHtml(a.name)} · ${escapeHtml(a.departmentName)} · ${ASSET_STATUS_LABELS[a.status] || escapeHtml(a.status)}</option>`).join("")
    || '<option value="" disabled>Không có tài sản phù hợp</option>';
  if (options.length) select.selectedIndex = 0;
  document.getElementById("assignAssetHint").textContent = result.totalItems > result.items.length
    ? `Hiện ${options.length} tài sản đầu tiên — gõ mã hoặc tên để thu hẹp.`
    : "Chỉ hiện tài sản chưa thanh lý và chưa được gán license này.";
}

async function loadAssignedOptions() {
  const assigned = await api.get(`/software-licenses/${assignLicenseId}/assignments`);
  assignedAssetIds = new Set(assigned.map((a) => a.assetId));
  document.getElementById("unassignAssetId").innerHTML = assigned.map((a) => `<option value="${a.assetId}">${escapeHtml(a.assetCode)} — ${escapeHtml(a.assetName)} · ${escapeHtml(a.departmentName)} · gán ${escapeHtml(DateVi.toDisplay(a.assignedDate))}</option>`).join("")
    || '<option value="" disabled>Chưa có tài sản nào được gán</option>';
  if (assigned.length) document.getElementById("unassignAssetId").selectedIndex = 0;
  return assigned;
}

async function openAssignModal(licenseId, mode) {
  assignLicenseId = licenseId;
  assignMode = mode;
  const errEl = document.getElementById("assignModalError");
  errEl.classList.add("d-none");
  document.getElementById("assetSearch").value = "";
  document.getElementById("unassignReason").value = "";

  const assign = mode === "assign";
  document.getElementById("assignModalTitle").textContent = assign ? "Gán license cho tài sản" : "Gỡ license khỏi tài sản";
  document.getElementById("assignSubmitBtn").className = assign ? "btn btn-primary" : "btn btn-danger";
  document.getElementById("assignSubmitBtn").textContent = assign ? "Gán" : "Gỡ license";
  document.getElementById("assignModeAssign").classList.toggle("d-none", !assign);
  document.getElementById("assignModeUnassign").classList.toggle("d-none", assign);

  try {
    const license = await refreshAssignInfo();
    const assigned = await loadAssignedOptions();
    if (assign) {
      await loadAssignable();
      if (license.isExpired || license.isFull) {
        showAssignError(license.isExpired
          ? "License đã hết hạn nên không thể gán thêm. Hãy gia hạn (sửa ngày hết hạn) trước."
          : `License đã hết chỗ (${license.currentUsage}/${license.maxUsage}). Hãy gỡ bớt hoặc tăng MaxUsage.`);
      }
      document.getElementById("assignSubmitBtn").disabled = license.isExpired || license.isFull;
    } else {
      document.getElementById("assignSubmitBtn").disabled = assigned.length === 0;
    }
    assignModal.show();
  } catch (err) {
    showError(err.message);
  }
}

function showAssignError(message) {
  const el = document.getElementById("assignModalError");
  el.textContent = message;
  el.classList.remove("d-none");
}

document.getElementById("assetSearch").addEventListener("input", (e) => {
  clearTimeout(assetSearchTimer);
  assetSearchTimer = setTimeout(() => loadAssignable(e.target.value.trim()).catch((err) => showAssignError(err.message)), 300);
});

function submitAssignForm(e) {
  e.preventDefault();
  return runLocked(e.currentTarget, () => assignOrUnassign());
}

async function assignOrUnassign() {
  document.getElementById("assignModalError").classList.add("d-none");
  const assign = assignMode === "assign";
  const assetId = Number(document.getElementById(assign ? "assignAssetId" : "unassignAssetId").value);
  if (!assetId) {
    showAssignError(assign ? "Vui lòng chọn một tài sản." : "Vui lòng chọn tài sản cần gỡ license.");
    return;
  }

  if (!assign) {
    const label = document.getElementById("unassignAssetId").selectedOptions[0].textContent;
    if (!confirm(`Gỡ license khỏi tài sản:\n${label}\n\nTiếp tục?`)) return;
  }

  try {
    if (assign) {
      await api.post(`/software-licenses/${assignLicenseId}/assign`, { assetId });
    } else {
      const reason = document.getElementById("unassignReason").value.trim();
      await api.del(`/software-licenses/${assignLicenseId}/assign/${assetId}${reason ? `?reason=${encodeURIComponent(reason)}` : ""}`);
    }
    assignModal.hide();
    loadLicenses(licensePage);
  } catch (err) {
    // Luôn tải lại số liệu thật từ máy chủ: lỗi nghiệp vụ (hết chỗ, hết hạn, trùng, đã bị gỡ trước đó) thường do dữ liệu đã đổi.
    if (!err.status) {
      showAssignError("Không nhận được phản hồi từ máy chủ — thao tác có thể đã được thực hiện. Số liệu bên dưới đã được tải lại; hãy kiểm tra rồi thử lại nếu cần.");
    } else if (err.status === 404 && !assign) {
      showAssignError("Tài sản này không còn được gán license (có thể đã được gỡ trước đó). Danh sách đã được làm mới.");
    } else {
      showAssignError(err.message);
    }
    try {
      const license = await refreshAssignInfo();
      await loadAssignedOptions();
      if (assign) {
        await loadAssignable(document.getElementById("assetSearch").value.trim());
        document.getElementById("assignSubmitBtn").disabled = license.isExpired || license.isFull;
      }
    } catch {
      // Không làm mất thông báo lỗi chính nếu việc tải lại cũng lỗi.
    }
    loadLicenses(licensePage);
  }
}
