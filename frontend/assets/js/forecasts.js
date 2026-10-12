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

let successTimer = null;
function showSuccess(message) {
  const el = document.getElementById("successAlert");
  el.textContent = message;
  el.classList.remove("d-none");
  clearTimeout(successTimer);
  successTimer = setTimeout(() => el.classList.add("d-none"), 4000);
}

// Định dạng hiển thị ở frontend (backend chỉ trả số decimal thô).
function formatVnd(value) {
  return `${Number(value).toLocaleString("vi-VN")} ₫`;
}

// Định mức hiển thị kiểu Việt Nam, tối đa 2 chữ số thập phân (0,1 / 1 / 2,5).
function formatRatio(value) {
  return Number(value).toLocaleString("vi-VN", { maximumFractionDigits: 2 });
}

// Nhận cả "0,5" lẫn "0.5"; trả NaN nếu không hợp lệ hoặc quá 2 chữ số thập phân (khớp validator backend).
function parseRatioInput(text) {
  const normalized = String(text).trim().replace(",", ".");
  return /^\d+(\.\d{1,2})?$/.test(normalized) ? Number(normalized) : NaN;
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
  const options = Array.from({ length: MAX_YEARS_AHEAD + 1 }, (_, i) => current + i)
    .map((year) => `<option value="${year}">${year}</option>`).join("");
  document.getElementById("genYear").innerHTML = options;
  document.getElementById("planYear").innerHTML = options;
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

  // Tổng = thay thế + mua thêm (backend đã tính sẵn từng dòng).
  const totalCount = lastGenerated.reduce((sum, f) => sum + f.totalPurchaseCount, 0);
  const totalBudget = lastGenerated.reduce((sum, f) => sum + Number(f.totalBudget), 0);

  document.getElementById("resultTitle").textContent = `Kết quả dự báo năm ${result.year}`;
  document.getElementById("resultTotal").textContent = `Tổng: ${totalCount} thiết bị — ${formatVnd(totalBudget)}`;

  const body = document.getElementById("resultTableBody");
  body.innerHTML = lastGenerated.length
    ? lastGenerated.map((f, index) => `
      <tr>
        <td class="fw-semibold">${escapeHtml(f.departmentName)}
          ${f.totalPurchaseCount === 0 ? `<div class="small text-muted fw-normal">Chưa có thiết bị nào cần thay hoặc mua thêm năm ${f.year}</div>` : ""}
        </td>
        <td class="text-end">${f.estimatedReplacementCount} thiết bị<div class="small text-muted">${formatVnd(f.estimatedBudget)}</div></td>
        <td class="text-end">${f.estimatedExpansionCount} thiết bị<div class="small text-muted">${formatVnd(f.estimatedExpansionBudget)}</div>
          <div class="small text-muted">Nhân sự tăng thêm: ${f.additionalHeadcount}</div></td>
        <td class="text-end fw-semibold">${f.totalPurchaseCount} thiết bị<div class="small fw-normal">${formatVnd(f.totalBudget)}</div></td>
        <td class="text-end">
          <button class="btn btn-sm btn-outline-primary" data-action="generated-detail" data-index="${index}">
            <i class="bi bi-eye"></i> Chi tiết
          </button>
        </td>
      </tr>`).join("")
    : '<tr><td colspan="5" class="text-center text-muted py-4">Không có phòng ban nào.</td></tr>';

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
  document.getElementById("detailCount").textContent =
    `${forecast.estimatedReplacementCount} thiết bị — ${formatVnd(forecast.estimatedBudget)}`;
  document.getElementById("detailExpansion").textContent =
    `${forecast.estimatedExpansionCount} thiết bị — ${formatVnd(forecast.estimatedExpansionBudget)}`;
  document.getElementById("detailTotal").textContent =
    `${forecast.totalPurchaseCount} thiết bị — ${formatVnd(forecast.totalBudget)}`;
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

  renderExpansionDetail(forecast);
  renderPurchaseSummary(forecast);

  detailModal.show();
}

