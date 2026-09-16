let currentPage = 1;

const STATUS_LABELS = {
  InUse: "Đang dùng",
  Maintenance: "Bảo trì",
  Broken: "Hỏng",
  Disposed: "Đã thanh lý",
};

const STATUS_BADGE_CLASSES = {
  InUse: "bg-success",
  Maintenance: "bg-warning text-dark",
  Broken: "bg-danger",
  Disposed: "bg-secondary",
};

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

async function loadFilterOptions() {
  try {
    const [departments, categories] = await Promise.all([
      api.get("/departments"),
      api.get("/asset-categories"),
    ]);
    fillSelect("departmentId", departments, "-- Phòng ban --");
    fillSelect("categoryId", categories, "-- Loại tài sản --");
  } catch {
    // Chưa có token hợp lệ hoặc API chưa sẵn sàng — giữ nguyên option mặc định, không chặn trang chạy tiếp.
  }
}

function fillSelect(elementId, items, placeholder) {
  const select = document.getElementById(elementId);
  const previousValue = select.value;
  select.innerHTML = `<option value="">${placeholder}</option>` +
    items.map((item) => `<option value="${item.id}">${escapeHtml(item.name)}</option>`).join("");
  select.value = previousValue;
}

function buildSearchParams(page) {
  const params = new URLSearchParams({ page, pageSize: 10 });
  const fields = ["keyword", "status", "warrantyStatus", "departmentId", "categoryId", "purchaseYear"];
  for (const field of fields) {
    const value = document.getElementById(field).value.trim();
    if (value) {
      params.set(field, value);
    }
  }
  return params;
}

async function loadAssets(page = 1) {
  currentPage = page;
  clearError();
  try {
    const result = await api.get(`/assets/search?${buildSearchParams(page)}`);
    renderTable(result.items);
    renderPagination(result.page, result.totalPages);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} tài sản`;
  } catch (err) {
    showError(err.message);
    renderTable([]);
    renderPagination(1, 0);
    document.getElementById("totalItemsLabel").textContent = "";
  }
}

function renderTable(items) {
  const tbody = document.getElementById("assetTableBody");
  if (items.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7" class="text-center text-muted">Không có tài sản phù hợp.</td></tr>`;
    return;
  }

  tbody.innerHTML = items.map((a) => `
    <tr>
      <td>${escapeHtml(a.assetCode)}</td>
      <td>${escapeHtml(a.name)}</td>
      <td>${escapeHtml(a.categoryName)}</td>
      <td>${escapeHtml(a.departmentName)}</td>
      <td><span class="badge ${STATUS_BADGE_CLASSES[a.status] || "bg-secondary"}">${STATUS_LABELS[a.status] || a.status}</span></td>
      <td>${a.warrantyExpiry ? (a.isUnderWarranty ? '<span class="badge bg-success">Còn hạn</span>' : '<span class="badge bg-secondary">Hết hạn</span>') : '<span class="text-muted">-</span>'}</td>
      <td>
        ${a.status === "Disposed"
          ? '<span class="text-muted small">Đã thanh lý</span>'
          : `<button class="btn btn-sm btn-danger" onclick="disposeAsset(${a.id})">Thanh lý</button>`}
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

async function disposeAsset(id) {
  if (!confirm("Chuyển tài sản này sang trạng thái Đã thanh lý?")) return;
  try {
    await api.del(`/assets/${id}`);
    loadAssets(currentPage);
  } catch (err) {
    showError(err.message);
  }
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

document.getElementById("filterForm").addEventListener("submit", (e) => {
  e.preventDefault();
  loadAssets(1);
});

document.getElementById("resetFilterBtn").addEventListener("click", () => {
  document.getElementById("filterForm").reset();
  loadAssets(1);
});

document.getElementById("tokenInput").value = getToken() || "";
document.getElementById("saveTokenBtn").addEventListener("click", () => {
  setToken(document.getElementById("tokenInput").value.trim());
  loadFilterOptions();
  loadAssets(1);
});

loadFilterOptions();
loadAssets();
