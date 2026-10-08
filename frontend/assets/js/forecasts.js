const session = requireAuth("../pages/login.html");
renderNav({ active: "forecasts", basePath: "../" });

const pageSize = 20;
const MAX_YEARS_AHEAD = 10; // khớp D6 ở backend: từ năm hiện tại đến +10

const detailModal = bootstrap.Modal.getOrCreateInstance(document.getElementById("forecastDetailModal"));

// Kết quả lần "Tạo dự báo" gần nhất, giữ để mở chi tiết từng phòng ban mà không gọi lại API.
let lastGenerated = [];

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

// Định dạng hiển thị ở frontend (backend chỉ trả số decimal thô).
function formatVnd(value) {
  return `${Number(value).toLocaleString("vi-VN")} ₫`;
}

// Ô nhập tiền: chỉ giữ chữ số và tự chèn dấu chấm ngăn cách hàng nghìn khi đang gõ (25000000 -> 25.000.000).
function digitsOnly(text) {
  return String(text).replace(/\D/g, "");
}

function groupThousands(digits) {
  return digits.replace(/^0+(?=\d)/, "").replace(/\B(?=(\d{3})+(?!\d))/g, ".");
}

function parseVndInput(text) {
  const digits = digitsOnly(text);
  return digits ? Number(digits) : NaN;
}

function formatVndInput(input) {
  // Giữ vị trí con trỏ theo số chữ số đứng trước nó, nếu không con trỏ nhảy về cuối khi sửa giữa chuỗi.
  const caret = input.selectionStart ?? input.value.length;
  const digitsBeforeCaret = digitsOnly(input.value.slice(0, caret)).length;
  const formatted = groupThousands(digitsOnly(input.value));
  input.value = formatted;

  let position = 0;
  let seen = 0;
  while (position < formatted.length && seen < digitsBeforeCaret) {
    if (/\d/.test(formatted[position])) seen += 1;
    position += 1;
  }
  input.setSelectionRange(position, position);
}

const REASON_LABELS = {
  AgeExceeded: (a) => `Quá tuổi (ngưỡng ${a.maxAgeYears} năm)`,
  FailureCountExceeded: (a) => `Bảo trì nhiều (ngưỡng ${a.maxFailureCount} lần)`,
};

function formatDate(value) {
  if (!value) return "-";
  const date = new Date(`${value}T00:00:00`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString("vi-VN");
}

function formatDateTime(value) {
  if (!value) return "-";
  // API trả giờ UTC không kèm "Z" -> thêm "Z" để trình duyệt đổi đúng sang giờ địa phương.
  const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("vi-VN");
}

// ---------- Khởi tạo bộ lọc ----------

function fillYearOptions() {
  const current = new Date().getFullYear();
  const select = document.getElementById("genYear");
  select.innerHTML = Array.from({ length: MAX_YEARS_AHEAD + 1 }, (_, i) => current + i)
    .map((year) => `<option value="${year}">${year}</option>`).join("");
}

async function loadDepartments() {
  const departments = (await api.get("/departments")) || [];
  const options = departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");
  document.getElementById("genDepartment").insertAdjacentHTML("beforeend", options);
  document.getElementById("filterDepartment").insertAdjacentHTML("beforeend", options);
}

// ---------- Tạo dự báo ----------

function renderWarnings(warnings) {
  const banner = document.getElementById("warningBanner");
  if (!warnings.length) {
    banner.classList.add("d-none");
    return;
  }
  document.getElementById("warningList").innerHTML = warnings.map((w) => `<li>${escapeHtml(w)}</li>`).join("");
  banner.classList.remove("d-none");
}

function renderGenerated(result) {
  lastGenerated = result.forecasts || [];
  renderWarnings(result.warnings || []);

  const totalCount = lastGenerated.reduce((sum, f) => sum + f.estimatedReplacementCount, 0);
  const totalBudget = lastGenerated.reduce((sum, f) => sum + Number(f.estimatedBudget), 0);

  document.getElementById("resultTitle").textContent = `Kết quả dự báo năm ${result.year}`;
  document.getElementById("resultTotal").textContent = `Tổng: ${totalCount} thiết bị — ${formatVnd(totalBudget)}`;

  const body = document.getElementById("resultTableBody");
  body.innerHTML = lastGenerated.length
    ? lastGenerated.map((f, index) => `
      <tr>
        <td class="fw-semibold">${escapeHtml(f.departmentName)}
          ${f.estimatedReplacementCount === 0 ? `<div class="small text-muted fw-normal">Chưa có tài sản nào đến hạn thay thế năm ${f.year}</div>` : ""}
        </td>
        <td class="text-end">${f.estimatedReplacementCount}</td>
        <td class="text-end">${formatVnd(f.estimatedBudget)}</td>
        <td class="text-end">
          <button class="btn btn-sm btn-outline-primary" data-action="generated-detail" data-index="${index}">
            <i class="bi bi-eye"></i> Chi tiết
          </button>
        </td>
      </tr>`).join("")
    : '<tr><td colspan="4" class="text-center text-muted py-4">Không có phòng ban nào.</td></tr>';

  document.getElementById("resultSection").classList.remove("d-none");
}

document.getElementById("generateForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  clearError();

  const button = document.getElementById("generateBtn");
  const departmentId = document.getElementById("genDepartment").value;
  const payload = { year: Number(document.getElementById("genYear").value) };
  if (departmentId) payload.departmentId = Number(departmentId);

  button.disabled = true; // tránh bấm đúp
  try {
    renderGenerated(await api.post("/forecasts/generate", payload));
    await loadHistory(1);
  } catch (err) {
    showError(err.message);
  } finally {
    button.disabled = false;
  }
});

