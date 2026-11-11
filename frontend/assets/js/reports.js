const session = requireAuth("login.html");
renderNav({ active: "reports", basePath: "../" });

const $ = (id) => document.getElementById(id);

function showError(message) {
  $("okAlert").classList.add("d-none");
  $("errorAlert").textContent = message;
  $("errorAlert").classList.remove("d-none");
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function showOk(message) {
  $("errorAlert").classList.add("d-none");
  $("okAlert").textContent = message;
  $("okAlert").classList.remove("d-none");
}

function clearAlerts() {
  $("errorAlert").classList.add("d-none");
  $("okAlert").classList.add("d-none");
}

// Chỉ đưa tham số có giá trị lên URL (giá trị trống = không lọc).
function query(params) {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== "" && value !== null && value !== undefined) search.set(key, value);
  }
  const text = search.toString();
  return text ? `?${text}` : "";
}

// Kiểm tra số nguyên trong khoảng ngay ở trình duyệt để báo lỗi sớm (server vẫn kiểm tra lại).
function integerInRange(inputId, label, min, max) {
  const raw = $(inputId).value.trim();
  const value = Number(raw);
  if (raw === "" || !Number.isInteger(value) || value < min || value > max) {
    throw new Error(`${label} phải là số nguyên từ ${min} đến ${max}.`);
  }
  return value;
}

const REPORTS = {
  inventory: () => {
    const year = $("invYear").value.trim();
    if (year !== "" && !/^\d{4}$/.test(year)) throw new Error("Năm mua phải gồm 4 chữ số (vd 2024).");
    return {
      path: "/reports/assets/export",
      params: { categoryId: $("invCategory").value, status: $("invStatus").value, purchaseYear: year, keyword: $("invKeyword").value.trim() },
    };
  },
  maintenance: () => ({
    path: "/reports/maintenance-due/export",
    params: { categoryId: $("mtCategory").value, days: integerInRange("mtDays", "Số ngày bảo hành", 1, 365) },
  }),
  lifecycle: () => ({
    path: "/reports/short-lifecycle/export",
    params: { categoryId: $("lcCategory").value, months: integerInRange("lcMonths", "Số tháng", 1, 60) },
  }),
  licenses: () => ({ path: "/reports/licenses/export", params: { status: $("licState").value } }),
};

async function exportReport(button) {
  clearAlerts();
  let spec;
  try {
    spec = REPORTS[button.dataset.report]();
  } catch (err) {
    showError(err.message);
    return;
  }

  const params = { ...spec.params, format: $("format").value };
  if (!$("deptBox").classList.contains("d-none")) params.departmentId = $("department").value;   // Manager: server tự ép phòng ban

  const original = button.innerHTML;
  document.querySelectorAll(".export-btn").forEach((b) => { b.disabled = true; });   // tránh bấm đúp / xuất nhiều file cùng lúc
  button.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Đang tạo file...';
  try {
    const { blob, filename, rows } = await api.download(spec.path + query(params));
    saveBlob(blob, filename);
    showOk(`Đã tải "${filename}"${rows !== null ? ` (${rows.toLocaleString("vi-VN")} dòng)` : ""} — ${(blob.size / 1024).toLocaleString("vi-VN", { maximumFractionDigits: 0 })} KB.`);
  } catch (err) {
    showError(err.message);
  } finally {
    document.querySelectorAll(".export-btn").forEach((b) => { b.disabled = false; });
    button.innerHTML = original;
  }
}

async function init() {
  const isAdmin = session.role === "Admin IT";
  try {
    const [departments, categories] = await Promise.all([isAdmin ? api.get("/departments") : Promise.resolve([]), api.get("/asset-categories")]);
    $("department").insertAdjacentHTML("beforeend", (departments || []).map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join(""));
    const options = (categories || []).map((c) => `<option value="${c.id}">${escapeHtml(c.name)}</option>`).join("");
    document.querySelectorAll(".category-select").forEach((select) => select.insertAdjacentHTML("beforeend", options));
  } catch (err) {
    showError(`Không tải được danh sách bộ lọc: ${err.message}`);
  }

  if (isAdmin) {
    $("lockedBox").classList.add("d-none");
  } else {
    // Vai trò khác bị khoá ở phòng ban của mình (server ép); chỉ hiển thị để người dùng biết phạm vi file sẽ xuất.
    $("deptBox").classList.add("d-none");
    $("lockedBox").classList.remove("d-none");
    $("lockedLabel").textContent = "Phòng ban của bạn";
  }

  if (!can("licenses.manage")) $("licenseCard").remove();   // cần quyền Quản lý giấy phép (Manager mặc định không có)

  document.querySelectorAll(".export-btn").forEach((button) => button.addEventListener("click", () => exportReport(button)));
}

if (!can("reports.export")) {
  $("forbiddenAlert").textContent = "Vai trò của bạn chưa được cấp quyền xuất báo cáo.";
  $("forbiddenAlert").classList.remove("d-none");
  $("reportsContent").classList.add("d-none");
} else {
  init();
}
