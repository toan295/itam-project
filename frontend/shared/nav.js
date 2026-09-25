// Sidebar điều hướng dùng chung cho mọi trang. Gọi renderNav({ active, basePath }) sau khi requireAuth().
// basePath: "" khi gọi từ frontend/index.html, "../" khi gọi từ frontend/pages/*.html.
function renderNav({ active, basePath }) {
  const session = getSession();
  const el = document.getElementById("appNav");
  if (!el || !session) return;

  // roles: vai trò được thấy mục này (không khai báo = mọi vai trò). Khớp đúng phân quyền backend:
  // - Loại tài sản / Phòng ban / Phần mềm & Giấy phép / Người dùng: chỉ Admin IT (UC-03, UC-04, UC-09, UC-10).
  // - Phân bổ – thu hồi: Admin IT, Manager (UC-14, UC-15). - Bảo trì, Tài sản: mọi vai trò (đã giới hạn theo phòng ban ở API).
  const ADMIN = ["Admin IT"];
  const ADMIN_MANAGER = ["Admin IT", "Manager"];
  const allLinks = [
    { key: "home", label: "Trang chủ", icon: "bi-grid-1x2", href: `${basePath}index.html` },
    { key: "assets", label: "Tài sản", icon: "bi-laptop", href: `${basePath}pages/assets.html` },
    { key: "categories", label: "Loại tài sản", icon: "bi-tags", href: `${basePath}pages/asset-categories.html`, roles: ADMIN },
    { key: "departments", label: "Phòng ban", icon: "bi-building", href: `${basePath}pages/departments.html`, roles: ADMIN },
    { key: "licenses", label: "Phần mềm & Giấy phép", icon: "bi-key", href: `${basePath}pages/software-licenses.html`, roles: ADMIN },
    { key: "maintenance", label: "Bảo trì", icon: "bi-tools", href: `${basePath}pages/maintenance.html` },
    { key: "allocations", label: "Phân bổ", icon: "bi-box-arrow-up-right", href: `${basePath}pages/allocations.html`, roles: ADMIN_MANAGER },
    { key: "users", label: "Người dùng", icon: "bi-people", href: `${basePath}pages/users.html`, roles: ADMIN },
  ];
  const canSee = (l) => !l.roles || l.roles.includes(session.role);
  const links = allLinks.filter(canSee);

  // Vào thẳng URL của trang không có quyền (gõ tay/bookmark) -> về Trang chủ thay vì hiện trang lỗi 403.
  const activeLink = allLinks.find((l) => l.key === active);
  if (activeLink && !canSee(activeLink)) {
    window.location.href = allLinks[0].href;
    return;
  }

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
        <div class="nav-section-label">Điều hướng</div>
        ${links.map((l) => `
          <a class="nav-link ${l.key === active ? "active" : ""}" href="${l.href}">
            <i class="bi ${l.icon}"></i> ${escapeHtml(l.label)}
          </a>`).join("")}
      </nav>
      <div class="sidebar-user">
        <div class="user-row">
          <div class="avatar">${escapeHtml(initials || "?")}</div>
          <div class="user-meta">
            <div class="name">${escapeHtml(session.fullName)}</div>
            <div class="role">${escapeHtml(session.role)}</div>
          </div>
        </div>
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

  document.getElementById("navChangePasswordBtn").addEventListener("click", openChangePasswordModal);
  ensureChangePasswordModal();
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
              <div id="changePasswordError" class="alert alert-danger d-none py-2"></div>
              <div id="changePasswordSuccess" class="alert alert-success d-none py-2"></div>
              <div class="mb-2">
                <label class="form-label">Mật khẩu hiện tại</label>
                <input type="password" class="form-control" id="currentPasswordInput" required>
              </div>
              <div class="mb-2">
                <label class="form-label">Mật khẩu mới</label>
                <input type="password" class="form-control" id="newPasswordInput" required minlength="6" maxlength="100">
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
