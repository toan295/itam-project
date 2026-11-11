// Sidebar điều hướng dùng chung cho mọi trang. Gọi renderNav({ active, basePath }) sau khi requireAuth().
// basePath: "" khi gọi từ frontend/index.html, "../" khi gọi từ frontend/pages/*.html.
// Nhãn hiển thị tiếng Việt cho tên vai trò (tên lưu trong hệ thống giữ nguyên tiếng Anh).
const ROLE_LABELS = { Manager: "Quản lý", Technician: "Nhân viên kỹ thuật", Employee: "Nhân viên" };

// Admin có thể vừa đổi ma trận quyền trong lúc người dùng đang đăng nhập: hỏi lại server, nếu khác thì cập nhật phiên và tải lại một lần.
async function syncPermissions() {
  try {
    const me = await api.get("/auth/me");
    const fresh = [...(me.permissions || [])].sort().join(",");
    const current = [...(getSession()?.permissions || [])].sort().join(",");
    if (fresh !== current) {
      setSession({ ...getSession(), permissions: me.permissions || [] });
      window.location.reload();
    }
  } catch {
    // Mất mạng/hết hạn phiên: các request khác sẽ tự báo lỗi.
  }
}

function renderNav({ active, basePath }) {
  const session = getSession();
  const el = document.getElementById("appNav");
  if (!el || !session) return;

  // roles: vai trò được thấy mục này (không khai báo = mọi vai trò). Khớp đúng phân quyền backend:
  // - Loại tài sản / Phòng ban / Phần mềm & Giấy phép / Người dùng: chỉ Admin IT (UC-03, UC-04, UC-09, UC-10).
  // - Phân bổ – thu hồi: Admin IT, Manager (UC-14, UC-15). - Bảo trì, Tài sản: mọi vai trò (đã giới hạn theo phòng ban ở API).
  // perm: mục hiện khi vai trò của người dùng đang được cấp quyền đó trong ma trận phân quyền.
  // Nhân viên (vai trò Employee) chỉ dùng cổng riêng "Tài sản của tôi" (backend chặn mọi API khác).
  const STAFF = ["Admin IT", "Manager", "Technician"];
  const EMPLOYEE = ["Employee"];
  const allLinks = [
    // roles: vai trò thấy mục; perm: quyền cần có (ưu tiên perm nếu có). group: nhóm hiển thị trong menu (theo thứ tự xuất hiện).
    { key: "myAssets", group: "Cá nhân", label: "Tài sản của tôi", icon: "bi-laptop", href: `${basePath}pages/my-assets.html`, roles: EMPLOYEE },

    { key: "home", group: "Tổng quan", label: "Trang chủ", icon: "bi-grid-1x2", href: `${basePath}index.html`, roles: STAFF },

    { key: "assets", group: "Quản lý tài sản", label: "Tài sản", icon: "bi-laptop", href: `${basePath}pages/assets.html`, roles: STAFF },
    { key: "allocations", group: "Quản lý tài sản", label: "Phân bổ", icon: "bi-box-arrow-up-right", href: `${basePath}pages/allocations.html`, perm: "allocations.manage" },
    { key: "maintenance", group: "Quản lý tài sản", label: "Bảo trì", icon: "bi-tools", href: `${basePath}pages/maintenance.html`, roles: STAFF },
    { key: "disposals", group: "Quản lý tài sản", label: "Thanh lý", icon: "bi-archive", href: `${basePath}pages/disposals.html`, roles: STAFF },
    { key: "licenses", group: "Quản lý tài sản", label: "Phần mềm & Giấy phép", icon: "bi-key", href: `${basePath}pages/software-licenses.html`, perm: "licenses.manage" },

    { key: "lifecycle", group: "Phân tích & Báo cáo", label: "Vòng đời tài sản", icon: "bi-hourglass-split", href: `${basePath}pages/lifecycle.html`, perm: "lifecycle.view" },
    { key: "forecasts", group: "Phân tích & Báo cáo", label: "Dự báo ngân sách", icon: "bi-graph-up-arrow", href: `${basePath}pages/forecasts.html`, perm: "forecasts.manage" },
    { key: "reports", group: "Phân tích & Báo cáo", label: "Báo cáo", icon: "bi-file-earmark-arrow-down", href: `${basePath}pages/reports.html`, perm: "reports.export" },

    { key: "categories", group: "Danh mục", label: "Loại tài sản", icon: "bi-tags", href: `${basePath}pages/asset-categories.html`, perm: "asset-categories.manage" },
    { key: "departments", group: "Danh mục", label: "Phòng ban", icon: "bi-building", href: `${basePath}pages/departments.html`, perm: "departments.manage" },
    { key: "employees", group: "Danh mục", label: "Nhân viên", icon: "bi-person-badge", href: `${basePath}pages/employees.html`, perm: "employees.manage" },

    { key: "users", group: "Hệ thống", label: "Người dùng", icon: "bi-people", href: `${basePath}pages/users.html`, perm: "users.manage" },
    { key: "permissions", group: "Hệ thống", label: "Phân quyền", icon: "bi-shield-lock", href: `${basePath}pages/permissions.html`, perm: "permissions.manage" },
    { key: "auditLogs", group: "Hệ thống", label: "Nhật ký hệ thống", icon: "bi-journal-text", href: `${basePath}pages/audit-logs.html`, perm: "audit-logs.view" },
    { key: "backup", group: "Hệ thống", label: "Sao lưu & phục hồi", icon: "bi-cloud-arrow-down", href: `${basePath}pages/backup.html`, perm: "backup.manage" },
  ];
  const canSee = (l) => (l.perm ? can(l.perm) : !l.roles || l.roles.includes(session.role));
  const links = allLinks.filter(canSee);

  // Vào thẳng URL của trang không có quyền (gõ tay/bookmark) -> về Trang chủ thay vì hiện trang lỗi 403.
  const activeLink = allLinks.find((l) => l.key === active);
  if (activeLink && !canSee(activeLink)) {
    window.location.href = links[0]?.href || allLinks[0].href;
    return;
  }

  syncPermissions();

  const initials = (session.fullName || "?")
    .split(" ")
    .filter(Boolean)
    .slice(-2)
    .map((w) => w[0].toUpperCase())
    .join("");

  el.outerHTML = `
    <div class="mobile-topbar">
      <button class="btn btn-sm btn-outline-secondary" id="sidebarToggleBtn"><i class="bi bi-list"></i></button>
      <span class="fw-bold">EAIMS</span>
    </div>
    <div class="sidebar-backdrop" id="sidebarBackdrop"></div>
    <aside class="eaims-sidebar" id="appNav">
      <div class="brand">
        <span class="logo-dot">🖧</span> EAIMS
      </div>
      <nav>
        ${links.map((l, i) => `${i === 0 || links[i - 1].group !== l.group ? `<div class="nav-section-label">${escapeHtml(l.group)}</div>` : ""}
          <a class="nav-link ${l.key === active ? "active" : ""}" href="${l.href}">
            <i class="bi ${l.icon}"></i> ${escapeHtml(l.label)}
          </a>`).join("")}
      </nav>
      <div class="sidebar-user">
        <div class="user-row">
          <div class="avatar">${escapeHtml(initials || "?")}</div>
          <div class="user-meta">
            <div class="name">${escapeHtml(session.fullName)}</div>
            <div class="role">${escapeHtml(ROLE_LABELS[session.role] || session.role)}</div>
          </div>
        </div>
        <button class="btn btn-outline-light btn-sm w-100 mb-2" id="navProfileBtn">
          <i class="bi bi-person-circle"></i> Thông tin cá nhân
        </button>
        <button class="btn btn-outline-light btn-sm w-100 mb-2" id="navChangePasswordBtn">
          <i class="bi bi-key"></i> Đổi mật khẩu
        </button>
        <button class="btn btn-outline-light btn-sm w-100" id="navLogoutBtn">
          <i class="bi bi-box-arrow-right"></i> Đăng xuất
        </button>
      </div>
    </aside>`;

  document.getElementById("navLogoutBtn").addEventListener("click", () => logout(`${basePath}pages/login.html`));

  const sidebar = document.getElementById("appNav");
  const backdrop = document.getElementById("sidebarBackdrop");
  const toggleBtn = document.getElementById("sidebarToggleBtn");
  const openSidebar = () => { sidebar.classList.add("open"); backdrop.classList.add("open"); };
  const closeSidebar = () => { sidebar.classList.remove("open"); backdrop.classList.remove("open"); };
  toggleBtn.addEventListener("click", () => sidebar.classList.contains("open") ? closeSidebar() : openSidebar());
  backdrop.addEventListener("click", closeSidebar);

  document.getElementById("navProfileBtn").addEventListener("click", openProfileModal);
  document.getElementById("navChangePasswordBtn").addEventListener("click", openChangePasswordModal);
  ensureChangePasswordModal();

  // Tài khoản đang dùng mật khẩu khởi tạo (Admin IT đầu tiên, tạo từ cấu hình) phải đổi mật khẩu trước khi dùng tiếp.
  if (session.mustChangePassword) {
    forcePasswordChange();
  }
  syncMustChangePassword(session);
}

