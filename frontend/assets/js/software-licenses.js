const state = {
  apiBaseUrl: localStorage.getItem("itamApiBaseUrl") || "https://localhost:7070/api/v1",
  token: localStorage.getItem("itamAccessToken") || ""
};

const apiBaseInput = document.getElementById("apiBaseUrl");
const tokenInput = document.getElementById("jwtToken");
const warningBanner = document.getElementById("warningBanner");
const errorBanner = document.getElementById("errorBanner");
const rows = document.getElementById("licenseRows");

apiBaseInput.value = state.apiBaseUrl;
tokenInput.value = state.token;

function showError(message) {
  errorBanner.textContent = message;
  errorBanner.style.display = "block";
}

function clearError() {
  errorBanner.textContent = "";
  errorBanner.style.display = "none";
}

async function api(path, options = {}) {
  clearError();
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (state.token) {
    headers.Authorization = `Bearer ${state.token}`;
  }

  const response = await fetch(`${state.apiBaseUrl}${path}`, { ...options, headers });
  if (response.status === 204) {
    return null;
  }

  const body = await response.json().catch(() => null);
  if (!response.ok) {
    const message = body?.message || `HTTP ${response.status}`;
    const errors = Array.isArray(body?.errors) ? ` ${body.errors.join("; ")}` : "";
    throw new Error(message + errors);
  }

  return body?.data;
}

function statusText(item) {
  if (item.isExpired) return "Đã hết hạn";
  if (item.isExpiringSoon && item.isNearUsageLimit) return "Sắp hết hạn · Gần giới hạn";
  if (item.isExpiringSoon) return "Sắp hết hạn";
  if (item.isNearUsageLimit) return "Gần giới hạn";
  return "Bình thường";
}

function rowClass(item) {
  if (item.isExpired) return "expired";
  if (item.isExpiringSoon || item.isNearUsageLimit) return "warning";
  return "";
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function renderTable(items) {
  rows.innerHTML = items.map(item => `
    <tr class="${rowClass(item)}">
      <td>${item.id}</td>
      <td>${escapeHtml(item.softwareName)}</td>
      <td>${escapeHtml(item.licenseKey)}</td>
      <td>${escapeHtml(item.expiryDate)}</td>
      <td>${item.currentUsage}/${item.maxUsage} (${item.usagePercentage}%)</td>
      <td>${statusText(item)}</td>
      <td class="actions">
        <button onclick="editLicense(${item.id})">Sửa</button>
        <button onclick="assignLicense(${item.id})">Gán</button>
        <button onclick="unassignLicense(${item.id})">Gỡ</button>
        <button onclick="deleteLicense(${item.id})">Xóa</button>
      </td>
    </tr>`).join("");
}

async function loadWarnings() {
  const items = await api("/software-licenses/expiring-soon?days=30");
  if (!items.length) {
    warningBanner.style.display = "none";
    warningBanner.textContent = "";
    return;
  }

  warningBanner.textContent = `Có ${items.length} license sẽ hết hạn trong 30 ngày tới.`;
  warningBanner.style.display = "block";
}

async function loadLicenses() {
  const search = document.getElementById("searchText").value.trim();
  const query = new URLSearchParams({ page: "1", pageSize: "100" });
  if (search) query.set("search", search);

  const result = await api(`/software-licenses?${query.toString()}`);
  renderTable(result.items || []);
}

async function refreshAll() {
  try {
    await Promise.all([loadWarnings(), loadLicenses()]);
  } catch (error) {
    showError(error.message);
  }
}

document.getElementById("saveConfig").addEventListener("click", () => {
  state.apiBaseUrl = apiBaseInput.value.trim().replace(/\/$/, "");
  state.token = tokenInput.value.trim();
  localStorage.setItem("itamApiBaseUrl", state.apiBaseUrl);
  localStorage.setItem("itamAccessToken", state.token);
  refreshAll();
});

document.getElementById("reloadData").addEventListener("click", refreshAll);
document.getElementById("searchButton").addEventListener("click", loadLicenses);

document.getElementById("createForm").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    await api("/software-licenses", {
      method: "POST",
      body: JSON.stringify({
        softwareName: document.getElementById("softwareName").value,
        licenseKey: document.getElementById("licenseKey").value,
        expiryDate: document.getElementById("expiryDate").value,
        maxUsage: Number(document.getElementById("maxUsage").value),
        notes: document.getElementById("notes").value || null
      })
    });
    event.target.reset();
    await refreshAll();
  } catch (error) {
    showError(error.message);
  }
});

window.assignLicense = async id => {
  const assetId = Number(prompt("Nhập AssetId cần gán license:"));
  if (!Number.isInteger(assetId) || assetId <= 0) return;

  try {
    await api(`/software-licenses/${id}/assign`, {
      method: "POST",
      body: JSON.stringify({ assetId })
    });
    await refreshAll();
  } catch (error) {
    showError(error.message);
  }
};

window.unassignLicense = async id => {
  const assetId = Number(prompt("Nhập AssetId cần gỡ license:"));
  if (!Number.isInteger(assetId) || assetId <= 0) return;

  try {
    await api(`/software-licenses/${id}/assign/${assetId}`, { method: "DELETE" });
    await refreshAll();
  } catch (error) {
    showError(error.message);
  }
};

window.deleteLicense = async id => {
  if (!confirm(`Xóa license Id = ${id}?`)) return;
  try {
    await api(`/software-licenses/${id}`, { method: "DELETE" });
    await refreshAll();
  } catch (error) {
    showError(error.message);
  }
};

window.editLicense = async id => {
  try {
    const current = await api(`/software-licenses/${id}`);
    const softwareName = prompt("Tên phần mềm:", current.softwareName);
    if (softwareName === null) return;
    const licenseKey = prompt("License Key:", current.licenseKey);
    if (licenseKey === null) return;
    const expiryDate = prompt("Ngày hết hạn (YYYY-MM-DD):", current.expiryDate);
    if (expiryDate === null) return;
    const maxUsageText = prompt("MaxUsage:", current.maxUsage);
    if (maxUsageText === null) return;
    const notes = prompt("Ghi chú:", current.notes || "");
    if (notes === null) return;

    await api(`/software-licenses/${id}`, {
      method: "PUT",
      body: JSON.stringify({
        softwareName,
        licenseKey,
        expiryDate,
        maxUsage: Number(maxUsageText),
        notes: notes || null
      })
    });
    await refreshAll();
  } catch (error) {
    showError(error.message);
  }
};

refreshAll();