document.getElementById("resultTableBody").addEventListener("click", (event) => {
  const button = event.target.closest('[data-action="generated-detail"]');
  if (button) openDetail(lastGenerated[Number(button.dataset.index)]);
});

// ---------- Chi tiết (modal) ----------

function openDetail(forecast) {
  document.getElementById("detailTitle").textContent = `Dự báo năm ${forecast.year} — ${forecast.departmentName}`;
  document.getElementById("detailCount").textContent = forecast.estimatedReplacementCount;
  document.getElementById("detailBudget").textContent = formatVnd(forecast.estimatedBudget);
  document.getElementById("detailGeneratedAt").textContent = formatDateTime(forecast.generatedAt);

  const notes = document.getElementById("detailNotes");
  notes.textContent = forecast.notes || "";
  notes.classList.toggle("d-none", !forecast.notes);

  const breakdown = forecast.breakdown || [];
  const assets = breakdown.flatMap((b) => (b.assets || []).map((a) => ({ ...a, categoryName: b.categoryName })));

  document.getElementById("detailExplain").textContent = forecast.estimatedReplacementCount === 0
    ? `Phòng ban này chưa có tài sản nào đến hạn thay thế trong năm ${forecast.year} (chưa vượt ngưỡng tuổi hoặc ngưỡng số lần bảo trì, hoặc đã được tính ở năm khác).`
    : `${forecast.estimatedReplacementCount} tài sản của phòng ban này đến hạn thay thế trong năm ${forecast.year}. Danh sách bên dưới cho biết từng tài sản và lý do.`;

  document.getElementById("detailAssetsBody").innerHTML = assets.length
    ? assets.map((a) => `
        <tr>
          <td class="text-nowrap">${escapeHtml(a.assetCode)}</td>
          <td>${escapeHtml(a.assetName)}</td>
          <td>${escapeHtml(a.categoryName)}</td>
          <td class="text-nowrap">${formatDate(a.purchaseDate)}</td>
          <td class="text-end">${a.ageYears == null ? "-" : `${a.ageYears.toFixed(1)} năm`}</td>
          <td class="text-end">${a.ticketCount}</td>
          <td>${(a.reasons || []).length === 0 ? `<span class="badge text-bg-light border">Tròn ngưỡng tuổi trong năm ${forecast.year}</span>` : (a.reasons || []).map((r) => `<span class="badge text-bg-secondary me-1">${escapeHtml(REASON_LABELS[r] ? REASON_LABELS[r](a) : r)}</span>`).join("")}</td>
        </tr>`).join("")
    : `<tr><td colspan="7" class="text-center text-muted py-3">${
        forecast.estimatedReplacementCount > 0
          ? "Dự báo này được tạo trước khi có tính năng liệt kê tài sản — bấm “Tạo dự báo” lại để xem danh sách."
          : "Không có tài sản nào."}</td></tr>`;

  document.getElementById("detailBreakdownBody").innerHTML = breakdown.length
    ? breakdown.map((b) => b.unitPrice == null
      ? `<tr class="table-warning">
          <td>${escapeHtml(b.categoryName)}</td>
          <td class="text-end">${b.assetCount}</td>
          <td class="text-end" colspan="2"><span class="badge text-bg-warning">Chưa có đơn giá</span></td>
        </tr>`
      : `<tr>
          <td>${escapeHtml(b.categoryName)}</td>
          <td class="text-end">${b.assetCount}</td>
          <td class="text-end">${formatVnd(b.unitPrice)}</td>
          <td class="text-end">${formatVnd(b.subtotal)}</td>
        </tr>`).join("")
    : '<tr><td colspan="4" class="text-center text-muted py-3">Không có thiết bị nào đến hạn thay thế.</td></tr>';

  detailModal.show();
}

