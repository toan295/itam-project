// Thanh tìm kiếm / bộ lọc nâng cao dùng chung cho các trang danh sách.
// HTML dựng theo mẫu: <form id="filterForm"> chứa hàng lọc chính + vùng #advancedFilters (collapse), nút #advancedToggle
// (kèm huy hiệu #filterCount) và nút #resetFilterBtn.
//
//   const bar = initFilterBar({
//     fields: ["keyword", "status", "sort"],      // id các ô lọc
//     uncounted: ["sort"],                        // ô không tính vào huy hiệu (vd. sắp xếp)
//     textFields: ["keyword"],                    // ô gõ: chờ ngừng gõ 350ms rồi mới lọc (các ô khác lọc ngay khi đổi)
//     onChange: () => loadItems(1),
//     onReset: () => { ... },                     // (tuỳ chọn) khôi phục giá trị cố định sau khi xoá lọc
//   });
//   bar.params({ keyword: "keyword", status: "status" })   // -> URLSearchParams từ các ô có giá trị (hoặc mảng id nếu tên tham số trùng id)
//
// Giá trị trống không gửi lên server. Huy hiệu hiện số bộ lọc đang bật; vùng nâng cao tự mở nếu có bộ lọc nâng cao đang bật.

function initFilterBar({
  formId = "filterForm", resetId = "resetFilterBtn", badgeId = "filterCount", advancedId = "advancedFilters",
  fields, uncounted = [], textFields = [], onChange, onReset = null, debounceMs = 350,
}) {
  const el = (id) => document.getElementById(id);
  const form = el(formId);
  let timer = null;

  // Ô ngày kiểu Việt Nam (data-date-vi, xem form-helpers.js) trả về giá trị ISO; đang nhập dở/sai thì coi như trống.
  const valueOf = (id) => {
    const input = el(id);
    if (!input) return "";
    return input.dataset.dateVi !== undefined ? DateVi.value(input) : input.value.trim();
  };
  const counted = fields.filter((id) => !uncounted.includes(id));

  function refreshBadge() {
    const badge = el(badgeId);
    if (!badge) return;
    // Ô bị khoá (vd. Manager cố định phòng ban của mình) không phải bộ lọc người dùng tự bật nên không tính vào huy hiệu.
    const n = counted.filter((id) => el(id) && !el(id).disabled && valueOf(id) !== "").length;
    badge.textContent = n;
    badge.classList.toggle("d-none", n === 0);
  }

  function trigger() {
    refreshBadge();
    onChange();
  }

  form.addEventListener("submit", (e) => { e.preventDefault(); clearTimeout(timer); trigger(); });

  for (const id of fields) {
    const input = el(id);
    if (!input) continue;
    if (input.dataset.dateVi !== undefined) {
      // Ô ngày chữ: chỉ lọc khi nhập xong một ngày hợp lệ (hoặc xoá trống), không lọc theo chuỗi nhập dở.
      input.addEventListener("change", () => { if (DateVi.validate(input)) trigger(); });
      input.addEventListener("input", () => { if (input.value === "") trigger(); });
    } else if (textFields.includes(id)) {
      input.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(trigger, debounceMs); });
    } else {
      input.addEventListener("change", trigger);
    }
  }

  const reset = el(resetId);
  if (reset) {
    reset.addEventListener("click", () => {
      form.reset();
      form.querySelectorAll("[data-date-vi]").forEach((i) => DateVi.validate(i));
      if (onReset) onReset();
      trigger();
    });
  }

  // Bộ lọc nâng cao đang bật thì mở sẵn vùng nâng cao để người dùng thấy vì sao danh sách bị thu hẹp.
  function openAdvancedIfActive() {
    const panel = el(advancedId);
    if (!panel || !panel.classList.contains("collapse")) return;   // vùng nâng cao luôn hiển thị -> không cần mở
    const inPanel = [...panel.querySelectorAll("input, select")].some((i) => !uncounted.includes(i.id) && valueOf(i.id) !== "");
    if (inPanel) bootstrap.Collapse.getOrCreateInstance(panel, { toggle: false }).show();
  }

  refreshBadge();

  return {
    refreshBadge,
    openAdvancedIfActive,
    // map: { idÔLọc: "tênThamSốQuery" }. Chỉ thêm tham số có giá trị.
    params(map, base = {}) {
      const params = new URLSearchParams(base);
      const entries = Array.isArray(map) ? map.map((id) => [id, id]) : Object.entries(map);
      for (const [id, name] of entries) {
        const value = valueOf(id);
        if (value) params.set(name, value);
      }
      return params;
    },
  };
}
