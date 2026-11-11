const session = requireAuth("login.html");
renderNav({ active: "backup", basePath: "../" });

const KIND_LABEL = {
  Manual: ["slate", "Sao lưu thủ công"],
  PreRestore: ["warning", "Tự tạo trước khi phục hồi"],
  Uploaded: ["info", "Tải lên"],
};
let items = [];
let restoreModal;
let restoreTarget = null;

const $ = (id) => document.getElementById(id);

function show(id, text) {
  const el = $(id);
  el.textContent = text;
  el.classList.remove("d-none");
}

function clearAlerts() {
  $("errorAlert").classList.add("d-none");
  $("okAlert").classList.add("d-none");
}

function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

// Giờ Việt Nam không kèm múi giờ: hiển thị nguyên giá trị, không để trình duyệt đổi múi giờ.
function formatLocal(value) {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})/.exec(value || "");
  return m ? `${m[4]}:${m[5]}:${m[6]} ${m[3]}/${m[2]}/${m[1]}` : "-";
}

async function withBusy(button, busyText, task) {
  const original = button.innerHTML;
  button.disabled = true;
  button.innerHTML = `<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>${busyText}`;
  try {
    return await task();
  } finally {
    button.disabled = false;
    button.innerHTML = original;
  }
}

async function load() {
  try {
    items = await api.get("/backups");
    render();
  } catch (err) {
    show("errorAlert", err.message);
  }
}

function render() {
  $("totalLabel").textContent = `Tổng cộng: ${items.length} bản sao lưu`;
  $("backupBody").innerHTML = items.length === 0
    ? '<tr><td colspan="5"><div class="empty-state"><i class="bi bi-cloud-slash"></i><div class="title">Chưa có bản sao lưu nào</div>Bấm "Sao lưu ngay" để tạo bản đầu tiên.</div></td></tr>'
    : items.map((b, i) => {
      const [tone, label] = KIND_LABEL[b.kind] || ["slate", b.kind];
      return `<tr>
        <td class="fw-semibold"><i class="bi bi-file-earmark-zip text-muted"></i> ${escapeHtml(b.name)}</td>
        <td class="text-nowrap">${escapeHtml(formatLocal(b.createdAt))}</td>
        <td><span class="badge-soft ${tone}">${escapeHtml(label)}</span></td>
        <td class="text-end text-nowrap">${formatSize(b.sizeBytes)}</td>
        <td class="text-end text-nowrap">
          <button class="btn btn-sm btn-outline-primary" onclick="downloadBackup(${i})"><i class="bi bi-download"></i> Tải về</button>
          <button class="btn btn-sm btn-outline-danger" onclick="openRestore(${i})"><i class="bi bi-arrow-counterclockwise"></i> Phục hồi</button>
          <button class="btn btn-sm btn-outline-secondary" onclick="deleteBackup(${i})" aria-label="Xoá"><i class="bi bi-trash"></i></button>
        </td>
      </tr>`;
    }).join("");
}

$("createBtn").addEventListener("click", () => {
  clearAlerts();
  withBusy($("createBtn"), "Đang sao lưu...", async () => {
    try {
      const b = await api.post("/backups", {});
      show("okAlert", `Đã tạo bản sao lưu ${b.name} (${formatSize(b.sizeBytes)}).`);
      await load();
    } catch (err) {
      show("errorAlert", err.message);
    }
  });
});

async function downloadBackup(i) {
  clearAlerts();
  try {
    const { blob, filename } = await api.download(`/backups/${encodeURIComponent(items[i].name)}/download`);
    saveBlob(blob, filename === "bao-cao" ? items[i].name : filename);
  } catch (err) {
    show("errorAlert", err.message);
  }
}

async function deleteBackup(i) {
  clearAlerts();
  if (!confirm(`Xoá bản sao lưu ${items[i].name}? Thao tác này không hoàn tác được.`)) return;
  try {
    await api.del(`/backups/${encodeURIComponent(items[i].name)}`);
    show("okAlert", "Đã xoá bản sao lưu.");
    await load();
  } catch (err) {
    show("errorAlert", err.message);
  }
}

$("uploadBtn").addEventListener("click", () => $("uploadInput").click());
$("uploadInput").addEventListener("change", async () => {
  const file = $("uploadInput").files[0];
  $("uploadInput").value = "";
  if (!file) return;
  clearAlerts();
  if (!/\.sql$/i.test(file.name)) return show("errorAlert", "Chỉ nhận file .sql do chức năng sao lưu tạo ra.");
  await withBusy($("uploadBtn"), "Đang tải lên...", async () => {
    const form = new FormData();
    form.append("file", file);
    try {
      const res = await fetch(`${API_BASE_URL}/backups/upload`, { method: "POST", headers: { Authorization: `Bearer ${getToken() || ""}` }, body: form });
      const body = await res.json().catch(() => null);
      if (!res.ok || !body?.success) throw new Error(body?.message || `Tải lên thất bại (mã ${res.status}).`);
      show("okAlert", `Đã tải lên ${body.data.name}. Bấm "Phục hồi" nếu muốn dùng bản này.`);
      await load();
    } catch (err) {
      show("errorAlert", err.message);
    }
  });
});

function openRestore(i) {
  restoreTarget = items[i];
  $("restoreName").textContent = restoreTarget.name;
  $("restoreConfirm").value = "";
  $("restoreSubmit").disabled = true;
  $("restoreError").classList.add("d-none");
  restoreModal.show();
}

$("restoreConfirm").addEventListener("input", () => {
  $("restoreSubmit").disabled = $("restoreConfirm").value.trim().toUpperCase() !== "KHOI PHUC";
});

$("restoreForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  $("restoreError").classList.add("d-none");
  await withBusy($("restoreSubmit"), "Đang phục hồi...", async () => {
    try {
      const result = await api.post(`/backups/${encodeURIComponent(restoreTarget.name)}/restore`, { confirmText: $("restoreConfirm").value.trim() });
      restoreModal.hide();
      alert(`Đã phục hồi dữ liệu từ ${result.restoredFrom}.\nBản sao lưu dữ liệu trước đó: ${result.safetyBackup}.\n\nVui lòng đăng nhập lại.`);
      clearSession();
      window.location.href = "login.html";
    } catch (err) {
      if (err.status === 401) {
        // Phục hồi xong nhưng tài khoản hiện tại không còn trong dữ liệu đã phục hồi.
        window.location.href = "login.html";
        return;
      }
      $("restoreError").textContent = err.message;
      $("restoreError").classList.remove("d-none");
    }
  });
});

if (!can("backup.manage")) {
  show("forbiddenAlert", "Chỉ Admin IT được dùng chức năng sao lưu & phục hồi.");
  $("backupContent").classList.add("d-none");
  $("createBtn").classList.add("d-none");
  $("uploadBtn").classList.add("d-none");
} else {
  restoreModal = new bootstrap.Modal($("restoreModal"));
  load();
}
