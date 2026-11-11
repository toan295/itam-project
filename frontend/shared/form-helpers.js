// Tiện ích form dùng chung: ô ngày dd/mm/yyyy, cảnh báo mất thay đổi khi đóng modal, chống bấm gửi hai lần.

// ===== Ngày kiểu Việt Nam (dd/mm/yyyy) =====
// <input type="date"> hiển thị theo ngôn ngữ của trình duyệt (thường mm/dd/yyyy) và không đổi được bằng code.
// Ô gắn data-date-vi là ô chữ hiển thị dd/mm/yyyy, chọn bằng lịch; giá trị gửi lên server luôn là ISO (yyyy-mm-dd).
const DateVi = {
  MIN_YEAR: 1900,

  toDisplay(iso) {
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso || "");
    return m ? `${m[3]}/${m[2]}/${m[1]}` : "";
  },

  // "" nếu ô trống; null nếu nhập sai (sai khuôn, ngày không tồn tại như 31/02); ngược lại chuỗi ISO.
  toIso(text) {
    const value = (text || "").trim();
    if (value === "") return "";
    const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(value);
    if (!m) return null;
    const [d, mo, y] = [Number(m[1]), Number(m[2]), Number(m[3])];
    if (y < DateVi.MIN_YEAR || y > 9999) return null;
    const date = new Date(Date.UTC(y, mo - 1, d));
    if (date.getUTCFullYear() !== y || date.getUTCMonth() !== mo - 1 || date.getUTCDate() !== d) return null;
    return `${String(y).padStart(4, "0")}-${String(mo).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
  },

  // Giá trị ISO của ô: "" khi trống hoặc đang nhập dở/sai.
  value(input) {
    return DateVi.toIso(input.value) || "";
  },

  set(input, iso) {
    input.value = DateVi.toDisplay(iso);
    DateVi.validate(input);
  },

  validate(input) {
    const bad = DateVi.toIso(input.value) === null;
    input.setCustomValidity(bad ? "Ngày không hợp lệ. Nhập theo dạng dd/mm/yyyy, ví dụ 25/12/2026." : "");
    input.classList.toggle("is-invalid", bad);
    return !bad;
  },

  // Ô ngày không cho gõ tay: bấm vào là mở lịch chọn ngày (lịch gốc của trình duyệt, ẩn đi và neo ngay dưới ô).
  // Hiển thị vẫn là dd/mm/yyyy. Phím Delete/Backspace xoá ngày (ô không bắt buộc), Enter/Space mở lịch.
  attach(input) {
    if (input.dataset.dateViReady) return;
    input.dataset.dateViReady = "1";
    input.type = "text";
    input.inputMode = "none";          // điện thoại không bật bàn phím
    input.maxLength = 10;
    input.autocomplete = "off";
    input.classList.add("date-vi-input");
    if (!input.placeholder) input.placeholder = "dd/mm/yyyy";

    const picker = document.createElement("input");
    picker.type = "date";
    picker.tabIndex = -1;
    picker.setAttribute("aria-hidden", "true");
    picker.className = "date-vi-picker";
    input.insertAdjacentElement("afterend", picker);

    const open = () => {
      if (input.disabled) return;
      picker.style.left = `${input.offsetLeft}px`;
      picker.style.top = `${input.offsetTop + input.offsetHeight}px`;
      picker.style.width = `${input.offsetWidth}px`;
      picker.value = DateVi.toIso(input.value) || "";
      if (input.dataset.min) picker.min = input.dataset.min; else picker.removeAttribute("min");
      if (input.dataset.max) picker.max = input.dataset.max; else picker.removeAttribute("max");
      try { picker.showPicker(); } catch { picker.focus(); picker.click(); }
    };
    const commit = (iso) => {
      input.value = DateVi.toDisplay(iso);
      DateVi.validate(input);
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
    };

    input.addEventListener("click", open);
    input.addEventListener("keydown", (e) => {
      if (e.key === "Tab" || e.key === "Escape" || e.ctrlKey || e.metaKey) return;
      e.preventDefault();
      if (e.key === "Enter" || e.key === " " || e.key === "ArrowDown") open();
      else if ((e.key === "Delete" || e.key === "Backspace") && input.value !== "") commit("");
    });
    input.addEventListener("beforeinput", (e) => e.preventDefault());
    input.addEventListener("paste", (e) => e.preventDefault());
    picker.addEventListener("change", () => { if (picker.value) commit(picker.value); });
  },

  attachAll(root = document) {
    root.querySelectorAll("input[data-date-vi]").forEach(DateVi.attach);
  },
};

// ===== Cảnh báo mất thay đổi khi đóng modal =====
// Modal đã có dữ liệu nhập mà người dùng đóng (nút X, Huỷ, Esc, bấm nền) thì hỏi xác nhận.
// Sau khi lưu thành công gọi guard.markClean() trước modal.hide() để không bị hỏi.
function guardUnsavedChanges(modalEl, formEl, message = "Bạn đã nhập dữ liệu chưa lưu. Đóng và bỏ các thay đổi?") {
  let dirty = false;
  modalEl.addEventListener("shown.bs.modal", () => { dirty = false; });
  formEl.addEventListener("input", () => { dirty = true; });
  formEl.addEventListener("change", () => { dirty = true; });
  modalEl.addEventListener("hide.bs.modal", (e) => {
    if (dirty && !window.confirm(message)) e.preventDefault();
  });
  return { markClean() { dirty = false; } };
}

// ===== Chống bấm gửi hai lần =====
// Khoá nút gửi (kèm biểu tượng quay) trong lúc chờ server; chạy trùng thì bỏ qua lần sau.
async function runLocked(form, task) {
  if (form.dataset.busy === "1") return;
  form.dataset.busy = "1";
  const button = form.querySelector('button[type="submit"]');
  const original = button ? button.innerHTML : "";
  if (button) {
    button.disabled = true;
    button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Đang lưu...';
  }
  try {
    return await task();
  } finally {
    form.dataset.busy = "";
    if (button) {
      button.disabled = false;
      button.innerHTML = original;
    }
  }
}

DateVi.attachAll(); // script nằm cuối <body> nên các ô đã có sẵn; gọi thêm lúc DOM sẵn sàng phòng trường hợp nạp sớm.
document.addEventListener("DOMContentLoaded", () => DateVi.attachAll());
