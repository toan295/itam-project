const session = requireAuth("login.html");
renderNav({ active: "permissions", basePath: "../" });

const ROLE_INFO = {
  "Admin IT": { label: "Admin IT", sub: "Quản trị hệ thống" },
  Manager: { label: "Quản lý", sub: "Quản lý phòng ban" },
  Technician: { label: "Nhân viên kỹ thuật", sub: "Xử lý phiếu bảo trì" },
  Employee: { label: "Nhân viên", sub: "Người dùng được cấp tài sản" },
};

const DRAFT_KEY = `eaims_perm_draft_${session.userId}`;

let matrix = null;      // dữ liệu máy chủ đã xác nhận (kể cả revision của từng vai trò)
let draft = {};         // roleId -> Set mã quyền đang chọn (chưa lưu)
let saved = {};         // roleId -> Set mã quyền máy chủ đã xác nhận
let revision = {};      // roleId -> dấu phiên bản máy chủ lúc tải, gửi lại khi lưu để phát hiện người khác vừa sửa
let lastApplied = null; // [{ roleId, name, before }] của lần lưu/khôi phục gần nhất, để hoàn tác
let busy = false;

function showError(message) {
  const el = document.getElementById("errorAlert");
  el.textContent = message;
  el.classList.remove("d-none");
  document.getElementById("infoAlert").classList.add("d-none");
}

function showInfo(message, { undo = false } = {}) {
  const el = document.getElementById("infoAlert");
  el.textContent = message;
  if (undo && lastApplied) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "btn btn-sm btn-outline-success ms-3";
    btn.innerHTML = '<i class="bi bi-arrow-counterclockwise"></i> Hoàn tác';
    btn.addEventListener("click", undoLast);
    el.appendChild(btn);
  }
  el.classList.remove("d-none");
  document.getElementById("errorAlert").classList.add("d-none");
}

function clearAlerts() {
  document.getElementById("errorAlert").classList.add("d-none");
  document.getElementById("infoAlert").classList.add("d-none");
}

const sameSet = (a, b) => a.size === b.size && [...a].every((x) => b.has(x));
const labelOf = (code) => matrix.groups.flatMap((g) => g.items).find((p) => p.code === code)?.label || code;
const roleLabel = (role) => (ROLE_INFO[role.name] || {}).label || role.name;

function dirtyRoles() {
  return matrix.roles.filter((r) => !r.isLocked && !sameSet(draft[r.id], saved[r.id]));
}

// Giữ bản nháp qua lần tải lại trang: lưu theo từng vai trò kèm revision nền để biết bản nháp dựa trên dữ liệu nào.
function persistDraft() {
  try {
    const out = {};
    for (const role of dirtyRoles()) out[role.id] = { base: revision[role.id], baseCodes: [...saved[role.id]], codes: [...draft[role.id]] };
    if (Object.keys(out).length) sessionStorage.setItem(DRAFT_KEY, JSON.stringify(out));
    else sessionStorage.removeItem(DRAFT_KEY);
  } catch { /* trình duyệt chặn storage: bỏ qua, vẫn còn cảnh báo beforeunload */ }
}

function readStoredDraft() {
  try {
    return JSON.parse(sessionStorage.getItem(DRAFT_KEY) || "{}");
  } catch {
    return {};
  }
}

// Nạp ma trận máy chủ vừa xác nhận. `preserve`: roleId -> { base, codes } là bản nháp muốn giữ lại (nếu khác dữ liệu máy chủ).
// Trả về danh sách vai trò mà bản nháp dựa trên dữ liệu cũ (người khác đã sửa trong lúc đó).
function applyMatrix(fresh, preserve = {}) {
  matrix = fresh;
  saved = {};
  draft = {};
  revision = {};
  const stale = [];
  for (const role of matrix.roles) {
    saved[role.id] = new Set(role.permissions);
    revision[role.id] = role.revision;
    const keep = !role.isLocked && preserve[role.id];
    let wanted = keep ? new Set(keep.codes) : null;
    // Người khác đã đổi vai trò này sau khi bản nháp được tạo: áp phần thay đổi của bạn (cấp/thu hồi) lên bản mới nhất,
    // để không vô tình hoàn lại thay đổi của người kia.
    if (wanted && keep.base !== role.revision && Array.isArray(keep.baseCodes)) {
      const base = new Set(keep.baseCodes);
      const merged = new Set(role.permissions);
      for (const c of wanted) if (!base.has(c)) merged.add(c);
      for (const c of base) if (!wanted.has(c)) merged.delete(c);
      wanted = merged;
    }
    if (wanted && !sameSet(wanted, saved[role.id])) {
      draft[role.id] = wanted;
      if (keep.base !== role.revision) stale.push(role);
    } else {
      draft[role.id] = new Set(role.permissions);
    }
  }
  render();
  persistDraft();
  return stale;
}

