const session = requireAuth("login.html");
renderNav({ active: "assets", basePath: "../" });

const canCreateOrEdit = can("assets.create") || can("assets.update");
const canDispose = session.role === "Admin IT"; // chỉ Admin IT được khôi phục tài sản đã thanh lý.
const canStartDisposal = can("disposals.inspect"); // Technician mở quy trình thanh lý (kiểm tra -> đề xuất).

let currentPage = 1;
let assetModal;
// Danh sách tài sản đang hiển thị — tra cứu lại theo id khi Sửa, thay vì nhúng thẳng JSON.stringify(a)
// vào thuộc tính onclick='...' (dữ liệu tự do như Tên/Serial/Thông số chứa dấu nháy đơn sẽ phá vỡ
// thuộc tính HTML và cho phép chèn script — XSS lưu trữ). Xem cùng cách sửa ở users.js/asset-categories.js/software-licenses.js.
let currentItems = [];

const STATUS_LABELS = {
  InUse: "Đang dùng",
  Maintenance: "Bảo trì",
  Broken: "Hỏng",
  Disposed: "Đã thanh lý",
};

const STATUS_BADGE_CLASSES = {
  InUse: "success",
  Maintenance: "warning",
  Broken: "danger",
  Disposed: "slate",
};

const STATUS_ICONS = {
  InUse: "bi-check-circle",
  Maintenance: "bi-tools",
  Broken: "bi-exclamation-triangle",
  Disposed: "bi-archive",
};

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

function showModalError(message) {
  const el = document.getElementById("modalError");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearModalError() {
  document.getElementById("modalError").classList.add("d-none");
}

async function loadFilterOptions() {
  try {
    const [departments, categories] = await Promise.all([
      api.get("/departments"),
      api.get("/asset-categories"),
    ]);
    fillSelect("departmentId", departments, "-- Phòng ban --");
    fillSelect("categoryId", categories, "-- Loại tài sản --");
    fillSelect("formDepartmentId", departments, null);
    fillSelect("formCategoryId", categories, null);

    // Manager chỉ được tạo/sửa tài sản trong đúng phòng ban của mình (UC-05 E3 / UC-06 E2) —
    // khoá sẵn lựa chọn để tránh submit rồi mới nhận 403 gây khó hiểu.
    if (session.role === "Manager") {
      document.getElementById("formDepartmentId").value = String(session.departmentId);
      document.getElementById("formDepartmentId").disabled = true;
    }
  } catch {
    // Chưa có quyền hoặc API chưa sẵn sàng — giữ nguyên option mặc định, không chặn trang chạy tiếp.
  }
}

function fillSelect(elementId, items, placeholder) {
  const select = document.getElementById(elementId);
  const previousValue = select.value;
  const placeholderHtml = placeholder !== null ? `<option value="">${placeholder}</option>` : "";
  select.innerHTML = placeholderHtml +
    items.map((item) => `<option value="${item.id}">${escapeHtml(item.name)}</option>`).join("");
  select.value = previousValue;
}

// Năm mua chỉ chọn từ danh sách (từ năm nay lùi về MIN_PURCHASE_YEAR) — không cho gõ tự do để khỏi nhập sai.
const MIN_PURCHASE_YEAR = 1990;
(function fillPurchaseYears() {
  const select = document.getElementById("purchaseYear");
  for (let y = new Date().getFullYear(); y >= MIN_PURCHASE_YEAR; y--) {
    select.insertAdjacentHTML("beforeend", `<option value="${y}">${y}</option>`);
  }
})();

const FILTER_IDS = ["keyword", "status", "warrantyStatus", "departmentId", "categoryId", "purchaseYear",
  "warrantyExpiringInDays", "allocationState", "purchasedFrom", "sort"];

function buildSearchParams(page) {
  return filterBar.params(FILTER_IDS, { page, pageSize: 10 });
}

let loadSeq = 0; // chỉ nhận kết quả của lần tải mới nhất — tránh kết quả cũ (về chậm) đè lên bộ lọc mới.

function renderLoading() {
  document.getElementById("assetTableBody").innerHTML =
    `<tr><td colspan="7" class="text-center text-muted py-4"><span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Đang tải...</td></tr>`;
}

async function loadAssets(page = 1) {
  const seq = ++loadSeq;
  currentPage = page;
  clearError();
  renderLoading();
  try {
    const result = await api.get(`/assets/search?${buildSearchParams(page)}`);
    if (seq !== loadSeq) return;
    // Đang ở trang vượt quá tổng số trang (vd xoá/lọc làm danh sách ngắn lại) -> quay về trang cuối thay vì hiện trống.
    if (result.items.length === 0 && page > 1 && result.totalPages > 0) {
      loadAssets(result.totalPages);
      return;
    }
    renderTable(result.items);
    renderPagination(result.page, result.totalPages);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} tài sản`;
  } catch (err) {
    if (seq !== loadSeq) return;
    showError(err.message);
    renderTable([], true);
    renderPagination(1, 0);
    document.getElementById("totalItemsLabel").textContent = "";
  }
}

function renderTable(items, loadFailed = false) {
  currentItems = items;
  const tbody = document.getElementById("assetTableBody");
  if (loadFailed) {
    tbody.innerHTML = `<tr><td colspan="7"><div class="empty-state"><i class="bi bi-exclamation-triangle"></i><div class="title">Không tải được danh sách tài sản</div><button class="btn btn-sm btn-outline-primary mt-2" onclick="loadAssets(currentPage)"><i class="bi bi-arrow-clockwise"></i> Thử lại</button></div></td></tr>`;
    return;
  }
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7"><div class="empty-state"><i class="bi bi-inbox"></i><div class="title">Không có tài sản phù hợp</div>Thử điều chỉnh bộ lọc hoặc thêm tài sản mới.</div></td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((a) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(a.assetCode)}</td>
      <td>${escapeHtml(a.name)}${a.serialNumber ? `<div class="text-muted small">Serial: ${escapeHtml(a.serialNumber)}</div>` : ""}</td>
      <td>${escapeHtml(a.categoryName)}</td>
      <td>${escapeHtml(a.departmentName)}</td>
      <td><span class="badge-soft ${STATUS_BADGE_CLASSES[a.status] || "slate"}"><i class="bi ${STATUS_ICONS[a.status] || "bi-question-circle"}"></i> ${STATUS_LABELS[a.status] || a.status}</span></td>
      <td>${a.warrantyExpiry ? (a.isUnderWarranty ? '<span class="badge-soft success"><i class="bi bi-shield-check"></i> Còn hạn</span>' : '<span class="badge-soft slate"><i class="bi bi-shield-x"></i> Hết hạn</span>') : '<span class="text-muted">-</span>'}</td>
      <td class="text-end">
        ${canCreateOrEdit ? `<button class="btn btn-sm btn-outline-primary me-1" onclick="openEditModal(${a.id})"><i class="bi bi-pencil"></i> Sửa</button>` : ""}
        ${canStartDisposal && a.status !== "Disposed"
          ? `<a class="btn btn-sm btn-outline-danger" href="disposals.html?newAssetId=${a.id}" title="Thanh lý phải qua quy trình: Technician kiểm tra và đề xuất, Manager duyệt, Admin IT thực hiện."><i class="bi bi-clipboard-check"></i> Kiểm tra thanh lý</a>`
          : ""}
      </td>
    </tr>`).join("");
}

