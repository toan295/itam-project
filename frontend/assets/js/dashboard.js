// Trang chủ / Dashboard (UC-18). Mọi con số lấy từ GET /dashboard/summary (cùng nguồn với trang Vòng đời và báo cáo xuất file);
// frontend chỉ hiển thị, không tự tính lại. Mỗi widget độc lập: lỗi/ẩn của một widget không ảnh hưởng widget khác.
const session = requireAuth("pages/login.html");

const STATUS_COLORS = { InUse: "#15803d", Maintenance: "#b45309", Broken: "#b91c1c" };
const STATUS_LABELS = { InUse: "Đang sử dụng", Maintenance: "Đang bảo trì", Broken: "Hỏng" };
const COLOR_MUTED = "#94a3b8";
const COLOR_WARN = "#d97706";

const charts = {};
let loadSeq = 0;
let currentSummary = null;

const numberFmt = new Intl.NumberFormat("vi-VN");
const percentFmt = new Intl.NumberFormat("vi-VN", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

const fmtInt = (n) => numberFmt.format(Number(n || 0));
const fmtPercent = (ratio) => `${percentFmt.format(Number(ratio || 0) * 100)}%`;

function fmtDateTime(value) {
  if (!value) return "—";
  // API trả giờ UTC không kèm "Z" -> thêm "Z" để trình duyệt đổi đúng sang giờ địa phương.
  const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString("vi-VN");
}

function el(id) {
  return document.getElementById(id);
}

// ---------- Chart ----------

function drawChart(id, config, summaryText) {
  if (charts[id]) charts[id].destroy();
  const canvas = el(id);
  canvas.setAttribute("aria-label", summaryText);
  charts[id] = new Chart(canvas, config);
}

function clearChart(id) {
  if (charts[id]) {
    charts[id].destroy();
    delete charts[id];
  }
}

const baseOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { position: "bottom", labels: { boxWidth: 10, font: { size: 12 } } } },
};

function stackedStatusChart(id, rows, horizontal, label) {
  const names = rows.map((r) => r.name);
  const dataset = (key, status) => ({
    label: STATUS_LABELS[status],
    data: rows.map((r) => r[key]),
    backgroundColor: STATUS_COLORS[status],
  });
  drawChart(id, {
    type: "bar",
    data: { labels: names, datasets: [dataset("inUse", "InUse"), dataset("maintenance", "Maintenance"), dataset("broken", "Broken")] },
    options: {
      ...baseOptions,
      indexAxis: horizontal ? "y" : "x",
      scales: { x: { stacked: true, ticks: { precision: 0 } }, y: { stacked: true, ticks: { precision: 0 } } },
      plugins: {
        ...baseOptions.plugins,
        tooltip: { callbacks: { footer: (items) => `Tổng: ${fmtInt(rows[items[0].dataIndex].total)}` } },
      },
    },
  }, `${label}: ${rows.map((r) => `${r.name} ${r.total}`).join(", ")}`);
}

// ---------- Trạng thái từng widget ----------

// status "hidden": vai trò không có quyền xem -> gỡ hẳn thẻ. "error": báo riêng trong thẻ đó.
function applyWidgetState(name, widget) {
  document.querySelectorAll(`[data-widget="${name}"]`).forEach((card) => {
    const state = card.querySelector(".widget-state");
    const content = card.querySelectorAll(".chart-box, .table-responsive, #licenseBody, #budgetBody, .row.text-center, .widget-note");
    if (widget.status === "hidden") {
      card.remove();
      return;
    }
    if (widget.status === "error") {
      content.forEach((c) => c.classList.add("d-none"));
      state.innerHTML = `<div class="text-danger small mt-2"><i class="bi bi-exclamation-triangle"></i> ${escapeHtml(widget.message || "Không tải được số liệu này.")}</div>`;
      return;
    }
    content.forEach((c) => c.classList.remove("d-none"));
    state.innerHTML = "";
  });
}

function showEmpty(card, text) {
  const state = document.querySelector(`${card} .widget-state`);
  if (state) state.innerHTML = `<div class="text-muted small text-center py-4"><i class="bi bi-inbox"></i> ${escapeHtml(text)}</div>`;
}

// ---------- Hiển thị từng widget ----------

function renderOverview(w) {
  if (w.status !== "ok") {
    ["vTotal", "vInUse", "vMaint", "vBroken", "vTickets"].forEach((id) => { el(id).textContent = "–"; });
    return;
  }
  const d = w.data;
  el("vTotal").textContent = fmtInt(d.totalAssets);
  el("vInUse").textContent = fmtInt(d.inUse);
  el("vMaint").textContent = fmtInt(d.maintenance);
  el("vBroken").textContent = fmtInt(d.broken);
  el("vTickets").textContent = fmtInt(d.openTickets);
}