function currentDrafts() {
  const out = {};
  for (const role of matrix.roles) {
    if (!role.isLocked && !sameSet(draft[role.id], saved[role.id])) out[role.id] = { base: revision[role.id], baseCodes: [...saved[role.id]], codes: [...draft[role.id]] };
  }
  return out;
}

function staleNotice(stale) {
  return `Quyền của ${stale.map(roleLabel).join(", ")} vừa được người khác thay đổi. Thay đổi bạn chưa lưu vẫn được giữ và đã áp lên bản mới nhất (các ô đổi màu) — hãy kiểm tra lại rồi bấm Lưu.`;
}

async function load({ keepDrafts = false, restoreStored = false } = {}) {
  try {
    const preserve = keepDrafts ? currentDrafts() : restoreStored ? readStoredDraft() : {};
    const stale = applyMatrix(await api.get("/permissions/matrix"), preserve);
    if (restoreStored && dirtyRoles().length) {
      const note = "Đã khôi phục bản nháp chưa lưu từ lần mở trước.";
      if (stale.length) showError(`${note} ${staleNotice(stale)}`);
      else showInfo(note);
    } else if (keepDrafts && stale.length) {
      showError(staleNotice(stale));
    }
  } catch (err) {
    showError(err.message);
  }
}

function roleHeader(role) {
  const info = ROLE_INFO[role.name] || { label: role.name, sub: "" };
  const reset = role.isLocked
    ? `<div class="small text-muted mt-1"><i class="bi bi-lock-fill"></i> Luôn đủ quyền</div>`
    : `<button class="btn btn-link btn-sm p-0 mt-1" data-reset="${role.id}" title="Đưa quyền của vai trò này về mặc định của hệ thống">Khôi phục mặc định</button>`;
  return `<th class="role-col ${role.isLocked ? "locked" : ""}">
    <div class="fw-bold text-body" style="text-transform:none;font-size:.9rem">${escapeHtml(info.label)}</div>
    <div class="fw-normal" style="text-transform:none">${escapeHtml(info.sub)}</div>
    <div class="fw-normal" style="text-transform:none"><i class="bi bi-people"></i> ${role.userCount} người</div>
    ${reset}
  </th>`;
}

function cell(role, perm) {
  const isAdmin = role.name === "Admin IT";
  const checked = draft[role.id].has(perm.code);
  const changed = checked !== saved[role.id].has(perm.code);
  const offDefault = checked !== role.defaultPermissions.includes(perm.code);

  // Quyền hệ thống: Admin IT luôn bật (không gỡ được); vai trò khác không bao giờ có.
  if (perm.systemOnly) {
    return isAdmin
      ? `<td title="Quyền hệ thống, luôn bật cho Admin IT"><i class="bi bi-lock-fill text-success"></i></td>`
      : `<td title="Quyền hệ thống chỉ dành cho Admin IT"><i class="bi bi-dash text-muted"></i></td>`;
  }

  const disabled = role.isLocked || busy ? "disabled" : "";
  return `<td class="${role.isLocked ? "role-col locked" : ""}">
    <span class="perm-cell ${changed ? "changed" : ""} ${!role.isLocked && offDefault ? "off-default" : ""}">
      <input type="checkbox" class="form-check-input" ${checked ? "checked" : ""} ${disabled}
             data-role="${role.id}" data-perm="${escapeHtml(perm.code)}"
             aria-label="${escapeHtml(perm.label)} - ${escapeHtml(role.name)}">
    </span></td>`;
}