function renderExpansionDetail(forecast) {
  const items = forecast.expansionBreakdown || [];
  document.getElementById("detailExpansionExplain").textContent = items.length
    ? `Nhân sự tăng thêm trong năm ${forecast.year}: ${forecast.additionalHeadcount} người. Số lượng = nhân sự × định mức, làm tròn lên.`
    : "Không có nhu cầu mua thêm: phòng ban chưa có kế hoạch mở rộng cho năm này, chưa cấu hình định mức, hoặc dự báo được tạo trước khi có tính năng mua thêm (bấm “Tạo dự báo” lại để cập nhật).";

  document.getElementById("detailExpansionBody").innerHTML = items.length
    ? items.map((i) => i.unitPrice == null
      ? `<tr class="table-warning">
          <td>${escapeHtml(i.categoryName)}</td>
          <td class="text-end">${formatRatio(i.devicesPerPerson)}</td>
          <td class="text-end">${i.quantity}</td>
          <td class="text-end" colspan="2"><span class="badge text-bg-warning">Chưa có đơn giá</span></td>
        </tr>`
      : `<tr>
          <td>${escapeHtml(i.categoryName)}</td>
          <td class="text-end">${formatRatio(i.devicesPerPerson)}</td>
          <td class="text-end">${i.quantity}</td>
          <td class="text-end">${formatVnd(i.unitPrice)}</td>
          <td class="text-end">${formatVnd(i.subtotal)}</td>
        </tr>`).join("")
    : '<tr><td colspan="5" class="text-center text-muted py-3">Không có thiết bị nào cần mua thêm.</td></tr>';
}

