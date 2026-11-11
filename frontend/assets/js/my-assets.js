// Cổng nhân viên: xem tài sản được cấp và gửi/theo dõi yêu cầu bảo trì của chính mình.
const session = requireAuth("login.html");
renderNav({ active: "myAssets", basePath: "../" });

const ASSET_STATUS = {
  InUse: ["Đang dùng", "success"], Maintenance: ["Đang bảo trì", "warning"], Broken: ["Hỏng", "danger"], Disposed: ["Đã thanh lý", "slate"],
};
const TICKET_STATUS = { Pending: ["Chờ xử lý", "warning"], Resolved: ["Đã xử lý", "success"], Failed: ["Không xử lý được", "danger"] };
const PRIORITY = { Low: ["Thấp", "slate"], Normal: ["Bình thường", "info"], High: ["Cao", "warning"], Urgent: ["Khẩn cấp", "danger"] };

let assets = [];
let ticketModal;

function badge(map, key) {
  const [label, css] = map[key] || [key, "slate"];
  return `<span class="badge-soft ${css}">${escapeHtml(label)}</span>`;
}

function formatDate(value) {
  if (!value) return "-";
  const date = new Date(/^\d{4}-\d{2}-\d{2}$/.test(value) ? `${value}T00:00:00` : (/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`));
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString("vi-VN");
}

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function showSuccess(message) {
  const el = document.getElementById("successAlert");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearAlerts() {
  document.getElementById("errorAlert").classList.add("d-none");
  document.getElementById("successAlert").classList.add("d-none");
}

async function loadAssets() {
  try {
    assets = (await api.get("/my/assets")) || [];
  } catch (err) {
    showError(err.message);
    assets = [];
  }
  renderAssets();
}

function renderAssets() {
  const body = document.getElementById("assetTableBody");
  if (assets.length === 0) {
    body.innerHTML = `<tr><td colspan="7"><div class="empty-state"><i class="bi bi-inbox"></i>
      <div class="title">Bạn chưa được cấp thiết bị nào</div>Khi bộ phận IT bàn giao thiết bị cho bạn, thiết bị sẽ hiện ở đây.</div></td></tr>`;
    return;
  }

  body.innerHTML = assets.map((a) => {
    const canReport = !a.hasPendingTicket && (a.assetStatus === "InUse" || a.assetStatus === "Broken");
    const hint = a.hasPendingTicket ? "Đã có yêu cầu đang chờ xử lý" : "Thiết bị chưa thể báo bảo trì ở tình trạng này";
    return `
    <tr>
      <td class="fw-semibold">${escapeHtml(a.assetCode)}</td>
      <td>${escapeHtml(a.assetName)}</td>
      <td>${escapeHtml(a.categoryName)}</td>
      <td class="text-muted">${a.serialNumber ? escapeHtml(a.serialNumber) : "-"}</td>
      <td>${escapeHtml(formatDate(a.allocatedDate))}</td>
      <td>${badge(ASSET_STATUS, a.assetStatus)}</td>
      <td class="text-end">
        ${canReport
          ? `<button class="btn btn-sm btn-primary" data-action="report" data-id="${a.assetId}"><i class="bi bi-tools"></i> Yêu cầu bảo trì</button>`
          : `<span class="text-muted small">${escapeHtml(hint)}</span>`}
      </td>
    </tr>`;
  }).join("");
}

async function loadTickets(page = 1) {
  try {
    const result = await api.get(`/my/tickets?page=${page}&pageSize=10`);
    renderTickets(result.items || []);
    renderPagination(result);
    document.getElementById("ticketTotalLabel").textContent = `Tổng cộng: ${result.totalItems} yêu cầu`;
  } catch (err) {
    showError(err.message);
  }
}

function renderTickets(items) {
  const body = document.getElementById("ticketTableBody");
  if (items.length === 0) {
    body.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-4">Bạn chưa gửi yêu cầu bảo trì nào.</td></tr>';
    return;
  }

  body.innerHTML = items.map((t) => `
    <tr>
      <td><div class="fw-semibold">${escapeHtml(t.assetCode)}</div><div class="small text-muted">${escapeHtml(t.assetName)}</div></td>
      <td style="max-width:260px">${escapeHtml(t.issueDescription)}</td>
      <td>${badge(PRIORITY, t.priority)}</td>
      <td>${badge(TICKET_STATUS, t.status)}</td>
      <td>${escapeHtml(formatDate(t.reportedDate))}</td>
      <td>${t.technicianName ? escapeHtml(t.technicianName) : '<span class="text-muted">Chưa phân công</span>'}</td>
      <td style="max-width:240px">${t.notes ? escapeHtml(t.notes) : '<span class="text-muted">-</span>'}
        ${t.resolvedDate ? `<div class="small text-muted">${escapeHtml(formatDate(t.resolvedDate))}</div>` : ""}</td>
    </tr>`).join("");
}

function renderPagination(result) {
  const el = document.getElementById("ticketPagination");
  el.innerHTML = "";
  for (let page = 1; page <= (result.totalPages || 0); page += 1) {
    const li = document.createElement("li");
    li.className = `page-item ${page === result.page ? "active" : ""}`;
    li.innerHTML = `<button class="page-link" type="button">${page}</button>`;
    li.querySelector("button").addEventListener("click", () => loadTickets(page));
    el.appendChild(li);
  }
}

function openTicketModal(assetId) {
  const asset = assets.find((a) => a.assetId === assetId);
  if (!asset) return;
  document.getElementById("ticketForm").reset();
  document.getElementById("ticketAssetId").value = asset.assetId;
  document.getElementById("ticketAssetLabel").value = `${asset.assetCode} — ${asset.assetName}`;
  document.getElementById("ticketModalError").classList.add("d-none");
  ticketModal.show();
}

document.getElementById("assetTableBody").addEventListener("click", (event) => {
  const button = event.target.closest('[data-action="report"]');
  if (button) openTicketModal(Number(button.dataset.id));
});

document.getElementById("ticketForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const errorEl = document.getElementById("ticketModalError");
  errorEl.classList.add("d-none");

  const submit = document.getElementById("ticketSubmitBtn");
  submit.disabled = true; // tránh bấm đúp gửi hai phiếu
  try {
    await api.post("/maintenance-tickets", {
      assetId: Number(document.getElementById("ticketAssetId").value),
      issueDescription: document.getElementById("ticketIssue").value.trim(),
      priority: document.getElementById("ticketPriority").value,
    });
    ticketModal.hide();
    clearAlerts();
    showSuccess("Đã gửi yêu cầu bảo trì. Bộ phận IT sẽ tiếp nhận và xử lý.");
    await Promise.all([loadAssets(), loadTickets(1)]);
  } catch (err) {
    errorEl.textContent = err.message;
    errorEl.classList.remove("d-none");
  } finally {
    submit.disabled = false;
  }
});

if (session?.role !== "Employee") {
  document.getElementById("errorAlert").textContent = "Trang này dành cho tài khoản nhân viên.";
  document.getElementById("errorAlert").classList.remove("d-none");
} else {
  ticketModal = new bootstrap.Modal(document.getElementById("ticketModal"));
  loadAssets();
  loadTickets(1);
}
