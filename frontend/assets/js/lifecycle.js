const session = requireAuth("login.html");
renderNav({ active: "lifecycle", basePath: "../" });

const pageSize = 20;
let currentPage = 1;

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
  return Number(value).toFixed(1);
}

function formatPercent(value) {
  return `${(Number(value || 0) * 100).toFixed(1)}%`;
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

async function loadStats() {
  const params = new URLSearchParams();
  if (currentDepartmentId()) params.set("departmentId", currentDepartmentId());

  const stats = await api.get(`/lifecycle/stats${params.toString() ? `?${params}` : ""}`);
  document.getElementById("statTotalAssets").textContent = stats.totalAssets;
  document.getElementById("statAverageAge").textContent = formatOneDecimal(stats.averageAgeYears);
  document.getElementById("statFailureRatio").textContent = formatPercent(stats.failureRatio);
  document.getElementById("statAverageTickets").textContent = formatOneDecimal(stats.averageTicketsPerAsset);
  document.getElementById("statOverdue").textContent = stats.overdueNowCount;
  document.getElementById("statUnknownPurchase").textContent = `${stats.unknownPurchaseDateCount} tài sản chưa có ngày mua`;

  renderCategoryStats(stats.byCategory || []);
  renderAgeBuckets(stats.ageBuckets || []);
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
      <td>${item.totalAssets}</td>
      <td>${formatOneDecimal(item.averageAgeYears)}</td>
      <td>${formatPercent(item.failureRatio)}</td>
      <td>${item.maxAgeYears} năm / ${item.maxFailureCount} lần</td>
      <td>${item.overdueNowCount > 0 ? `<span class="badge text-bg-danger">${item.overdueNowCount}</span>` : "0"}</td>
    </tr>`).join("");
}

function renderAgeBuckets(items) {
  const el = document.getElementById("ageBuckets");
  if (!items.length) {
    el.innerHTML = '<div class="text-muted">Không có dữ liệu.</div>';
    return;
  }

  el.innerHTML = items.map((item) => `
    <div class="col-6 col-md">
      <div class="border rounded p-3 text-center h-100">
        <div class="fw-bold fs-5">${item.count}</div>
        <div class="text-muted small">${escapeHtml(item.label)}</div>
      </div>
    </div>`).join("");
}

function buildCandidateQuery(page) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (currentDepartmentId()) params.set("departmentId", currentDepartmentId());
  if (currentCategoryId()) params.set("categoryId", currentCategoryId());
  return params.toString();
}

async function loadCandidates(page = 1) {
  const result = await api.get(`/lifecycle/replacement-candidates?${buildCandidateQuery(page)}`);
  currentPage = result.page;
  renderCandidates(result.items || []);
  renderPagination(result);
  document.getElementById("candidateTotalLabel").textContent = `Tổng cộng: ${result.totalItems} tài sản`;
}

function reasonBadges(item) {
  const reasons = item.reasons || [];
  if (item.priority >= 2) {
    return '<span class="badge text-bg-danger">Ưu tiên cao</span>';
  }

  return reasons.map((reason) => {
    const css = reason === "Lỗi nhiều" ? "text-bg-warning" : "text-bg-secondary";
    return `<span class="badge ${css} me-1">${escapeHtml(reason)}</span>`;
  }).join("");
}

function renderCandidates(items) {
  const body = document.getElementById("candidateBody");
  if (!items.length) {
    body.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-4">Không có tài sản đã vượt ngưỡng thay thế.</td></tr>';
    return;
  }

  body.innerHTML = items.map((item) => `
    <tr>
      <td class="fw-semibold">${escapeHtml(item.assetCode)}</td>
      <td>${escapeHtml(item.assetName)}</td>
      <td>${escapeHtml(item.categoryName)}</td>
      <td>${escapeHtml(item.departmentName)}</td>
      <td>${formatOneDecimal(item.ageYears)}</td>
      <td>${item.ticketCount} <span class="text-muted small">(${item.failedTicketCount} lỗi)</span></td>
      <td>${reasonBadges(item)}</td>
    </tr>`).join("");
}

function renderPagination(result) {
  const el = document.getElementById("pagination");
  el.innerHTML = "";
  for (let page = 1; page <= result.totalPages; page += 1) {
    const li = document.createElement("li");
    li.className = `page-item ${page === result.page ? "active" : ""}`;
    li.innerHTML = `<button class="page-link" type="button">${page}</button>`;
    li.querySelector("button").addEventListener("click", () => loadCandidates(page));
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
    await Promise.all([loadPolicies(), loadStats(), loadCandidates(currentPage)]);
  } catch (err) {
    showError(err.message);
  }
}

async function resetPolicy(categoryId) {
  if (!confirm("Xoá ngưỡng riêng và quay về cấu hình mặc định?")) return;
  clearError();
  try {
    await api.del(`/lifecycle/policies/${categoryId}`);
    await Promise.all([loadPolicies(), loadStats(), loadCandidates(currentPage)]);
  } catch (err) {
    showError(err.message);
  }
}

window.savePolicy = savePolicy;
window.resetPolicy = resetPolicy;

async function reloadData(page = 1) {
  clearError();
  try {
    await Promise.all([loadStats(), loadCandidates(page), loadPolicies()]);
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
  reloadData(1);
});

if (session?.role !== "Admin IT") {
  document.getElementById("forbiddenAlert").textContent = "Chỉ Admin IT được phép xem và cấu hình Vòng đời tài sản.";
  document.getElementById("forbiddenAlert").classList.remove("d-none");
  document.getElementById("lifecycleContent").classList.add("d-none");
} else {
  loadFilterOptions().then(() => reloadData(1));
}
