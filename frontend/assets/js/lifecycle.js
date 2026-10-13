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

function formatOneDecimal(value) {
  if (value === null || value === undefined) return "—";
  return oneDecimalFormatter.format(Number(value));
}

function formatPercent(value) {
  return `${oneDecimalFormatter.format(Number(value || 0) * 100)}%`;
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
  return document.getElementById("sortBy").value || "score";
}

function currentUpcomingMonths() {
  return Number(document.getElementById("upcomingMonths").value || 12);
}

async function loadStats() {
  const params = new URLSearchParams();
  if (currentDepartmentId()) params.set("departmentId", currentDepartmentId());

  const stats = await api.get(`/lifecycle/stats${params.toString() ? `?${params}` : ""}`);
  document.getElementById("statTotalAssets").textContent = stats.totalAssets;
  document.getElementById("statAverageAge").textContent = formatOneDecimal(stats.averageAgeYears);
  document.getElementById("statMedianAge").textContent = formatOneDecimal(stats.medianAgeYears);
  document.getElementById("statFailureRatio").textContent = formatPercent(stats.failureRatio);
  document.getElementById("statAverageTickets").textContent = formatOneDecimal(stats.averageTicketsPerAsset);
  document.getElementById("statOverdue").textContent = stats.overdueNowCount;
  document.getElementById("statUnknownPurchase").textContent = `${stats.assetsWithoutPurchaseDate} tài sản chưa có ngày mua`;

  renderCategoryStats(stats.byCategory || []);
  renderAgeBuckets(stats.ageBuckets || []);
  renderDepartmentStats(stats.byDepartment || []);
  renderTopFailing(stats.topFailingAssets || []);
  renderTicketTrend(stats.ticketTrend || []);
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
      <td>${formatOneDecimal(item.averageAgeYears)}</td>
      <td>${formatPercent(item.failureRatio)}</td>
      <td>${item.maxAgeYears} năm / ${item.maxFailureCount} lần</td>
      <td>${item.overdueNowCount > 0 ? `<span class="badge text-bg-danger">${item.overdueNowCount}</span>` : "0"}</td>
    </tr>`).join("");
}

function renderAgeBuckets(items) {
  const body = document.getElementById("ageBucketsBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Không có dữ liệu.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.label)}</td>
      <td>${item.count}</td>
      <td>${item.assetsWithAtLeastOneTicket}</td>
      <td>${formatPercent(item.failureRatio)}</td>
      <td>${formatOneDecimal(item.averageTicketsPerAsset)}</td>
    </tr>`).join("");
}

function renderDepartmentStats(items) {
  const body = document.getElementById("departmentStatsBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-4">Không có dữ liệu.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.departmentName)}</td>
      <td>${item.totalAssets}</td>
      <td>${formatOneDecimal(item.averageAgeYears)}</td>
      <td>${formatPercent(item.failureRatio)}</td>
      <td>${formatOneDecimal(item.averageTicketsPerAsset)}</td>
      <td>${item.overdueNowCount}</td>
    </tr>`).join("");
}

function renderTopFailing(items) {
  const body = document.getElementById("topFailingBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="6" class="text-center text-muted py-4">Chưa có tài sản phát sinh phiếu bảo trì.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.assetCode)}</td>
      <td>${escapeHtml(item.assetName)}</td>
      <td>${escapeHtml(item.categoryName)}</td>
      <td>${escapeHtml(item.departmentName)}</td>
      <td>${item.ticketCount}</td>
      <td>${item.failedTicketCount}</td>
    </tr>`).join("");
}

function renderTicketTrend(items) {
  const body = document.getElementById("ticketTrendBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="2" class="text-center text-muted py-4">Không có dữ liệu.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr><td>${item.year}</td><td>${item.ticketCount}</td></tr>`).join("");
}

function buildCandidateQuery(page) {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy: currentSortBy(),
  });
  if (currentDepartmentId()) params.set("departmentId", currentDepartmentId());
  if (currentCategoryId()) params.set("categoryId", currentCategoryId());
  return params.toString();
}

async function loadCandidates(page = 1) {
  const result = await api.get(`/lifecycle/replacement-candidates?${buildCandidateQuery(page)}`);
  currentPage = result.page;
  renderCandidates(result.items || []);
  renderPagination(result, "pagination", loadCandidates);
  document.getElementById("candidateTotalLabel").textContent = `Tổng cộng: ${result.totalItems} tài sản`;
}

function priorityBar(score) {
  const value = Math.max(0, Math.min(100, Number(score || 0)));
  const css = value >= 80 ? "bg-danger" : value >= 50 ? "bg-warning" : "bg-success";
  return `
    <div class="d-flex align-items-center gap-2" style="min-width: 120px;">
      <div class="progress flex-grow-1" style="height: 8px;">
        <div class="progress-bar ${css}" role="progressbar" style="width: ${value}%" aria-valuenow="${value}" aria-valuemin="0" aria-valuemax="100"></div>
      </div>
      <span class="fw-semibold">${value}</span>
    </div>`;
}

function warrantyBadge(item) {
  if (!item.isUnderWarranty) return "";
  return '<span class="badge text-bg-success ms-1" title="Còn bảo hành — cân nhắc sửa/đổi trả trước khi thay">Còn bảo hành</span>';
}