function renderAssetStatus(w) {
  if (w.status !== "ok") return clearChart("chStatus");
  const rows = w.data;
  if (rows.every((r) => r.count === 0)) {
    clearChart("chStatus");
    el("chStatus").closest(".chart-box").classList.add("d-none");
    showEmpty('[data-widget="assetStatus"]', "Chưa có tài sản nào.");
    return;
  }
  drawChart("chStatus", {
    type: "doughnut",
    data: {
      labels: rows.map((r) => `${r.label} (${fmtPercent(r.percent)})`),
      datasets: [{ data: rows.map((r) => r.count), backgroundColor: rows.map((r) => STATUS_COLORS[r.status] || COLOR_MUTED), borderWidth: 0 }],
    },
    options: { ...baseOptions, cutout: "62%" },
  }, `Tình trạng thiết bị: ${rows.map((r) => `${r.label} ${r.count}`).join(", ")}`);
}

function renderByCategory(w) {
  if (w.status !== "ok") return clearChart("chCategory");
  if (!w.data.length) {
    clearChart("chCategory");
    el("chCategory").closest(".chart-box").classList.add("d-none");
    return showEmpty('[data-widget="byCategory"]', "Chưa có tài sản nào.");
  }
  stackedStatusChart("chCategory", w.data, true, "Tình trạng theo loại");
}

function renderByDepartment(w, scope) {
  // Phạm vi một phòng ban (Manager hoặc Admin đã lọc): biểu đồ theo phòng ban chỉ có một cột nên không hiển thị.
  const card = el("deptWidget");
  if (card && scope.departmentId) {
    clearChart("chDepartment");
    card.classList.add("d-none");
    return;
  }
  if (card) card.classList.remove("d-none");
  if (w.status !== "ok") return clearChart("chDepartment");
  if (!w.data.length) {
    clearChart("chDepartment");
    el("chDepartment").closest(".chart-box").classList.add("d-none");
    return showEmpty('[data-widget="byDepartment"]', "Chưa có tài sản nào.");
  }
  stackedStatusChart("chDepartment", w.data, true, "Tình trạng theo phòng ban");
}

function renderFailure(w, scope) {
  if (w.status !== "ok") {
    clearChart("chFailure");
    el("topBody").innerHTML = "";
    return;
  }
  const d = w.data;
  const cats = [...d.byCategory].sort((a, b) => b.failureRatio - a.failureRatio || a.categoryName.localeCompare(b.categoryName, "vi"));
  el("failureNote").textContent =
    `Toàn bộ: ${fmtPercent(d.failureRatio)} (${fmtInt(d.assetsWithAtLeastOneTicket)} thiết bị từng có ít nhất 1 phiếu bảo trì).`;
  if (!cats.length) {
    clearChart("chFailure");
    el("chFailure").closest(".chart-box").classList.add("d-none");
  } else {
    el("chFailure").closest(".chart-box").classList.remove("d-none");
    drawChart("chFailure", {
      type: "bar",
      data: { labels: cats.map((c) => c.categoryName), datasets: [{ label: "Tỷ lệ từng lỗi (%)", data: cats.map((c) => Math.round(c.failureRatio * 1000) / 10), backgroundColor: COLOR_WARN }] },
      options: {
        ...baseOptions,
        indexAxis: "y",
        plugins: {
          legend: { display: false },
          tooltip: { callbacks: { label: (i) => `${i.formattedValue}% (${cats[i.dataIndex].assetsWithAtLeastOneTicket}/${cats[i.dataIndex].assetCount} thiết bị)` } },
        },
        scales: { x: { beginAtZero: true, max: 100, ticks: { callback: (v) => `${v}%` } } },
      },
    }, `Tỷ lệ lỗi theo loại: ${cats.map((c) => `${c.categoryName} ${fmtPercent(c.failureRatio)}`).join(", ")}`);
  }

  el("topBody").innerHTML = d.topFailingAssets.length
    ? d.topFailingAssets.map((a) => `<tr><td class="text-nowrap">${escapeHtml(a.assetCode)}</td><td>${escapeHtml(a.assetName)}</td><td>${escapeHtml(a.departmentName)}</td><td class="text-end">${fmtInt(a.ticketCount)}</td><td class="text-end">${fmtInt(a.failedTicketCount)}</td></tr>`).join("")
    : '<tr><td colspan="5" class="text-center text-muted py-3">Chưa có thiết bị nào phát sinh phiếu bảo trì.</td></tr>';
}

function renderReplacement(w) {
  if (w.status !== "ok") {
    el("vOverdue").textContent = "–";
    return;
  }
  el("vOverdue").textContent = fmtInt(w.data.overdueNowCount);
}

function setText(id, text) {
  const node = el(id);
  if (node) node.textContent = text;   // thẻ của widget bị ẩn đã được gỡ khỏi trang
}