function renderPagination(page, totalPages) {
  const el = document.getElementById("pagination");
  let html = "";
  for (let i = 1; i <= totalPages; i++) {
    html += `<li class="page-item ${i === page ? "active" : ""}">
      <a class="page-link" href="#" onclick="loadAssets(${i}); return false;">${i}</a></li>`;
  }
  el.innerHTML = html;
}

// Không ai được đặt "Đã thanh lý" trực tiếp (phải qua quy trình thanh lý); chỉ Admin IT được khôi phục tài sản
// đã thanh lý — Manager sửa được các trường khác nhưng không đổi được trạng thái này.
function applyStatusPermissions(currentStatus) {
  const select = document.getElementById("formStatus");
  const disposedOption = select.querySelector('option[value="Disposed"]');
  if (disposedOption) disposedOption.disabled = currentStatus !== "Disposed";
  select.disabled = !canDispose && currentStatus === "Disposed";
}

function openCreateModal() {
  document.getElementById("assetForm").reset();
  document.getElementById("assetId").value = "";
  document.getElementById("assetVersion").value = "";
  document.getElementById("assetModalTitle").textContent = "Thêm tài sản";
  document.getElementById("formStatusWrapper").style.display = "none";
  if (session.role === "Manager") {
    document.getElementById("formDepartmentId").value = String(session.departmentId);
  }
  clearModalError();
  document.querySelectorAll("#assetForm [data-date-vi]").forEach((i) => DateVi.validate(i));
  assetModal.show();
}