function render() {
  document.getElementById("matrixHead").innerHTML =
    `<tr><th>Chức năng</th>${matrix.roles.map(roleHeader).join("")}</tr>`;

  document.getElementById("matrixBody").innerHTML = matrix.groups.map((group) => `
    <tr class="group-row"><td colspan="${matrix.roles.length + 1}">${escapeHtml(group.name)}</td></tr>
    ${group.items.map((perm) => `
      <tr>
        <td><div class="fw-semibold">${escapeHtml(perm.label)}</div><div class="perm-desc">${escapeHtml(perm.description)}</div></td>
        ${matrix.roles.map((role) => cell(role, perm)).join("")}
      </tr>`).join("")}
  `).join("");

  updateSaveBar();
}

function updateSaveBar() {
  const dirty = dirtyRoles();
  document.getElementById("saveBar").classList.toggle("d-none", dirty.length === 0);
  document.getElementById("dirtySummary").innerHTML = dirty.length
    ? `<i class="bi bi-pencil-square text-warning"></i> Chưa lưu thay đổi của: <strong>${dirty.map((r) => escapeHtml(roleLabel(r))).join(", ")}</strong>`
    : "";
  document.getElementById("saveBtn").disabled = busy;
  document.getElementById("discardBtn").disabled = busy;
}

function diffOf(role, fromSet, toSet) {
  return {
    added: [...toSet].filter((c) => !fromSet.has(c)).map(labelOf),
    removed: [...fromSet].filter((c) => !toSet.has(c)).map(labelOf),
  };
}

function describeChanges(role, fromSet, toSet) {
  const { added, removed } = diffOf(role, fromSet, toSet);
  return `${roleLabel(role)} (${role.userCount} người):` +
    (added.length ? `\n  + Cấp: ${added.join(", ")}` : "") +
    (removed.length ? `\n  − Thu hồi: ${removed.join(", ")}` : "");
}

function setBusy(value) {
  busy = value;
  render();
}

document.getElementById("matrixBody").addEventListener("change", (e) => {
  const box = e.target.closest("input[type=checkbox]");
  if (!box || busy) return;
  const set = draft[Number(box.dataset.role)];
  if (box.checked) set.add(box.dataset.perm); else set.delete(box.dataset.perm);
  persistDraft();
  render();
});

// Khôi phục mặc định = thao tác riêng, áp dụng NGAY sau khi xác nhận (ghi nhật ký riêng "ResetPermissions").
// Hộp thoại nêu rõ phần sẽ cấp/thu hồi so với cấu hình hiện tại; bản nháp chưa lưu của chính vai trò này sẽ bị bỏ, của vai trò khác giữ nguyên.
document.getElementById("matrixHead").addEventListener("click", async (e) => {
  const btn = e.target.closest("[data-reset]");
  if (!btn || busy) return;
  const role = matrix.roles.find((r) => r.id === Number(btn.dataset.reset));
  const defaults = new Set(role.defaultPermissions);
  const { added, removed } = diffOf(role, saved[role.id], defaults);
  if (!added.length && !removed.length) {
    showInfo(`${roleLabel(role)} đã đúng quyền mặc định, không có gì để khôi phục.`);
    return;
  }

  const hasDraft = !sameSet(draft[role.id], saved[role.id]);
  const message =
    `Khôi phục quyền mặc định cho ${roleLabel(role)}? ${role.userCount} người dùng vai trò này bị ảnh hưởng NGAY.\n\n` +
    describeChanges(role, saved[role.id], defaults) +
    (hasDraft ? "\n\nLưu ý: thay đổi chưa lưu của vai trò này sẽ bị bỏ." : "");
  if (!confirm(message)) return;

  setBusy(true);
  try {
    const before = [...saved[role.id]];
    const preserve = currentDrafts();
    delete preserve[role.id];
    const res = await api.post(`/permissions/roles/${role.id}/reset`, { expectedRevision: revision[role.id] });
    lastApplied = [{ roleId: role.id, name: role.name, before }];
    const fresh = { ...matrix, roles: matrix.roles.map((r) => (r.id === res.id ? res : r)) };
    busy = false;
    applyMatrix(fresh, preserve);
    showInfo(`Đã khôi phục quyền mặc định cho ${roleLabel(role)}.`, { undo: true });
  } catch (err) {
    busy = false;
    showError(err.message);
    if (err.status === 409) await load({ keepDrafts: true });
    else render();
  }
});