function renderPurchaseSummary(forecast) {
  const items = forecast.purchaseSummary || [];
  document.getElementById("detailPurchaseBody").innerHTML = items.length
    ? items.map((i) => `
        <tr class="${i.unitPrice == null ? "table-warning" : ""}">
          <td>${escapeHtml(i.categoryName)}</td>
          <td class="text-end">${i.replacementQuantity}</td>
          <td class="text-end">${i.expansionQuantity}</td>
          <td class="text-end fw-semibold">${i.totalQuantity}</td>
          ${i.unitPrice == null
            ? '<td class="text-end" colspan="2"><span class="badge text-bg-warning">Chưa có đơn giá</span></td>'
            : `<td class="text-end">${formatVnd(i.unitPrice)}</td><td class="text-end">${formatVnd(i.totalCost)}</td>`}
        </tr>`).join("")
    : '<tr><td colspan="6" class="text-center text-muted py-3">Không có thiết bị nào cần mua.</td></tr>';
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
    body.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-4">Chưa có dự báo nào.</td></tr>';
    return;
  }
  body.innerHTML = items.map((f) => `
    <tr>
      <td>${f.year}</td>
      <td>${escapeHtml(f.departmentName)}</td>
      <td class="text-end">${f.estimatedReplacementCount}</td>
      <td class="text-end">${f.estimatedExpansionCount}</td>
      <td class="text-end fw-semibold">${f.totalPurchaseCount}</td>
      <td class="text-end">
        ${formatVnd(f.totalBudget)}
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

// ---------- Định mức thiết bị/người ----------

async function loadRatios() {
  try {
    renderRatios((await api.get("/forecasts/device-ratios")) || []);
  } catch (err) {
    showError(err.message);
  }
}

function renderRatios(items) {
  const body = document.getElementById("ratioTableBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="4" class="text-center text-muted py-4">Chưa có loại tài sản nào.</td></tr>';
    return;
  }
  body.innerHTML = items.map((r) => {
    const configured = r.devicesPerPerson != null;
    return `
      <tr data-category-id="${r.categoryId}">
        <td class="fw-semibold">${escapeHtml(r.categoryName)}</td>
        <td>
          <input type="text" inputmode="decimal" autocomplete="off" class="form-control form-control-sm ratio-input"
                 value="${configured ? formatRatio(r.devicesPerPerson) : ""}" placeholder="VD: 1 hoặc 0,1"
                 aria-label="Định mức ${escapeHtml(r.categoryName)}">
        </td>
        <td>${configured
          ? '<span class="badge text-bg-success">Đã cấu hình</span>'
          : '<span class="badge text-bg-secondary">Chưa cấu hình — không tính mua thêm</span>'}</td>
        <td class="text-end text-nowrap">
          <button class="btn btn-sm btn-primary" data-action="save-ratio"><i class="bi bi-check2"></i> Lưu</button>
          ${configured ? '<button class="btn btn-sm btn-outline-danger" data-action="delete-ratio"><i class="bi bi-trash"></i> Xoá</button>' : ""}
        </td>
      </tr>`;
  }).join("");
}

document.getElementById("ratioTableBody").addEventListener("click", async (event) => {
  const button = event.target.closest("[data-action]");
  if (!button) return;

  const row = button.closest("tr");
  const categoryId = Number(row.dataset.categoryId);
  clearError();

  try {
    if (button.dataset.action === "save-ratio") {
      const value = parseRatioInput(row.querySelector(".ratio-input").value);
      if (!Number.isFinite(value) || value < 0.01 || value > 20) {
        showError("Định mức phải là số từ 0,01 đến 20, tối đa 2 chữ số thập phân.");
        return;
      }
      await api.put(`/forecasts/device-ratios/${categoryId}`, { devicesPerPerson: value });
      showSuccess("Đã lưu định mức. Bấm “Tạo dự báo” lại để áp dụng.");
    } else if (button.dataset.action === "delete-ratio") {
      if (!confirm("Xoá định mức của loại này? Loại này sẽ không còn được tính vào nhu cầu mua thêm.")) return;
      await api.del(`/forecasts/device-ratios/${categoryId}`);
      showSuccess("Đã xoá định mức. Bấm “Tạo dự báo” lại để áp dụng.");
    }
    await loadRatios();
  } catch (err) {
    showError(err.message);
  }
});

document.getElementById("ratioTableBody").addEventListener("keydown", (event) => {
  if (event.key === "Enter" && event.target.matches(".ratio-input")) {
    event.preventDefault();
    event.target.closest("tr").querySelector('[data-action="save-ratio"]').click();
  }
});

// ---------- Kế hoạch mở rộng nhân sự ----------

async function loadPlans() {
  const year = document.getElementById("planYear").value;
  try {
    renderPlans((await api.get(`/forecasts/expansion-plans?year=${encodeURIComponent(year)}`)) || []);
  } catch (err) {
    showError(err.message);
  }
}

function renderPlans(items) {
  const body = document.getElementById("planTableBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Chưa có phòng ban nào.</td></tr>';
    return;
  }
  body.innerHTML = items.map((p) => `
    <tr data-department-id="${p.departmentId}" data-year="${p.year}">
      <td class="fw-semibold">${escapeHtml(p.departmentName)}</td>
      <td>
        <input type="number" min="0" max="5000" step="1" class="form-control form-control-sm plan-headcount ${p.hasPlan ? "" : "text-muted"}"
               value="${p.additionalHeadcount}" aria-label="Nhân sự tăng thêm ${escapeHtml(p.departmentName)}">
      </td>
      <td><input type="text" maxlength="500" class="form-control form-control-sm plan-notes" value="${escapeHtml(p.notes || "")}"
                 placeholder="Ghi chú (tuỳ chọn)" aria-label="Ghi chú ${escapeHtml(p.departmentName)}"></td>
      <td class="small text-muted">${p.hasPlan ? escapeHtml(formatDateTime(p.updatedAt)) : "Chưa có kế hoạch"}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-primary" data-action="save-plan"><i class="bi bi-check2"></i> Lưu</button>
        ${p.hasPlan ? '<button class="btn btn-sm btn-outline-danger" data-action="delete-plan"><i class="bi bi-trash"></i> Xoá</button>' : ""}
      </td>
    </tr>`).join("");
}

document.getElementById("planYear").addEventListener("change", loadPlans);

document.getElementById("planTableBody").addEventListener("click", async (event) => {
  const button = event.target.closest("[data-action]");
  if (!button) return;

  const row = button.closest("tr");
  const year = Number(row.dataset.year);
  const departmentId = Number(row.dataset.departmentId);
  clearError();

  try {
    if (button.dataset.action === "save-plan") {
      const raw = row.querySelector(".plan-headcount").value.trim();
      const headcount = Number(raw);
      if (raw === "" || !Number.isInteger(headcount) || headcount < 0 || headcount > 5000) {
        showError("Số nhân sự tăng thêm phải là số nguyên từ 0 đến 5.000.");
        return;
      }
      const notes = row.querySelector(".plan-notes").value.trim();
      await api.put(`/forecasts/expansion-plans/${year}/${departmentId}`, { additionalHeadcount: headcount, notes: notes || null });
      showSuccess("Đã lưu kế hoạch mở rộng. Bấm “Tạo dự báo” lại để áp dụng.");
    } else if (button.dataset.action === "delete-plan") {
      if (!confirm("Xoá kế hoạch mở rộng của phòng ban này trong năm đã chọn?")) return;
      await api.del(`/forecasts/expansion-plans/${year}/${departmentId}`);
      showSuccess("Đã xoá kế hoạch mở rộng. Bấm “Tạo dự báo” lại để áp dụng.");
    }
    await loadPlans();
  } catch (err) {
    showError(err.message);
  }
});

document.getElementById("planTableBody").addEventListener("keydown", (event) => {
  if (event.key === "Enter" && event.target.matches(".plan-headcount, .plan-notes")) {
    event.preventDefault();
    event.target.closest("tr").querySelector('[data-action="save-plan"]').click();
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
    .then(() => Promise.all([loadHistory(1), loadPrices(), loadRatios(), loadPlans()]));
}
