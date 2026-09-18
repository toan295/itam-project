const session = requireAuth("login.html");
renderNav({ active: "licenses", basePath: "../" });

let licenseModal;
let assignModal;
let assignMode = "assign"; // "assign" | "unassign"
let assignLicenseId = null;
let assetOptionsCache = null;
let currentItems = []; // tra cứu lại theo id khi Sửa, thay vì nhúng JSON.stringify(item) vào onclick='...' (rủi ro XSS)

if (session.role !== "Admin IT") {
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
  assignModal = new bootstrap.Modal(document.getElementById("assignModal"));

  document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);
  document.getElementById("searchForm").addEventListener("submit", (e) => {
    e.preventDefault();
    loadLicenses();
  });
  document.getElementById("licenseForm").addEventListener("submit", submitLicenseForm);
  document.getElementById("assignForm").addEventListener("submit", submitAssignForm);

  loadWarnings();
  loadLicenses();
}

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

function statusBadges(item) {
  const badges = [];
  if (item.isExpired) badges.push('<span class="badge-soft danger"><i class="bi bi-x-circle"></i> Đã hết hạn</span>');
  else if (item.isExpiringSoon) badges.push('<span class="badge-soft warning"><i class="bi bi-clock"></i> Sắp hết hạn</span>');
  if (item.isNearUsageLimit) badges.push('<span class="badge-soft info"><i class="bi bi-speedometer2"></i> Gần giới hạn</span>');
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
    el.textContent = `⚠️ Có ${items.length} license sẽ hết hạn trong 30 ngày tới: ${items.map((i) => i.softwareName).join(", ")}.`;
    el.classList.remove("d-none");
  } catch {
    // Không chặn trang nếu cảnh báo lỗi — chỉ bảng chính mới cần báo lỗi rõ.
  }
}

async function loadLicenses() {
  clearError();
  const search = document.getElementById("searchText").value.trim();
  const params = new URLSearchParams({ page: "1", pageSize: "100" });
  if (search) params.set("search", search);

  try {
    const result = await api.get(`/software-licenses?${params}`);
    renderTable(result.items);
  } catch (err) {
    showError(err.message);
    renderTable([]);
  }
}

function renderTable(items) {
  currentItems = items;
  const tbody = document.getElementById("licenseTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="6"><div class="empty-state"><i class="bi bi-key"></i><div class="title">Không có license nào</div></div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.softwareName)}</td>
      <td><code>${escapeHtml(item.licenseKey)}</code></td>
      <td>${escapeHtml(item.expiryDate)}</td>
      <td>${item.currentUsage}/${item.maxUsage} <span class="text-muted small">(${item.usagePercentage}%)</span></td>
      <td>${statusBadges(item)}</td>
      <td class="text-end">
        <button class="btn btn-sm btn-outline-primary me-1" onclick="openEditModal(${item.id})"><i class="bi bi-pencil"></i> Sửa</button>
        <button class="btn btn-sm btn-outline-success me-1" onclick="openAssignModal(${item.id}, 'assign')"><i class="bi bi-link-45deg"></i> Gán</button>
        <button class="btn btn-sm btn-outline-secondary me-1" onclick="openAssignModal(${item.id}, 'unassign')"><i class="bi bi-x-lg"></i> Gỡ</button>
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
  document.getElementById("expiryDate").value = item.expiryDate;
  document.getElementById("maxUsage").value = item.maxUsage;
  document.getElementById("notes").value = item.notes || "";
  document.getElementById("licenseModalError").classList.add("d-none");
  licenseModal.show();
}

async function submitLicenseForm(e) {
  e.preventDefault();
  const errEl = document.getElementById("licenseModalError");
  errEl.classList.add("d-none");

  const id = document.getElementById("licenseId").value;
  const payload = {
    softwareName: document.getElementById("softwareName").value.trim(),
    licenseKey: document.getElementById("licenseKey").value.trim(),
    expiryDate: document.getElementById("expiryDate").value,
    maxUsage: Number(document.getElementById("maxUsage").value),
    notes: document.getElementById("notes").value.trim() || null,
  };

  try {
    if (id) {
      await api.put(`/software-licenses/${id}`, payload);
    } else {
      await api.post("/software-licenses", payload);
    }
    licenseModal.hide();
    loadLicenses();
    loadWarnings();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove("d-none");
  }
}

async function deleteLicense(id) {
  // Xoá license sẽ xoá cascade toàn bộ lượt gán của nó (AssetSoftwareLicenses) ở tầng DB — cảnh báo
  // rõ số lượng tài sản đang dùng để Admin IT không xoá nhầm mất lịch sử gán mà không biết.
  const item = currentItems.find((x) => x.id === id);
  const warning = item && item.currentUsage > 0
    ? ` License này đang được gán cho ${item.currentUsage} tài sản — toàn bộ các lượt gán này sẽ bị gỡ theo.`
    : "";
  if (!confirm(`Xoá license này? Thao tác không thể hoàn tác.${warning}`)) return;
  try {
    await api.del(`/software-licenses/${id}`);
    loadLicenses();
    loadWarnings();
  } catch (err) {
    showError(err.message);
  }
}

async function loadAssetOptions() {
  if (assetOptionsCache) return assetOptionsCache;
  const result = await api.get("/assets?page=1&pageSize=100");
  assetOptionsCache = result.items;
  return assetOptionsCache;
}

async function openAssignModal(licenseId, mode) {
  assignLicenseId = licenseId;
  assignMode = mode;
  const errEl = document.getElementById("assignModalError");
  errEl.classList.add("d-none");

  document.getElementById("assignModalTitle").textContent =
    mode === "assign" ? "Gán license cho tài sản" : "Gỡ license khỏi tài sản";
  document.getElementById("assignSubmitBtn").textContent = mode === "assign" ? "Gán" : "Gỡ";
  document.getElementById("assignSubmitBtn").className = mode === "assign" ? "btn btn-primary" : "btn btn-danger";
  document.getElementById("assignHelpText").textContent = mode === "assign"
    ? "Chọn tài sản cần gán license này. Hệ thống sẽ từ chối nếu vượt số lượng cho phép (MaxUsage) hoặc đã gán trùng."
    : "Chọn tài sản cần gỡ license này ra. Hệ thống sẽ báo lỗi nếu tài sản chưa từng được gán license này.";

  try {
    const assets = await loadAssetOptions();
    const select = document.getElementById("assignAssetId");
    select.innerHTML = assets
      .map((a) => `<option value="${a.id}">${escapeHtml(a.assetCode)} - ${escapeHtml(a.name)} (${escapeHtml(a.departmentName)})</option>`)
      .join("");
    assignModal.show();
  } catch (err) {
    showError(err.message);
  }
}

async function submitAssignForm(e) {
  e.preventDefault();
  const errEl = document.getElementById("assignModalError");
  errEl.classList.add("d-none");
  const assetId = Number(document.getElementById("assignAssetId").value);

  try {
    if (assignMode === "assign") {
      await api.post(`/software-licenses/${assignLicenseId}/assign`, { assetId });
    } else {
      await api.del(`/software-licenses/${assignLicenseId}/assign/${assetId}`);
    }
    assignModal.hide();
    loadLicenses();
  } catch (err) {
    errEl.textContent = err.message;
    errEl.classList.remove("d-none");
  }
}