document.getElementById("discardBtn").addEventListener("click", () => {
  if (busy) return;
  for (const role of matrix.roles) draft[role.id] = new Set(saved[role.id]);
  clearAlerts();
  persistDraft();
  render();
});

// Lưu CẢ ma trận trong một yêu cầu (một transaction ở máy chủ): hoặc mọi vai trò cùng đổi, hoặc không vai trò nào đổi.
// Chỉ báo thành công và cập nhật các ô sau khi máy chủ xác nhận; thất bại thì giữ nguyên bản nháp.
document.getElementById("saveBtn").addEventListener("click", async () => {
  const dirty = dirtyRoles();
  if (busy || dirty.length === 0) return;
  if (!confirm(`Lưu thay đổi phân quyền? Có hiệu lực ngay với người dùng đang đăng nhập.\n\n${dirty.map((r) => describeChanges(r, saved[r.id], draft[r.id])).join("\n\n")}`)) return;

  clearAlerts();
  setBusy(true);
  try {
    const before = dirty.map((r) => ({ roleId: r.id, name: r.name, before: [...saved[r.id]] }));
    const fresh = await api.put("/permissions/matrix", {
      roles: dirty.map((r) => ({ roleId: r.id, permissions: [...draft[r.id]], expectedRevision: revision[r.id] })),
    });
    lastApplied = before;
    busy = false;
    applyMatrix(fresh);
    showInfo("Đã lưu phân quyền.", { undo: true });
  } catch (err) {
    busy = false;
    render();
    if (!err.status) {
      showError("Không nhận được phản hồi từ máy chủ nên CHƯA biết thay đổi đã được lưu hay chưa. Bản nháp của bạn vẫn còn; hãy tải lại ma trận để kiểm tra rồi lưu lại nếu cần.");
    } else if (err.status === 409) {
      showError(`Chưa lưu gì cả. ${err.message}`);
      await load({ keepDrafts: true });
    } else {
      showError(`Chưa lưu gì cả (không vai trò nào bị đổi). ${err.message}`);
    }
  }
});

// Hoàn tác lần lưu/khôi phục gần nhất của phiên làm việc này (đưa các vai trò về đúng tập quyền trước đó).
async function undoLast() {
  if (busy || !lastApplied) return;
  const targets = lastApplied;
  const names = targets.map((t) => (ROLE_INFO[t.name] || {}).label || t.name).join(", ");
  if (!confirm(`Hoàn tác lần thay đổi gần nhất của: ${names}? Quyền sẽ trở về đúng như trước lần lưu đó, có hiệu lực ngay.`)) return;

  setBusy(true);
  try {
    const preserve = currentDrafts();
    for (const t of targets) delete preserve[t.roleId];
    const fresh = await api.put("/permissions/matrix", {
      roles: targets.map((t) => ({ roleId: t.roleId, permissions: t.before, expectedRevision: revision[t.roleId] })),
    });
    lastApplied = null;
    busy = false;
    applyMatrix(fresh, preserve);
    showInfo(`Đã hoàn tác thay đổi của ${names}.`);
  } catch (err) {
    busy = false;
    render();
    showError(`Không hoàn tác được. ${err.message}`);
    if (err.status === 409) await load({ keepDrafts: true });
  }
}

// Tránh mất bản nháp khi lỡ rời trang; bản nháp cũng được lưu tạm để khôi phục sau khi tải lại.
window.addEventListener("beforeunload", (e) => {
  if (matrix && dirtyRoles().length) e.preventDefault();
});

if (!can("permissions.manage")) {
  document.querySelector("main").innerHTML = `<div class="alert alert-warning">Chỉ Admin IT được truy cập trang Phân quyền.</div>`;
} else {
  load({ restoreStored: true });
}