// Admin có thể đặt lại mật khẩu khi người dùng đang đăng nhập — đồng bộ cờ từ server thay vì tin session cũ.
async function syncMustChangePassword(session) {
  try {
    const profile = await api.get("/auth/me");
    const must = !!profile.mustChangePassword;
    if (must !== !!session.mustChangePassword) {
      setSession({ ...session, mustChangePassword: must });
      if (must) forcePasswordChange();
    }
    // Quyền của vai trò có thể vừa được đổi/bổ sung (vd tính năng mới): đồng bộ rồi tải lại một lần để menu đúng.
    const fresh = [...(profile.permissions || [])].sort().join(",");
    const stored = [...(getSession()?.permissions || [])].sort().join(",");
    if (fresh !== stored && !sessionStorage.getItem("eaims_perm_reload")) {
      setSession({ ...getSession(), permissions: profile.permissions || [] });
      sessionStorage.setItem("eaims_perm_reload", "1");
      window.location.reload();
    } else {
      sessionStorage.removeItem("eaims_perm_reload");
    }
  } catch {
    // Lỗi mạng/phiên hết hạn đã được xử lý ở nơi khác; không chặn trang vì việc đồng bộ này.
  }
}

function forcePasswordChange() {
  const modalEl = document.getElementById("changePasswordModal");
  modalEl.dataset.forced = "true";
  modalEl.querySelector(".btn-close").classList.add("d-none");
  modalEl.querySelector('.modal-footer [data-bs-dismiss="modal"]').classList.add("d-none");
  modalEl.querySelector(".modal-title").textContent = "Bạn cần đổi mật khẩu";
  const notice = document.getElementById("changePasswordNotice");
  notice.classList.remove("d-none");
  bootstrap.Modal.getOrCreateInstance(modalEl, { backdrop: "static", keyboard: false }).show();
}

