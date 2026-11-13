const session = requireAuth("login.html");
renderNav({ active: "lifecycle", basePath: "../" });

const pageSize = 20;
let currentPage = 1;
let currentUpcomingPage = 1;

const oneDecimalFormatter = new Intl.NumberFormat("vi-VN", {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearError() {
  document.getElementById("errorAlert").classList.add("d-none");
}

function showOk(message) {
  const el = document.getElementById("okAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearOk() {
  document.getElementById("okAlert").classList.add("d-none");
}

function formatPercent(value) {
  return `${oneDecimalFormatter.format(Number(value || 0) * 100)}%`;
}

// Mã tài sản bấm được để mở chi tiết vòng đời (M2).
function assetLink(item) {
  return `<button type="button" class="btn btn-link p-0 fw-semibold text-decoration-none" data-asset-id="${item.assetId}">${escapeHtml(item.assetCode)}</button>`;
}

function formatDateTime(value) {
  if (!value) return "—";
  // API trả giờ UTC không kèm "Z" -> thêm "Z" để trình duyệt đổi đúng sang giờ địa phương.
  const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString("vi-VN");
}

function formatDateOnly(value) {
  if (!value) return "—";
  const parts = String(value).split("-");
  if (parts.length !== 3) return escapeHtml(value);
  return `${parts[2]}/${parts[1]}/${parts[0]}`;
}

async function loadFilterOptions() {
  try {
    const [departments, categories] = await Promise.all([
      api.get("/departments"),
      api.get("/asset-categories"),
    ]);

    document.getElementById("departmentId").innerHTML =
      '<option value="">-- Tất cả --</option>' +
      departments.map((item) => `<option value="${item.id}">${escapeHtml(item.name)}</option>`).join("");

    document.getElementById("categoryId").innerHTML =
      '<option value="">-- Tất cả --</option>' +
      categories.map((item) => `<option value="${item.id}">${escapeHtml(item.name)}</option>`).join("");
  } catch (err) {
    showError(`Không tải được dữ liệu bộ lọc: ${err.message}`);
  }
}

function currentDepartmentId() {
  return document.getElementById("departmentId").value;
}

function currentCategoryId() {
  return document.getElementById("categoryId").value;
}

function currentSortBy() {
  return document.getElementById("sortBy").value || "age";
}

// Khoảng cảnh báo hợp lệ 1–60 tháng (khớp backend); trả null nếu người dùng nhập sai để báo lỗi ngay thay vì chờ 400.
function currentUpcomingMonths() {
  const raw = document.getElementById("upcomingMonths").value.trim();
  const months = raw === "" ? 12 : Number(raw);
  return Number.isInteger(months) && months >= 1 && months <= 60 ? months : null;
}

// Đổi bộ lọc nhanh: chỉ phản hồi của yêu cầu MỚI NHẤT được hiển thị (phản hồi chậm của bộ lọc cũ bị bỏ).
const loadSeq = { stats: 0, candidates: 0, upcoming: 0 };

function optionText(selectId) {
  const select = document.getElementById(selectId);
  return select.value ? select.options[select.selectedIndex].text : "Tất cả";
}

async function loadStats() {
  const seq = ++loadSeq.stats;
  const params = new URLSearchParams();
  if (currentDepartmentId()) params.set("departmentId", currentDepartmentId());
  if (currentCategoryId()) params.set("categoryId", currentCategoryId());

  const stats = await api.get(`/lifecycle/stats${params.toString() ? `?${params}` : ""}`);
  if (seq !== loadSeq.stats) return;

  const invalidDates = stats.assetsWithoutPurchaseDate + stats.assetsWithFuturePurchaseDate;
  document.getElementById("scopeLabel").innerHTML =
    `<strong>Phạm vi thống kê:</strong> Phòng ban: ${escapeHtml(optionText("departmentId"))} · Loại: ${escapeHtml(optionText("categoryId"))} · ` +
    `${stats.totalAssets} tài sản chưa thanh lý · ${stats.totalTickets} phiếu bảo trì · tính đến ${formatDateOnly(stats.asOfDate)}`;
  document.getElementById("statTotalAssets").textContent = stats.totalAssets;
  document.getElementById("statAverageAge").textContent = formatAge(stats.averageAgeYears);
  document.getElementById("statOverdue").textContent = stats.overdueNowCount;
  document.getElementById("statUnknownPurchase").textContent = invalidDates === 0
    ? "Mọi tài sản đều có ngày mua hợp lệ"
    : `${invalidDates} tài sản thiếu/sai ngày mua (không tính tuổi)` +
      (stats.assetsWithFuturePurchaseDate ? ` — ${stats.assetsWithFuturePurchaseDate} có ngày mua ở tương lai` : "");

  renderCategoryStats(stats.byCategory || []);
  renderDepartmentStats(stats.byDepartment || []);
  renderTopFailing(stats.topFailingAssets || []);
}

function renderCategoryStats(items) {
  const body = document.getElementById("categoryStatsBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-4">Không có dữ liệu tài sản.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.categoryName)}</td>
      <td>${item.assetCount}</td>
      <td>${formatAge(item.averageAgeYears)}</td>
      <td>${formatPercent(item.failureRatio)}</td>
      <td>${item.maxAgeYears} năm / ${item.maxFailureCount} lần</td>
      <td>${item.overdueNowCount > 0 ? `<span class="badge text-bg-danger">${item.overdueNowCount}</span>` : "0"}</td>
    </tr>`).join("");
}

function renderDepartmentStats(items) {
  const body = document.getElementById("departmentStatsBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Không có dữ liệu.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.departmentName)}</td>
      <td>${item.assetCount}</td>
      <td>${formatAge(item.averageAgeYears)}</td>
      <td>${formatPercent(item.failureRatio)}</td>
      <td>${item.overdueNowCount}</td>
    </tr>`).join("");
}

function renderTopFailing(items) {
  const body = document.getElementById("topFailingBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-4">Chưa có tài sản phát sinh phiếu bảo trì.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td>${assetLink(item)}</td>
      <td>${escapeHtml(item.assetName)}</td>
      <td>${escapeHtml(item.categoryName)}</td>
      <td>${escapeHtml(item.departmentName)}</td>
      <td>${formatAge(item.ageYears)}</td>
      <td>${item.ticketCount}</td>
      <td>${item.failedTicketCount}</td>
    </tr>`).join("");
}

function buildCandidateQuery(page) {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy: currentSortBy(),
  });
  if (currentDepartmentId()) params.set("departmentId", currentDepartmentId());
  if (currentCategoryId()) params.set("categoryId", currentCategoryId());
  for (const [id, name] of [["keyword", "keyword"], ["reason", "reason"], ["underWarranty", "underWarranty"]]) {
    const value = document.getElementById(id).value.trim();
    if (value) params.set(name, value);
  }
  return params.toString();
}

async function loadCandidates(page = 1) {
  const seq = ++loadSeq.candidates;
  let result = await api.get(`/lifecycle/replacement-candidates?${buildCandidateQuery(page)}`);
  if (seq !== loadSeq.candidates) return;
  // Đang ở trang cuối rồi dữ liệu/bộ lọc làm số trang giảm: nhảy về trang cuối còn dữ liệu thay vì hiện bảng rỗng.
  if (!(result.items || []).length && result.totalItems > 0 && page > 1) {
    result = await api.get(`/lifecycle/replacement-candidates?${buildCandidateQuery(result.totalPages)}`);
    if (seq !== loadSeq.candidates) return;
  }
  currentPage = result.page;
  renderCandidates(result.items || []);
  renderPagination(result, "pagination", loadCandidates);
  document.getElementById("candidateTotalLabel").textContent = `Tổng cộng: ${result.totalItems} tài sản`;
}

function warrantyBadge(item) {
  if (!item.isUnderWarranty) return "";
  return '<span class="badge text-bg-success ms-1" title="Còn bảo hành — cân nhắc sửa/đổi trả trước khi thay">Còn bảo hành</span>';
}

function renderCandidates(items) {
  const body = document.getElementById("candidateBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-4">Không có tài sản đã vượt ngưỡng thay thế.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td>${assetLink(item)}${warrantyBadge(item)}</td>
      <td>${escapeHtml(item.assetName)}</td>
      <td>${escapeHtml(item.categoryName)}</td>
      <td>${escapeHtml(item.departmentName)}</td>
      <td title="Ngưỡng áp dụng: ${item.appliedMaxAgeYears} năm">${formatAge(item.ageYears)}</td>
      <td title="Ngưỡng áp dụng: ${item.appliedMaxFailureCount} phiếu">${item.ticketCount}</td>
      <td>${escapeHtml(item.reasonText || "—")}</td>
    </tr>`).join("");
}

function buildUpcomingQuery(page) {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    months: String(currentUpcomingMonths()),
  });
  if (currentDepartmentId()) params.set("departmentId", currentDepartmentId());
  if (currentCategoryId()) params.set("categoryId", currentCategoryId());
  for (const [id, name] of [["keyword", "keyword"], ["upcomingReason", "reason"], ["minTickets", "minTickets"]]) {
    const value = document.getElementById(id).value.trim();
    if (value) params.set(name, value);
  }
  return params.toString();
}