async function viewHistoryDetail(id) {
  clearError();
  try {
    openDetail(await api.get(`/forecasts/${id}`));
  } catch (err) {
    showError(err.message);
  }
}

// ---------- Lịch sử ----------

function buildHistoryQuery(page) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  const year = document.getElementById("filterYear").value;
  const departmentId = document.getElementById("filterDepartment").value;
  if (year) params.set("year", year);
  if (departmentId) params.set("departmentId", departmentId);
  return params.toString();
}

async function loadHistory(page = 1) {
  try {
    const result = await api.get(`/forecasts?${buildHistoryQuery(page)}`);
    renderHistory(result.items || []);
    renderPagination(result);
    document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} dự báo`;
  } catch (err) {
    showError(err.message);
  }
}

function renderHistory(items) {
  const body = document.getElementById("historyTableBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-4">Chưa có dự báo nào.</td></tr>';
    return;
  }
  body.innerHTML = items.map((f) => `
    <tr>
      <td>${f.year}</td>
      <td>${escapeHtml(f.departmentName)}</td>
      <td class="text-end">${f.estimatedReplacementCount}</td>
      <td class="text-end">
        ${formatVnd(f.estimatedBudget)}
        ${f.notes ? '<i class="bi bi-exclamation-triangle text-warning ms-1" title="Có loại chưa có đơn giá"></i>' : ""}
      </td>
      <td>${escapeHtml(formatDateTime(f.generatedAt))}</td>
      <td class="text-end">
        <button class="btn btn-sm btn-outline-primary" data-action="history-detail" data-id="${f.id}">
          <i class="bi bi-eye"></i> Xem chi tiết
        </button>
        <button class="btn btn-sm btn-outline-danger" data-action="history-delete" data-id="${f.id}">
          <i class="bi bi-trash"></i> Xoá
        </button>
      </td>
    </tr>`).join("");
}

function renderPagination(result) {
  const el = document.getElementById("pagination");
  el.innerHTML = "";
  for (let page = 1; page <= result.totalPages; page += 1) {
    const li = document.createElement("li");
    li.className = `page-item ${page === result.page ? "active" : ""}`;
    li.innerHTML = `<button class="page-link" type="button">${page}</button>`;
    li.querySelector("button").addEventListener("click", () => loadHistory(page));
    el.appendChild(li);
  }
}

document.getElementById("historyTableBody").addEventListener("click", async (event) => {
  const detailBtn = event.target.closest('[data-action="history-detail"]');
  if (detailBtn) {
    viewHistoryDetail(Number(detailBtn.dataset.id));
    return;
  }

  const deleteBtn = event.target.closest('[data-action="history-delete"]');
  if (!deleteBtn) return;
  if (!confirm("Xoá dự báo này khỏi danh sách? Dữ liệu vẫn được giữ trong hệ thống (xoá mềm) và dự báo có thể được tạo lại.")) return;

  clearError();
  deleteBtn.disabled = true;
  try {
    await api.del(`/forecasts/${deleteBtn.dataset.id}`);
    await loadHistory(1);
  } catch (err) {
    showError(err.message);
    deleteBtn.disabled = false;
  }
});

document.getElementById("historyFilterForm").addEventListener("submit", (event) => {
  event.preventDefault();
  loadHistory(1);
});

document.getElementById("resetFilterBtn").addEventListener("click", () => {
  document.getElementById("filterYear").value = "";
  document.getElementById("filterDepartment").value = "";
  loadHistory(1);
});

// ---------- Đơn giá tham khảo ----------

async function loadPrices() {
  try {
    renderPrices((await api.get("/forecasts/reference-prices")) || []);
  } catch (err) {
    showError(err.message);
  }
}

function renderPrices(items) {
  const body = document.getElementById("priceTableBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="4" class="text-center text-muted py-4">Chưa có loại tài sản nào.</td></tr>';
    return;
  }
  body.innerHTML = items.map((p) => {
    const configured = p.unitPrice != null;
    return `
      <tr data-category-id="${p.categoryId}">
        <td class="fw-semibold">${escapeHtml(p.categoryName)}</td>
        <td>
          <div class="input-group input-group-sm">
            <input type="text" inputmode="numeric" autocomplete="off" class="form-control price-input"
                   value="${configured ? groupThousands(String(Math.round(p.unitPrice))) : ""}" placeholder="VD: 25.000.000"
                   aria-label="Đơn giá ${escapeHtml(p.categoryName)}">
            <span class="input-group-text">₫</span>
          </div>
        </td>
        <td>${configured
          ? '<span class="badge text-bg-success">Đã cấu hình</span>'
          : '<span class="badge text-bg-warning">Chưa cấu hình</span>'}</td>
        <td class="text-end text-nowrap">
          <button class="btn btn-sm btn-primary" data-action="save-price"><i class="bi bi-check2"></i> Lưu</button>
          ${configured ? '<button class="btn btn-sm btn-outline-danger" data-action="delete-price"><i class="bi bi-trash"></i> Xoá</button>' : ""}
        </td>
      </tr>`;
  }).join("");
}

document.getElementById("priceTableBody").addEventListener("click", async (event) => {
  const button = event.target.closest("[data-action]");
  if (!button) return;

  const row = button.closest("tr");
  const categoryId = Number(row.dataset.categoryId);
  clearError();

  try {
    if (button.dataset.action === "save-price") {
      const value = parseVndInput(row.querySelector(".price-input").value);
      if (!Number.isFinite(value) || value <= 0) {
        showError("Đơn giá phải là số lớn hơn 0.");
        return;
      }
      if (value > 1_000_000_000_000) {
        showError("Đơn giá vượt quá giới hạn cho phép.");
        return;
      }
      await api.put(`/forecasts/reference-prices/${categoryId}`, { unitPrice: value });
    } else if (button.dataset.action === "delete-price") {
      if (!confirm("Xoá đơn giá tham khảo của loại này? Loại này sẽ không còn được tính vào ngân sách (dữ liệu vẫn được giữ trong hệ thống; nhập lại đơn giá để dùng lại).")) return;
      await api.del(`/forecasts/reference-prices/${categoryId}`);
    }
    await loadPrices();
  } catch (err) {
    showError(err.message);
  }
});

// Tự định dạng khi đang gõ/dán; Enter trong ô giá = Lưu.
document.getElementById("priceTableBody").addEventListener("input", (event) => {
  if (event.target.matches(".price-input")) formatVndInput(event.target);
});

document.getElementById("priceTableBody").addEventListener("keydown", (event) => {
  if (event.key === "Enter" && event.target.matches(".price-input")) {
    event.preventDefault();
    event.target.closest("tr").querySelector('[data-action="save-price"]').click();
  }
});

// ---------- Khởi động ----------

if (session?.role !== "Admin IT") {
  document.getElementById("forbiddenAlert").textContent = "Chỉ Admin IT được phép sử dụng Dự báo ngân sách.";
  document.getElementById("forbiddenAlert").classList.remove("d-none");
  document.getElementById("forecastContent").classList.add("d-none");
} else {
  fillYearOptions();
  loadDepartments()
    .catch((err) => showError(`Không tải được danh sách phòng ban: ${err.message}`))
    .then(() => Promise.all([loadHistory(1), loadPrices()]));
}