// Modal "Đổi mật khẩu" (khi đang đăng nhập, còn nhớ mật khẩu hiện tại) — chèn 1 lần vào body,
// dùng chung cho mọi trang thay vì lặp lại markup trong từng file HTML.
function ensureChangePasswordModal() {
  if (document.getElementById("changePasswordModal")) return;

  const wrapper = document.createElement("div");
  wrapper.innerHTML = `
    <div class="modal fade" id="changePasswordModal" tabindex="-1">
      <div class="modal-dialog">
        <div class="modal-content">
          <form id="changePasswordForm">
            <div class="modal-header">
              <h5 class="modal-title">Đổi mật khẩu</h5>
              <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
              <div id="changePasswordNotice" class="alert alert-warning d-none py-2">
                Tài khoản của bạn đang dùng mật khẩu khởi tạo. Hãy đặt mật khẩu mới để tiếp tục.
              </div>
              <div id="changePasswordError" class="alert alert-danger d-none py-2"></div>
              <div id="changePasswordSuccess" class="alert alert-success d-none py-2"></div>
              <div class="mb-2">
                <label class="form-label">Mật khẩu hiện tại</label>
                <input type="password" class="form-control" id="currentPasswordInput" required>
              </div>
              <div class="mb-2">
                <label class="form-label">Mật khẩu mới</label>
                <input type="password" class="form-control" id="newPasswordInput" required minlength="8" maxlength="72" autocomplete="new-password">
                <div class="form-text">Ít nhất 8 ký tự, gồm cả chữ và số.</div>
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Huỷ</button>
              <button type="submit" class="btn btn-primary">Đổi mật khẩu</button>
            </div>
          </form>
        </div>
      </div>
    </div>`;
  document.body.appendChild(wrapper.firstElementChild);

  document.getElementById("changePasswordForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const errEl = document.getElementById("changePasswordError");
    const okEl = document.getElementById("changePasswordSuccess");
    errEl.classList.add("d-none");
    okEl.classList.add("d-none");

    try {
      await api.post("/auth/change-password", {
        currentPassword: document.getElementById("currentPasswordInput").value,
        newPassword: document.getElementById("newPasswordInput").value,
      });
      okEl.textContent = "Đổi mật khẩu thành công.";
      okEl.classList.remove("d-none");
      document.getElementById("changePasswordForm").reset();

      const current = getSession();
      if (current?.mustChangePassword) {
        setSession({ ...current, mustChangePassword: false });
        setTimeout(() => window.location.reload(), 800); // tải lại để trang nạp dữ liệu bình thường.
      }
    } catch (err) {
      errEl.textContent = err.message;
      errEl.classList.remove("d-none");
    }
  });
}