function renderCandidates(items) {
  const body = document.getElementById("candidateBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="8" class="text-center text-muted py-4">Không có tài sản đã vượt ngưỡng thay thế.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.assetCode)}${warrantyBadge(item)}</td>
      <td>${escapeHtml(item.assetName)}</td>
      <td>${escapeHtml(item.categoryName)}</td>
      <td>${escapeHtml(item.departmentName)}</td>
      <td title="Ngưỡng áp dụng: ${item.appliedMaxAgeYears} năm">${formatOneDecimal(item.ageYears)}</td>
      <td title="Ngưỡng áp dụng: ${item.appliedMaxFailureCount} phiếu">${item.ticketCount} <span class="text-muted small">(${item.failedTicketCount} lỗi)</span></td>
      <td>${priorityBar(item.priorityScore)}</td>
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
  return params.toString();
}

async function loadUpcoming(page = 1) {
  const result = await api.get(`/lifecycle/upcoming?${buildUpcomingQuery(page)}`);
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
      <td class="fw-semibold">${escapeHtml(item.assetCode)}</td>
      <td>${escapeHtml(item.assetName)}</td>
      <td>${escapeHtml(item.categoryName)}</td>
      <td>${escapeHtml(item.departmentName)}</td>
      <td>${formatOneDecimal(item.ageYears)}</td>
      <td>${item.ticketCount}</td>
      <td>${formatDateOnly(item.ageDueDate)}</td>
      <td>${item.daysUntilAgeDue ?? "—"}</td>
      <td>${escapeHtml(item.reasonText || "—")}</td>
    </tr>`).join("");
}

function renderPagination(result, elementId, loader) {
  const el = document.getElementById(elementId);
  el.innerHTML = "";
  for (let page = 1; page <= result.totalPages; page += 1) {
    const li = document.createElement("li");
    li.className = `page-item ${page === result.page ? "active" : ""}`;
    li.innerHTML = `<button class="page-link" type="button">${page}</button>`;
    li.querySelector("button").addEventListener("click", () => loader(page));
    el.appendChild(li);
  }
}

async function loadPolicies() {
  const policies = await api.get("/lifecycle/policies");
  const body = document.getElementById("policyBody");
  if (!policies.length) {
    body.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Chưa có loại tài sản.</td></tr>';
    return;
  }

  body.innerHTML = policies.map((item) => `
    <tr data-category-id="${item.categoryId}">
      <td class="fw-semibold">${escapeHtml(item.categoryName)}</td>
      <td><input type="number" min="1" max="30" class="form-control form-control-sm policy-age" value="${item.maxAgeYears}"></td>
      <td><input type="number" min="1" max="100" class="form-control form-control-sm policy-failure" value="${item.maxFailureCount}"></td>
      <td>${item.isOverridden
        ? '<span class="badge text-bg-primary">Ngưỡng riêng</span>'
        : '<span class="badge text-bg-secondary">Mặc định</span>'}</td>
      <td class="text-end text-nowrap">
        <button class="btn btn-sm btn-primary me-1" onclick="savePolicy(${item.categoryId})"><i class="bi bi-save"></i> Lưu</button>
        <button class="btn btn-sm btn-outline-secondary" ${item.isOverridden ? "" : "disabled"} onclick="resetPolicy(${item.categoryId})">Về mặc định</button>
      </td>
    </tr>`).join("");
}

async function savePolicy(categoryId) {
  clearError();
  const row = document.querySelector(`tr[data-category-id="${categoryId}"]`);
  const maxAgeYears = Number(row.querySelector(".policy-age").value);
  const maxFailureCount = Number(row.querySelector(".policy-failure").value);

  try {
    await api.put(`/lifecycle/policies/${categoryId}`, { maxAgeYears, maxFailureCount });
    await Promise.all([
      loadPolicies(),
      loadStats(),
      loadCandidates(currentPage),
      loadUpcoming(currentUpcomingPage),
    ]);
  } catch (err) {
    showError(err.message);
  }
}

async function resetPolicy(categoryId) {
  if (!confirm("Xoá ngưỡng riêng và quay về cấu hình mặc định?")) return;
  clearError();
  try {
    await api.del(`/lifecycle/policies/${categoryId}`);
    await Promise.all([
      loadPolicies(),
      loadStats(),
      loadCandidates(currentPage),
      loadUpcoming(currentUpcomingPage),
    ]);
  } catch (err) {
    showError(err.message);
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

document.getElementById("filterForm").addEventListener("submit", (event) => {
  event.preventDefault();
  reloadData(1);
});

document.getElementById("resetFilterBtn").addEventListener("click", () => {
  document.getElementById("departmentId").value = "";
  document.getElementById("categoryId").value = "";
  document.getElementById("sortBy").value = "score";
  reloadData(1);
});

document.getElementById("sortBy").addEventListener("change", () => loadCandidates(1).catch((err) => showError(err.message)));
document.getElementById("loadUpcomingBtn").addEventListener("click", () => {
  clearError();
  loadUpcoming(1).catch((err) => showError(err.message));
});

if (session?.role !== "Admin IT") {
  document.getElementById("forbiddenAlert").textContent = "Chỉ Admin IT được phép xem và cấu hình Vòng đời tài sản.";
  document.getElementById("forbiddenAlert").classList.remove("d-none");
  document.getElementById("lifecycleContent").classList.add("d-none");
} else {
  loadFilterOptions().then(() => reloadData(1));
}