async function loadUpcoming(page = 1) {
  if (currentUpcomingMonths() === null) {
    throw new Error("Số tháng cảnh báo phải là số nguyên từ 1 đến 60.");
  }
  const seq = ++loadSeq.upcoming;
  let result = await api.get(`/lifecycle/upcoming?${buildUpcomingQuery(page)}`);
  if (seq !== loadSeq.upcoming) return;
  if (!(result.items || []).length && result.totalItems > 0 && page > 1) {
    result = await api.get(`/lifecycle/upcoming?${buildUpcomingQuery(result.totalPages)}`);
    if (seq !== loadSeq.upcoming) return;
  }
  currentUpcomingPage = result.page;
  renderUpcoming(result.items || []);
  renderPagination(result, "upcomingPagination", loadUpcoming);
  document.getElementById("upcomingTotalLabel").textContent = `Tổng cộng: ${result.totalItems} tài sản`;
}

function renderUpcoming(items) {
  const body = document.getElementById("upcomingBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="9" class="text-center text-muted py-4">Không có tài sản sắp tới hạn trong khoảng đã chọn.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td>${assetLink(item)}</td>
      <td>${escapeHtml(item.assetName)}</td>
      <td>${escapeHtml(item.categoryName)}</td>
      <td>${escapeHtml(item.departmentName)}</td>
      <td>${formatAge(item.ageYears)}</td>
      <td>${item.ticketCount}</td>
      <td>${formatDateOnly(item.ageDueDate)}</td>
      <td>${item.daysUntilAgeDue ?? "—"}</td>
      <td>${escapeHtml(item.reasonText || "—")}</td>
    </tr>`).join("");
}

function renderPagination(result, elementId, loader) {
  const el = document.getElementById(elementId);
  el.innerHTML = "";
  const total = result.totalPages;
  if (total <= 1) return;
  // Rút gọn: trang đầu, cuối và vài trang quanh trang hiện tại (không dựng hàng trăm nút).
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
    li.querySelector("button").addEventListener("click", () => loader(page).catch((err) => showError(err.message)));
    el.appendChild(li);
    prev = page;
  }
}

let policyCache = new Map();

function formatUpdated(item) {
  if (!item.updatedAt && !item.updatedByName) {
    return item.isOverridden ? '<span class="text-muted small">Không rõ (cấu hình có từ trước)</span>' : '<span class="text-muted small">—</span>';
  }
  const who = item.updatedByName ? ` bởi ${escapeHtml(item.updatedByName)}` : "";
  return `<span class="small">${escapeHtml(formatDateTime(item.updatedAt))}${who}</span>`;
}

async function loadPolicies() {
  const policies = await api.get("/lifecycle/policies");
  const body = document.getElementById("policyBody");
  policyCache = new Map(policies.map((item) => [item.categoryId, item]));
  if (policies.length) {
    document.getElementById("defaultsLabel").textContent =
      `${policies[0].defaultMaxAgeYears} năm / ${policies[0].defaultMaxFailureCount} phiếu bảo trì`;
  }
  if (!policies.length) {
    body.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-4">Chưa có loại tài sản.</td></tr>';
    return;
  }

  body.innerHTML = policies.map((item) => `
    <tr data-category-id="${item.categoryId}">
      <td class="fw-semibold">${escapeHtml(item.categoryName)}</td>
      <td><input type="number" min="1" max="30" step="1" class="form-control form-control-sm policy-age" value="${item.maxAgeYears}"></td>
      <td><input type="number" min="1" max="100" step="1" class="form-control form-control-sm policy-failure" value="${item.maxFailureCount}"></td>
      <td>${item.isOverridden
        ? '<span class="badge text-bg-primary" title="Admin đã đặt ngưỡng riêng cho loại này">Tuỳ chỉnh</span>'
        : `<span class="badge text-bg-secondary" title="Chưa chỉnh riêng: đang dùng mặc định hệ thống (${item.defaultMaxAgeYears} năm / ${item.defaultMaxFailureCount} phiếu)">Mặc định hệ thống</span>`}
        <span class="badge text-bg-success" title="Ngưỡng này đang được dùng để đánh giá ngay bây giờ">Đang áp dụng</span></td>
      <td>${formatUpdated(item)}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-primary me-1" onclick="savePolicy(${item.categoryId})"><i class="bi bi-save"></i> Lưu</button>
        <button class="btn btn-sm btn-outline-secondary" ${item.isOverridden ? "" : "disabled"} onclick="resetPolicy(${item.categoryId})">Về mặc định</button>
      </td>
    </tr>`).join("");
}

// Sau khi đổi ngưỡng: số liệu và hai danh sách thay đổi theo; giữ nguyên trang đang xem (tự lùi nếu trang đó không còn).
async function refreshAfterPolicyChange() {
  await Promise.all([loadPolicies(), loadStats(), loadCandidates(currentPage), loadUpcoming(currentUpcomingPage)]);
}

// Lỗi 409 (người khác vừa sửa): nạp lại bảng ngưỡng để thấy giá trị mới nhất, không ghi đè lên chỗ người kia vừa lưu.
async function handlePolicyError(err) {
  showError(err.message);
  if (err.status === 409) {
    try { await loadPolicies(); } catch { /* giữ nguyên bảng hiện tại */ }
  }
}

function parsePositiveInt(raw) {
  const text = String(raw).trim();
  return /^\d+$/.test(text) ? Number(text) : null;
}

async function savePolicy(categoryId) {
  clearError();
  clearOk();
  const row = document.querySelector(`tr[data-category-id="${categoryId}"]`);
  const current = policyCache.get(categoryId);
  const maxAgeYears = parsePositiveInt(row.querySelector(".policy-age").value);
  const maxFailureCount = parsePositiveInt(row.querySelector(".policy-failure").value);

  if (maxAgeYears === null || maxAgeYears < 1 || maxAgeYears > 30) {
    showError("Tuổi tối đa phải là số nguyên từ 1 đến 30 năm.");
    return;
  }
  if (maxFailureCount === null || maxFailureCount < 1 || maxFailureCount > 100) {
    showError("Số lần lỗi tối đa phải là số nguyên từ 1 đến 100.");
    return;
  }
  if (current && maxAgeYears === current.maxAgeYears && maxFailureCount === current.maxFailureCount) {
    showOk(`Ngưỡng của "${current.categoryName}" không thay đổi nên không cần lưu.`);
    return;
  }

  try {
    await api.put(`/lifecycle/policies/${categoryId}`, {
      maxAgeYears,
      maxFailureCount,
      version: current && current.isOverridden ? current.version : null,
    });
    showOk(`Đã lưu ngưỡng của "${current ? current.categoryName : "loại tài sản"}": ${maxAgeYears} năm / ${maxFailureCount} phiếu. Có hiệu lực ngay.`);
    await refreshAfterPolicyChange();
  } catch (err) {
    await handlePolicyError(err);
  }
}

async function resetPolicy(categoryId) {
  clearError();
  clearOk();
  const current = policyCache.get(categoryId);
  if (!current || !current.isOverridden) return;

  const message = `Đặt "${current.categoryName}" về mặc định hệ thống?\n\n` +
    `Ngưỡng riêng hiện tại: ${current.maxAgeYears} năm / ${current.maxFailureCount} phiếu\n` +
    `Mặc định hệ thống: ${current.defaultMaxAgeYears} năm / ${current.defaultMaxFailureCount} phiếu\n\n` +
    `Chỉ loại này bị ảnh hưởng, số liệu và đề xuất thay thế sẽ được tính lại ngay. Thao tác được ghi vào Nhật ký hệ thống.`;
  if (!confirm(message)) return;

  try {
    await api.del(`/lifecycle/policies/${categoryId}?version=${current.version}`);
    showOk(`Đã đặt "${current.categoryName}" về mặc định hệ thống (${current.defaultMaxAgeYears} năm / ${current.defaultMaxFailureCount} phiếu).`);
    await refreshAfterPolicyChange();
  } catch (err) {
    await handlePolicyError(err);
  }
}

window.savePolicy = savePolicy;
window.resetPolicy = resetPolicy;

async function reloadData(page = 1) {
  clearError();
  try {
    await Promise.all([
      loadStats(),
      loadCandidates(page),
      loadUpcoming(1),
      loadPolicies(),
    ]);
  } catch (err) {
    showError(err.message);
  }
}

// Đổi bộ lọc chỉ tải lại thống kê và hai danh sách (không đụng tới bảng ngưỡng chính sách đang chỉnh dở).
initFilterBar({
  fields: ["keyword", "departmentId", "categoryId", "reason", "underWarranty", "sortBy", "upcomingReason", "minTickets"],
  uncounted: ["sortBy"],
  textFields: ["keyword", "minTickets"],
  onChange: () => {
    clearError();
    clearOk();
    Promise.all([loadStats(), loadCandidates(1), loadUpcoming(1)]).catch((err) => showError(err.message));
  },
});
document.getElementById("loadUpcomingBtn").addEventListener("click", () => {
  clearError();
  loadUpcoming(1).catch((err) => showError(err.message));
});

// ---------- Chi tiết vòng đời một tài sản (M2) ----------

const assetDetailModal = bootstrap.Modal.getOrCreateInstance(document.getElementById("assetDetailModal"));

const TICKET_STATUS = {
  Pending: ["Đang xử lý", "text-bg-warning"],
  Resolved: ["Đã xử lý", "text-bg-success"],
  Failed: ["Không sửa được", "text-bg-danger"],
};

let assetDetailRequestId = 0;

async function openAssetDetail(assetId) {
  const requestId = ++assetDetailRequestId;   // bỏ qua phản hồi cũ nếu người dùng bấm tài sản khác quá nhanh
  const errorEl = document.getElementById("assetDetailError");
  errorEl.classList.add("d-none");
  document.getElementById("assetDetailBody").classList.add("d-none");
  document.getElementById("assetDetailTitle").textContent = "Đang tải chi tiết vòng đời...";
  assetDetailModal.show();

  try {
    const detail = await api.get(`/lifecycle/assets/${assetId}`);
    if (requestId !== assetDetailRequestId) return;
    renderAssetDetail(detail);
    document.getElementById("assetDetailBody").classList.remove("d-none");
  } catch (err) {
    if (requestId !== assetDetailRequestId) return;
    document.getElementById("assetDetailTitle").textContent = "Chi tiết vòng đời";
    errorEl.textContent = err.message;
    errorEl.classList.remove("d-none");
  }
}

function renderAssetDetail(d) {
  const setText = (id, value) => { document.getElementById(id).textContent = value; };

  setText("assetDetailTitle", `${d.assetCode} — ${d.assetName}`);
  setText("adCategoryDept", `${d.categoryName} / ${d.departmentName}`);
  setText("adPurchaseAge", d.purchaseDateStatus === "InFuture"
    ? `${formatDateOnly(d.purchaseDate)} — ngày mua ở TƯƠNG LAI (dữ liệu sai, không tính tuổi; hãy sửa trong màn Tài sản)`
    : d.purchaseDate
      ? `${formatDateOnly(d.purchaseDate)} (${formatAge(d.ageYears)})`
      : "Chưa có ngày mua");
  document.getElementById("adWarranty").innerHTML = d.warrantyExpiry
    ? `${formatDateOnly(d.warrantyExpiry)} ${d.isUnderWarranty
        ? '<span class="badge text-bg-success" title="Còn bảo hành — cân nhắc sửa/đổi trả trước khi thay">Còn bảo hành</span>'
        : '<span class="badge text-bg-secondary">Hết bảo hành</span>'}`
    : "Không có thông tin bảo hành";
  setText("adThresholds", `${d.appliedMaxAgeYears} năm / ${d.appliedMaxFailureCount} phiếu bảo trì`);
  setText("adAgeDue", d.ageDueDate ? formatDateOnly(d.ageDueDate) : "—");

  const status = d.isOverdueNow
    ? `<span class="badge text-bg-danger">Đã vượt ngưỡng${d.isHighPriority ? " (cả tuổi và số phiếu)" : ""}</span>`
    : '<span class="badge text-bg-success">Chưa vượt ngưỡng</span>';
  document.getElementById("adStatus").innerHTML =
    `${status} <span class="text-muted small ms-1">${d.dueYear ? `Năm đến hạn: ${d.dueYear}` : "Chưa xác định năm đến hạn (thiếu ngày mua)"}</span>`;
  setText("adReason", d.reasonText || "Chưa vượt ngưỡng nào.");

  setText("adTicketCount", `(${d.tickets.length} phiếu, ${d.failedTicketCount} không sửa được)`);
  document.getElementById("adTicketsBody").innerHTML = d.tickets.length
    ? d.tickets.map((t) => {
        const [label, css] = TICKET_STATUS[t.status] || [t.status, "text-bg-secondary"];
        return `
        <tr>
          <td>#${t.id}</td>
          <td class="text-nowrap">${escapeHtml(formatDateTime(t.reportedDate))}</td>
          <td><span class="badge ${css}">${escapeHtml(label)}</span></td>
          <td class="text-nowrap">${escapeHtml(formatDateTime(t.resolvedDate))}</td>
          <td>${escapeHtml(t.description || "—")}</td>
        </tr>`;
      }).join("")
    : '<tr><td colspan="5" class="text-center text-muted py-3">Tài sản chưa có phiếu bảo trì nào.</td></tr>';
}

document.addEventListener("click", (event) => {
  const link = event.target.closest("[data-asset-id]");
  if (link) openAssetDetail(Number(link.dataset.assetId));
});

// ---------- Khởi động ----------
if (!can("lifecycle.view")) {
  document.getElementById("forbiddenAlert").textContent = "Chỉ Admin IT được phép xem và cấu hình Vòng đời tài sản.";
  document.getElementById("forbiddenAlert").classList.remove("d-none");
  document.getElementById("lifecycleContent").classList.add("d-none");
} else {
  loadFilterOptions().then(() => reloadData(1));
}