function openChangePasswordModal() {
  document.getElementById("changePasswordForm").reset();
  document.getElementById("changePasswordError").classList.add("d-none");
  document.getElementById("changePasswordSuccess").classList.add("d-none");
  bootstrap.Modal.getOrCreateInstance(document.getElementById("changePasswordModal")).show();
}

// ===== Thông tin cá nhân (mọi tài khoản): tự sửa họ tên và số điện thoại =====
function ensureProfileModal() {
  if (document.getElementById("profileModal")) return;
  const wrapper = document.createElement("div");
  wrapper.innerHTML = `
    <div class="modal fade" id="profileModal" tabindex="-1">
      <div class="modal-dialog">
        <div class="modal-content">
          <form id="profileForm">
            <div class="modal-header">
              <h5 class="modal-title">Thông tin cá nhân</h5>
              <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
              <div id="profileError" class="alert alert-danger d-none py-2"></div>
              <div id="profileSuccess" class="alert alert-success d-none py-2"></div>
              <div class="mb-2">
                <label class="form-label">Họ tên</label>
                <input class="form-control" id="profileFullName" required maxlength="100" autocomplete="off">
              </div>
              <div class="mb-2">
                <label class="form-label">Số điện thoại</label>
                <input class="form-control" id="profilePhone" maxlength="20" placeholder="VD: 0912345678" autocomplete="off">
              </div>
              <div class="row small text-muted">
                <div class="col-sm-6 mb-1"><i class="bi bi-envelope"></i> <span id="profileEmail"></span></div>
                <div class="col-sm-6 mb-1"><i class="bi bi-building"></i> <span id="profileDepartment"></span></div>
              </div>
              <div class="form-text">Email, phòng ban và vai trò do Admin IT quản lý.</div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Huỷ</button>
              <button type="submit" class="btn btn-primary">Lưu</button>
            </div>
          </form>
        </div>
      </div>
    </div>`;
  document.body.appendChild(wrapper.firstElementChild);

  document.getElementById("profileForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const errEl = document.getElementById("profileError");
    const okEl = document.getElementById("profileSuccess");
    errEl.classList.add("d-none");
    okEl.classList.add("d-none");
    try {
      const updated = await api.put("/auth/me", {
        fullName: document.getElementById("profileFullName").value.trim(),
        phone: document.getElementById("profilePhone").value.trim(),
      });
      const current = getSession();
      if (current) setSession({ ...current, fullName: updated.fullName });
      okEl.textContent = "Đã lưu thông tin cá nhân.";
      okEl.classList.remove("d-none");
      document.querySelectorAll(".sidebar-user .name").forEach((el) => { el.textContent = updated.fullName; });
    } catch (err) {
      errEl.textContent = err.message;
      errEl.classList.remove("d-none");
    }
  });
}

async function openProfileModal() {
  ensureProfileModal();
  document.getElementById("profileError").classList.add("d-none");
  document.getElementById("profileSuccess").classList.add("d-none");
  const modal = bootstrap.Modal.getOrCreateInstance(document.getElementById("profileModal"));
  try {
    const me = await api.get("/auth/me");
    document.getElementById("profileFullName").value = me.fullName;
    document.getElementById("profilePhone").value = me.phone || "";
    document.getElementById("profileEmail").textContent = me.email;
    document.getElementById("profileDepartment").textContent = me.departmentName || "—";
  } catch (err) {
    document.getElementById("profileError").textContent = err.message;
    document.getElementById("profileError").classList.remove("d-none");
  }
  modal.show();
}