function openEditModal(id) {
  const asset = currentItems.find((x) => x.id === id);
  if (!asset) return;

  document.getElementById("assetForm").reset();
  document.getElementById("assetId").value = asset.id;
  document.getElementById("assetVersion").value = asset.version ?? "";
  document.getElementById("assetModalTitle").textContent = `Sửa tài sản ${asset.assetCode}`;
  document.getElementById("assetCode").value = asset.assetCode;
  document.getElementById("assetName").value = asset.name;
  document.getElementById("formCategoryId").value = asset.categoryId;
  document.getElementById("formDepartmentId").value = asset.departmentId;
  document.getElementById("serialNumber").value = asset.serialNumber || "";
  document.getElementById("specification").value = asset.specification || "";
  document.getElementById("operatingSystem").value = asset.operatingSystem || "";
  DateVi.set(document.getElementById("purchaseDate"), asset.purchaseDate);
  DateVi.set(document.getElementById("warrantyExpiry"), asset.warrantyExpiry);
  document.getElementById("formStatusWrapper").style.display = "block";
  document.getElementById("formStatus").value = asset.status;
  applyStatusPermissions(asset.status);
  clearModalError();
  assetModal.show();
}

document.getElementById("openCreateBtn").addEventListener("click", openCreateModal);
if (!canCreateOrEdit) {
  document.getElementById("openCreateBtn").style.display = "none";
}

// Kiểm tra phía trình duyệt trước khi gửi (server vẫn kiểm tra lại): trường bắt buộc, ngày hợp lệ, hạn bảo hành sau ngày mua.
function validateAssetForm(payload) {
  const errors = [];
  if (!payload.assetCode) errors.push("Mã tài sản không được để trống.");
  if (!payload.name) errors.push("Tên tài sản không được để trống.");
  if (!payload.categoryId) errors.push("Phải chọn loại tài sản.");
  if (!payload.departmentId) errors.push("Phải chọn phòng ban.");
  for (const [id, label] of [["purchaseDate", "Ngày mua"], ["warrantyExpiry", "Hạn bảo hành"]]) {
    if (!DateVi.validate(document.getElementById(id))) errors.push(`${label} không hợp lệ (nhập dạng dd/mm/yyyy).`);
  }
  if (payload.purchaseDate && payload.purchaseDate < `${MIN_PURCHASE_YEAR}-01-01`) errors.push(`Ngày mua không được trước năm ${MIN_PURCHASE_YEAR}.`);
  if (payload.purchaseDate && payload.purchaseDate > new Date(Date.now() + 86400000).toISOString().slice(0, 10)) errors.push("Ngày mua không được ở tương lai.");
  if (payload.purchaseDate && payload.warrantyExpiry && payload.warrantyExpiry < payload.purchaseDate) errors.push("Hạn bảo hành phải từ ngày mua trở về sau.");
  return errors;
}

const assetForm = document.getElementById("assetForm");
const assetGuard = guardUnsavedChanges(document.getElementById("assetModal"), assetForm);

assetForm.addEventListener("submit", (e) => {
  e.preventDefault();
  runLocked(assetForm, async () => {
    clearModalError();

    const id = document.getElementById("assetId").value;
    const payload = {
      assetCode: document.getElementById("assetCode").value.trim(),
      name: document.getElementById("assetName").value.trim(),
      categoryId: Number(document.getElementById("formCategoryId").value),
      departmentId: Number(document.getElementById("formDepartmentId").value),
      serialNumber: document.getElementById("serialNumber").value.trim() || null,
      specification: document.getElementById("specification").value.trim() || null,
      operatingSystem: document.getElementById("operatingSystem").value.trim() || null,
      purchaseDate: DateVi.value(document.getElementById("purchaseDate")) || null,
      warrantyExpiry: DateVi.value(document.getElementById("warrantyExpiry")) || null,
    };

    const errors = validateAssetForm(payload);
    if (errors.length) {
      showModalError(errors.join(" "));
      return;
    }

    try {
      if (id) {
        payload.status = document.getElementById("formStatus").value;
        const version = document.getElementById("assetVersion").value;
        if (version) payload.version = Number(version);
        await api.put(`/assets/${id}`, payload);
      } else {
        await api.post("/assets", payload);
      }
      assetGuard.markClean();
      assetModal.hide();
      loadAssets(currentPage);
    } catch (err) {
      showModalError(err.message);
      if (err.status === 409 && id) loadAssets(currentPage); // dữ liệu đã đổi: làm mới danh sách nền để mở lại thấy bản mới.
    }
  });
});

const filterBar = initFilterBar({
  fields: FILTER_IDS, uncounted: ["sort"], textFields: ["keyword"], onChange: () => loadAssets(1),
});

assetModal = new bootstrap.Modal(document.getElementById("assetModal"));
loadFilterOptions();
loadAssets();