function renderLicenses(w) {
  if (w.status !== "ok") {
    setText("vLicense", "–");
    return;
  }
  const d = w.data;
  el("vLicense").textContent = fmtInt(d.expiringSoon);
  el("licenseBody").innerHTML = `
    <div class="d-flex justify-content-between"><span>Tổng giấy phép</span><strong>${fmtInt(d.total)}</strong></div>
    <div class="d-flex justify-content-between"><span>Sắp hết hạn (${d.expiringDays} ngày)</span><strong class="${d.expiringSoon ? "text-warning-emphasis" : ""}">${fmtInt(d.expiringSoon)}</strong></div>
    <div class="d-flex justify-content-between"><span>Đã hết hạn</span><strong class="${d.expired ? "text-danger" : ""}">${fmtInt(d.expired)}</strong></div>
    <a class="small d-inline-block mt-2" href="pages/software-licenses.html">Quản lý giấy phép <i class="bi bi-arrow-right"></i></a>`;
}

function renderSummary(s) {
  currentSummary = { ...s, asOf: s.generatedAt };
  const names = ["overview", "assetStatus", "byCategory", "byDepartment", "failure", "replacement", "maintenance", "licenses"];
  names.forEach((n) => applyWidgetState(n, s[n]));

  // Thẻ số liệu của widget bị ẩn thì bỏ luôn (giấy phép/ngân sách với Manager).
  if (s.licenses.status === "hidden") document.querySelector('[data-stat="license"]')?.remove();

  renderOverview(s.overview);
  renderAssetStatus(s.assetStatus);
  renderByCategory(s.byCategory);
  renderByDepartment(s.byDepartment, s.scope);
  renderFailure(s.failure, s.scope);
  renderReplacement(s.replacement);
  renderLicenses(s.licenses);

  const when = fmtDateTime(s.generatedAt);
  el("generatedLabel").textContent = `Số liệu tính lúc ${when}${s.fromCache ? ` (bản lưu tạm ${s.cacheSeconds} giây)` : ""}`;
  if (s.scope.isLocked) {
    el("deptFilterBox").classList.add("d-none");
    el("scopeLockedBox").classList.remove("d-none");
    el("scopeLockedLabel").textContent = `Phòng ban: ${s.scope.departmentName}`;
  }
}

// ---------- Tải dữ liệu ----------

function currentQuery(refresh) {
  const params = new URLSearchParams();
  const dept = el("dashDepartment").value;
  if (!el("deptFilterBox").classList.contains("d-none") && dept) params.set("departmentId", dept);
  if (refresh) params.set("refresh", "true");
  const text = params.toString();
  return text ? `?${text}` : "";
}

async function loadDashboard(refresh = false) {
  const seq = ++loadSeq;   // đổi bộ lọc nhanh: chỉ phản hồi mới nhất được hiển thị
  const button = el("refreshBtn");
  button.disabled = true;
  button.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Đang tải';
  el("dashError").classList.add("d-none");
  try {
    const summary = await api.get(`/dashboard/summary${currentQuery(refresh)}`);
    if (seq !== loadSeq) return;
    renderSummary(summary);
  } catch (err) {
    if (seq !== loadSeq) return;
    el("dashErrorText").textContent = err.message || "Không tải được Dashboard.";
    el("dashError").classList.remove("d-none");
  } finally {
    if (seq === loadSeq) {
      button.disabled = false;
      button.innerHTML = '<i class="bi bi-arrow-clockwise"></i> Làm mới';
    }
  }
}

async function init() {
  const isAdmin = session.role === "Admin IT";
  if (isAdmin) {
    try {
      const departments = (await api.get("/departments")) || [];
      el("dashDepartment").insertAdjacentHTML("beforeend", departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join(""));
    } catch {
      el("deptFilterBox").classList.add("d-none");   // không tải được danh sách phòng ban: vẫn xem được toàn hệ thống
    }
  } else {
    el("deptFilterBox").classList.add("d-none");   // vai trò khác bị khoá ở phòng ban của mình (server ép)
  }

  el("dashDepartment").addEventListener("change", () => loadDashboard());
  el("refreshBtn").addEventListener("click", () => loadDashboard(true));
  el("retryBtn").addEventListener("click", () => loadDashboard(true));
  await loadDashboard();
}

if (session?.role === "Employee") {
  window.location.replace("pages/my-assets.html");   // nhân viên không dùng trang chủ quản trị
} else if (session) {
  renderNav({ active: "home", basePath: "" });

  // Ẩn các thẻ truy cập nhanh mà vai trò hiện tại không có quyền dùng (khớp phân quyền backend).
  document.querySelectorAll("[data-roles]").forEach((node) => {
    if (!node.dataset.roles.split(",").includes(session.role)) node.remove();
  });
  el("welcomeName").textContent = `Chào mừng trở lại, ${session.fullName} 👋`;

  if (can("dashboard.view")) {
    el("dashboard").classList.remove("d-none");
    init();
  } else {
    el("noDashboard").classList.remove("d-none");
  }
}
