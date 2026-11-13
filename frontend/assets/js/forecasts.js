// [ĐÃ ĐÓNG] Chức năng Dự báo ngân sách (UC-17) đang được đóng (comment). Muốn mở lại: bỏ "// " ở đầu mỗi dòng của file này.
// const session = requireAuth("../pages/login.html");
// renderNav({ active: "forecasts", basePath: "../" });
//
// const pageSize = 20;
// const MAX_YEARS_AHEAD = 10; // khớp D6 ở backend: từ năm hiện tại đến +10
//
// const detailModal = bootstrap.Modal.getOrCreateInstance(document.getElementById("forecastDetailModal"));
//
// // Kết quả lần "Tạo dự báo" gần nhất, giữ để mở chi tiết từng phòng ban mà không gọi lại API.
// let lastGenerated = [];
//
// function showError(message) {
//   const el = document.getElementById("errorAlert");
//   el.textContent = message;
//   el.classList.remove("d-none");
//   window.scrollTo({ top: 0, behavior: "smooth" });
// }
//
// function clearError() {
//   document.getElementById("errorAlert").classList.add("d-none");
// }
//
// let successTimer = null;
// function showSuccess(message) {
//   const el = document.getElementById("successAlert");
//   el.textContent = message;
//   el.classList.remove("d-none");
//   clearTimeout(successTimer);
//   successTimer = setTimeout(() => el.classList.add("d-none"), 4000);
// }
//
// // Định dạng hiển thị ở frontend (backend chỉ trả số decimal thô).
// function formatVnd(value) {
//   return `${Number(value).toLocaleString("vi-VN")} ₫`;
// }
//
// // Định mức hiển thị kiểu Việt Nam, tối đa 2 chữ số thập phân (0,1 / 1 / 2,5).
// function formatRatio(value) {
//   return Number(value).toLocaleString("vi-VN", { maximumFractionDigits: 2 });
// }
//
// // Gợi ý dưới ô định mức: với 10 người mới thì cần bao nhiêu thiết bị (làm tròn lên, tính trên số nguyên phần trăm để không sai số).
// const RATIO_EXAMPLE_PEOPLE = 10;
//
// // Định mức nhập bằng 2 ô: "cứ P người thì cấp D thiết bị" → định mức = D / P (thiết bị/người).
// // Backend chỉ nhận tối đa 2 chữ số thập phân nên D/P phải chia hết ở mức phần trăm (vd 1/10 = 0,1; 1/3 thì không hợp lệ).
// function gcd(a, b) {
//   return b === 0 ? a : gcd(b, a % b);
// }
//
// function ratioToPair(value) {
//   const hundredths = Math.round(Number(value) * 100);
//   const divisor = gcd(hundredths, 100);
//   return { devices: hundredths / divisor, people: 100 / divisor };
// }
//
// // Trả { value } hợp lệ, { error } mô tả vì sao chưa hợp lệ, hoặc {} khi chưa nhập gì.
// function readRatioPair(peopleText, devicesText) {
//   const peopleRaw = String(peopleText).trim();
//   const devicesRaw = String(devicesText).trim();
//   if (!peopleRaw && !devicesRaw) return {};
//   if (!/^\d+$/.test(peopleRaw) || !/^\d+$/.test(devicesRaw)) return { error: "Nhập số nguyên ở cả hai ô." };
//
//   const people = Number(peopleRaw);
//   const devices = Number(devicesRaw);
//   if (people < 1 || devices < 1) return { error: "Cả hai số phải lớn hơn 0." };
//   if ((devices * 100) % people !== 0) {
//     return { error: `${devices}/${people} không chia hết đến 2 chữ số thập phân. Hãy đổi số người (vd 2, 4, 5, 10, 20, 25, 50, 100).` };
//   }
//   const value = (devices * 100) / people / 100;
//   if (value < 0.01 || value > 20) return { error: "Tỷ lệ phải từ 0,01 đến 20 thiết bị/người." };
//   return { value };
// }
//
// function ratioHintText(peopleText, devicesText) {
//   const { value, error } = readRatioPair(peopleText, devicesText);
//   if (error) return { text: error, ok: false };
//   if (value === undefined) return { text: "", ok: true };
//   const quantity = Math.ceil((RATIO_EXAMPLE_PEOPLE * Math.round(value * 100)) / 100);
//   return { text: `= ${formatRatio(value)} thiết bị/người. Ví dụ: ${RATIO_EXAMPLE_PEOPLE} người mới → cần ${quantity} thiết bị.`, ok: true };
// }
//
// // Ô nhập tiền: chỉ giữ chữ số và tự chèn dấu chấm ngăn cách hàng nghìn khi đang gõ (25000000 -> 25.000.000).
// function digitsOnly(text) {
//   return String(text).replace(/\D/g, "");
// }
//
// function groupThousands(digits) {
//   return digits.replace(/^0+(?=\d)/, "").replace(/\B(?=(\d{3})+(?!\d))/g, ".");
// }
//
// function parseVndInput(text) {
//   const digits = digitsOnly(text);
//   return digits ? Number(digits) : NaN;
// }
//
// function formatVndInput(input) {
//   // Giữ vị trí con trỏ theo số chữ số đứng trước nó, nếu không con trỏ nhảy về cuối khi sửa giữa chuỗi.
//   const caret = input.selectionStart ?? input.value.length;
//   const digitsBeforeCaret = digitsOnly(input.value.slice(0, caret)).length;
//   const formatted = groupThousands(digitsOnly(input.value));
//   input.value = formatted;
//
//   let position = 0;
//   let seen = 0;
//   while (position < formatted.length && seen < digitsBeforeCaret) {
//     if (/\d/.test(formatted[position])) seen += 1;
//     position += 1;
//   }
//   input.setSelectionRange(position, position);
// }
//
// const REASON_LABELS = {
//   AgeExceeded: (a) => `Quá tuổi (ngưỡng ${a.maxAgeYears} năm)`,
//   FailureCountExceeded: (a) => `Bảo trì nhiều (ngưỡng ${a.maxFailureCount} lần)`,
// };
//
// function formatDate(value) {
//   if (!value) return "-";
//   const date = new Date(`${value}T00:00:00`);
//   return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString("vi-VN");
// }
//
// // Lỗi 409 (người khác vừa sửa): báo lỗi rồi nạp lại để thấy giá trị mới nhất, không ghi đè.
// async function handleInputError(err, reload) {
//   showError(err.message);
//   if (err.status === 409) {
//     try { await reload(); } catch { /* giữ nguyên bảng hiện tại */ }
//   }
// }
//
// function updatedCell(item) {
//   if (!item.updatedAt && !item.updatedByName) return '<span class="text-muted small">—</span>';
//   return `<span class="small">${escapeHtml(formatDateTime(item.updatedAt))}${item.updatedByName ? ` · ${escapeHtml(item.updatedByName)}` : ""}</span>`;
// }
//
// const CHANGE_LABELS = {
//   Created: ["text-bg-success", "Tạo mới"],
//   Updated: ["text-bg-primary", "Đã cập nhật (đầu vào đổi)"],
//   Restored: ["text-bg-info", "Khôi phục dòng đã xoá"],
//   Unchanged: ["text-bg-secondary", "Không đổi (đầu vào y hệt)"],
// };
//
// function staleBadge(f) {
//   if (f.staleStatus === "Current") return '<span class="badge text-bg-success" title="Đầu vào hiện tại khớp với lúc tạo dự báo">Khớp đầu vào</span>';
//   if (f.staleStatus === "Stale") {
//     return `<span class="badge text-bg-warning" title="${escapeHtml((f.staleReasons || []).join("\n"))}">Đã cũ — đầu vào đã đổi</span>`;
//   }
//   return '<span class="badge text-bg-light border" title="Dự báo tạo trước khi hệ thống lưu dữ liệu đầu vào — tạo lại để có cơ sở so sánh">Chưa rõ</span>';
// }
//
// function formatDateTime(value) {
//   if (!value) return "-";
//   // API trả giờ UTC không kèm "Z" -> thêm "Z" để trình duyệt đổi đúng sang giờ địa phương.
//   const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/.test(value) ? value : `${value}Z`);
//   return Number.isNaN(date.getTime()) ? value : date.toLocaleString("vi-VN");
// }
//
// // ---------- Khởi tạo bộ lọc ----------
//
// function fillYearOptions() {
//   const current = new Date(Date.now() + 7 * 3600 * 1000).getUTCFullYear();   // năm theo lịch Việt Nam, cùng quy tắc với backend
//   const options = Array.from({ length: MAX_YEARS_AHEAD + 1 }, (_, i) => current + i)
//     .map((year) => `<option value="${year}">${year}</option>`).join("");
//   document.getElementById("genYear").innerHTML = options;
//   document.getElementById("planYear").innerHTML = options;
// }
//
// async function loadDepartments() {
//   const departments = (await api.get("/departments")) || [];
//   const options = departments.map((d) => `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join("");
//   document.getElementById("genDepartment").insertAdjacentHTML("beforeend", options);
//   document.getElementById("filterDepartment").insertAdjacentHTML("beforeend", options);
// }
//
// // ---------- Tạo dự báo ----------
//
// function renderWarnings(warnings) {
//   const banner = document.getElementById("warningBanner");
//   if (!warnings.length) {
//     banner.classList.add("d-none");
//     return;
//   }
//   document.getElementById("warningList").innerHTML = warnings.map((w) => `<li>${escapeHtml(w)}</li>`).join("");
//   banner.classList.remove("d-none");
// }
//
// function changeCell(f) {
//   const [css, label] = CHANGE_LABELS[f.change] || ["text-bg-secondary", f.change || "—"];
//   let html = `<span class="badge ${css}">${label}</span> <span class="text-muted small">Revision ${f.revision}</span>`;
//   if (f.previous && f.change === "Updated") {
//     const diff = Number(f.totalBudget) - Number(f.previous.totalBudget);
//     html += `<div class="small text-muted">Trước đó (rev ${f.previous.revision}): ${f.previous.totalPurchaseCount} thiết bị — ${formatVnd(f.previous.totalBudget)}` +
//       ` <strong class="${diff === 0 ? "" : diff > 0 ? "text-danger" : "text-success"}">(${diff >= 0 ? "+" : "−"}${formatVnd(Math.abs(diff))})</strong></div>`;
//   }
//   return html;
// }
//
// function renderGenerated(result) {
//   lastGenerated = result.forecasts || [];
//   renderWarnings(result.warnings || []);
//
//   // Tổng = thay thế + mua thêm (backend đã tính sẵn từng dòng).
//   const totalCount = lastGenerated.reduce((sum, f) => sum + f.totalPurchaseCount, 0);
//   const totalBudget = lastGenerated.reduce((sum, f) => sum + Number(f.totalBudget), 0);
//
//   document.getElementById("resultTitle").textContent = `Kết quả dự báo năm ${result.year}`;
//   document.getElementById("resultTotal").textContent = `Tổng: ${totalCount} thiết bị — ${formatVnd(totalBudget)}`;
//
//   const body = document.getElementById("resultTableBody");
//   body.innerHTML = lastGenerated.length
//     ? lastGenerated.map((f, index) => `
//       <tr>
//         <td class="fw-semibold">${escapeHtml(f.departmentName)}
//           ${f.totalPurchaseCount === 0 ? `<div class="small text-muted fw-normal">Chưa có thiết bị nào cần thay hoặc mua thêm năm ${f.year}</div>` : ""}
//         </td>
//         <td class="text-end">${f.estimatedReplacementCount} thiết bị<div class="small text-muted">${formatVnd(f.estimatedBudget)}</div></td>
//         <td class="text-end">${f.estimatedExpansionCount} thiết bị<div class="small text-muted">${formatVnd(f.estimatedExpansionBudget)}</div>
//           <div class="small text-muted">Nhân sự: ${f.baselineHeadcount} hiện tại ${f.additionalHeadcount >= 0 ? "+" : "−"} ${Math.abs(f.additionalHeadcount)} = ${f.projectedHeadcount}</div></td>
//         <td class="text-end fw-semibold">${f.totalPurchaseCount} thiết bị<div class="small fw-normal">${formatVnd(f.totalBudget)}</div></td>
//         <td>${changeCell(f)}</td>
//         <td class="text-end">
//           <button class="btn btn-sm btn-outline-primary" data-action="generated-detail" data-index="${index}">
//             <i class="bi bi-eye"></i> Chi tiết
//           </button>
//         </td>
//       </tr>`).join("")
//     : '<tr><td colspan="6" class="text-center text-muted py-4">Không có phòng ban nào.</td></tr>';
//
//   document.getElementById("resultSection").classList.remove("d-none");
// }
//
// document.getElementById("generateForm").addEventListener("submit", async (event) => {
//   event.preventDefault();
//   clearError();
//
//   const button = document.getElementById("generateBtn");
//   const departmentId = document.getElementById("genDepartment").value;
//   const payload = { year: Number(document.getElementById("genYear").value) };
//   if (departmentId) payload.departmentId = Number(departmentId);
//
//   button.disabled = true; // tránh bấm đúp
//   try {
//     renderGenerated(await api.post("/forecasts/generate", payload));
//     await loadHistory(1);
//   } catch (err) {
//     showError(err.message);
//   } finally {
//     button.disabled = false;
//   }
// });
//
// document.getElementById("resultTableBody").addEventListener("click", (event) => {
//   const button = event.target.closest('[data-action="generated-detail"]');
//   if (button) openDetail(lastGenerated[Number(button.dataset.index)]);
// });
//
// // ---------- Chi tiết (modal) ----------
//
// function openDetail(forecast) {
//   document.getElementById("detailTitle").textContent = `Dự báo năm ${forecast.year} — ${forecast.departmentName}`;
//   document.getElementById("detailCount").textContent =
//     `${forecast.estimatedReplacementCount} thiết bị — ${formatVnd(forecast.estimatedBudget)}`;
//   document.getElementById("detailExpansion").textContent =
//     `${forecast.estimatedExpansionCount} thiết bị — ${formatVnd(forecast.estimatedExpansionBudget)}`;
//   document.getElementById("detailTotal").textContent =
//     `${forecast.totalPurchaseCount} thiết bị — ${formatVnd(forecast.totalBudget)}`;
//   document.getElementById("detailGeneratedAt").textContent = formatDateTime(forecast.generatedAt);
//
//   const staleBox = document.getElementById("detailStale");
//   if (forecast.staleStatus === "Stale") {
//     staleBox.innerHTML = `<strong>Dự báo này đã cũ</strong> — số liệu bên dưới là bản đã lưu lúc ${escapeHtml(formatDateTime(forecast.generatedAt))} (Revision ${forecast.revision}). Từ đó đầu vào đã đổi:` +
//       `<ul class="mb-0">${(forecast.staleReasons || []).map((r) => `<li>${escapeHtml(r)}</li>`).join("")}</ul>Bấm “Tạo dự báo” lại để cập nhật.`;
//     staleBox.classList.remove("d-none");
//   } else if (forecast.staleStatus === "Unknown") {
//     staleBox.textContent = "Dự báo này tạo trước khi hệ thống lưu dữ liệu đầu vào nên không so sánh được với hiện tại. Tạo lại để có cơ sở so sánh.";
//     staleBox.classList.remove("d-none");
//   } else {
//     staleBox.classList.add("d-none");
//   }
//
//   const notes = document.getElementById("detailNotes");
//   notes.textContent = forecast.notes || "";
//   notes.classList.toggle("d-none", !forecast.notes);
//
//   const breakdown = forecast.breakdown || [];
//   const assets = breakdown.flatMap((b) => (b.assets || []).map((a) => ({ ...a, categoryName: b.categoryName })));
//
//   document.getElementById("detailExplain").textContent = forecast.estimatedReplacementCount === 0
//     ? `Phòng ban này chưa có tài sản nào đến hạn thay thế trong năm ${forecast.year} (chưa vượt ngưỡng tuổi hoặc ngưỡng số lần bảo trì, hoặc đã được tính ở năm khác).`
//     : `${forecast.estimatedReplacementCount} tài sản của phòng ban này đến hạn thay thế trong năm ${forecast.year}. Danh sách bên dưới cho biết từng tài sản và lý do.`;
//
//   document.getElementById("detailAssetsBody").innerHTML = assets.length
//     ? assets.map((a) => `
//         <tr>
//           <td class="text-nowrap">${escapeHtml(a.assetCode)}</td>
//           <td>${escapeHtml(a.assetName)}</td>
//           <td>${escapeHtml(a.categoryName)}</td>
//           <td class="text-nowrap">${formatDate(a.purchaseDate)}</td>
//           <td class="text-end">${a.ageYears == null ? "-" : formatAge(a.ageYears)}</td>
//           <td class="text-end">${a.ticketCount}</td>
//           <td>${a.basis
//             ? `<span class="small">${escapeHtml(a.basis)}</span>`
//             : ((a.reasons || []).length === 0
//               ? `<span class="badge text-bg-light border">Tròn ngưỡng tuổi trong năm ${forecast.year}</span>`
//               : (a.reasons || []).map((r) => `<span class="badge text-bg-secondary me-1">${escapeHtml(REASON_LABELS[r] ? REASON_LABELS[r](a) : r)}</span>`).join(""))}</td>
//         </tr>`).join("")
//     : `<tr><td colspan="7" class="text-center text-muted py-3">${
//         forecast.estimatedReplacementCount > 0
//           ? "Dự báo này được tạo trước khi có tính năng liệt kê tài sản — bấm “Tạo dự báo” lại để xem danh sách."
//           : "Không có tài sản nào."}</td></tr>`;
//
//   document.getElementById("detailBreakdownBody").innerHTML = breakdown.length
//     ? breakdown.map((b) => b.unitPrice == null
//       ? `<tr class="table-warning">
//           <td>${escapeHtml(b.categoryName)}</td>
//           <td class="text-end">${b.assetCount}</td>
//           <td class="text-end" colspan="2"><span class="badge text-bg-warning">Chưa có đơn giá</span></td>
//         </tr>`
//       : `<tr>
//           <td>${escapeHtml(b.categoryName)}</td>
//           <td class="text-end">${b.assetCount}</td>
//           <td class="text-end">${formatVnd(b.unitPrice)}</td>
//           <td class="text-end">${formatVnd(b.subtotal)}</td>
//         </tr>`).join("")
//     : '<tr><td colspan="4" class="text-center text-muted py-3">Không có thiết bị nào đến hạn thay thế.</td></tr>';
//
//   renderExpansionDetail(forecast);
//   renderPurchaseSummary(forecast);
//
//   detailModal.show();
// }
//
// function renderExpansionDetail(forecast) {
//   const items = forecast.expansionBreakdown || [];
//   const sign = forecast.additionalHeadcount >= 0 ? "+" : "−";
//   document.getElementById("detailExpansionExplain").textContent = items.length
//     ? `Nhân sự phòng ban: ${forecast.baselineHeadcount} đang hoạt động lúc tạo dự báo ${sign} ${Math.abs(forecast.additionalHeadcount)} theo kế hoạch = ${forecast.projectedHeadcount} người. ` +
//       `Nhu cầu = ⌈nhân sự × định mức⌉; mua thêm = nhu cầu − thiết bị cùng loại đang dùng được (chỉ khi thiếu). Thiết bị đến hạn thay thế được thay 1:1 ở phần thay thế nên không tính hai lần.`
//     : "Không có nhu cầu mua thêm: chưa cấu hình định mức, phòng ban chưa có nhân sự/nhu cầu, hoặc dự báo được tạo trước khi có tính năng này (bấm “Tạo dự báo” lại để cập nhật).";
//
//   document.getElementById("detailExpansionBody").innerHTML = items.length
//     ? items.map((i) => {
//       const priced = i.unitPrice != null;
//       const hint = i.replacementPossiblyAvoidable > 0
//         ? `<div class="text-warning-emphasis small">Đang dư ${i.surplus} thiết bị: có thể không cần mua thay ${i.replacementPossiblyAvoidable} thiết bị đến hạn (chưa trừ khỏi dự báo).</div>` : "";
//       return `<tr class="${priced || i.quantity === 0 ? "" : "table-warning"}">
//           <td>${escapeHtml(i.categoryName)}</td>
//           <td class="text-end">${formatRatio(i.devicesPerPerson)}</td>
//           <td class="text-end">${i.demand}</td>
//           <td class="text-end">${i.existingUsable}${i.replacementDue > 0 ? `<div class="small text-muted">${i.replacementDue} đến hạn thay</div>` : ""}</td>
//           <td class="text-end fw-semibold">${i.quantity}</td>
//           ${i.quantity === 0 ? '<td class="text-end text-muted" colspan="2">—</td>'
//             : priced ? `<td class="text-end">${formatVnd(i.unitPrice)}</td><td class="text-end">${formatVnd(i.subtotal)}</td>`
//               : '<td class="text-end" colspan="2"><span class="badge text-bg-warning">Chưa có đơn giá</span></td>'}
//           <td class="small">${escapeHtml(i.explanation || "")}${hint}</td>
//         </tr>`;
//     }).join("")
//     : '<tr><td colspan="8" class="text-center text-muted py-3">Không có thiết bị nào cần mua thêm.</td></tr>';
// }
//
// function renderPurchaseSummary(forecast) {
//   const items = forecast.purchaseSummary || [];
//   document.getElementById("detailPurchaseBody").innerHTML = items.length
//     ? items.map((i) => `
//         <tr class="${i.unitPrice == null ? "table-warning" : ""}">
//           <td>${escapeHtml(i.categoryName)}</td>
//           <td class="text-end">${i.replacementQuantity}</td>
//           <td class="text-end">${i.expansionQuantity}</td>
//           <td class="text-end fw-semibold">${i.totalQuantity}</td>
//           ${i.unitPrice == null
//             ? '<td class="text-end" colspan="2"><span class="badge text-bg-warning">Chưa có đơn giá</span></td>'
//             : `<td class="text-end">${formatVnd(i.unitPrice)}</td><td class="text-end">${formatVnd(i.totalCost)}</td>`}
//         </tr>`).join("")
//     : '<tr><td colspan="6" class="text-center text-muted py-3">Không có thiết bị nào cần mua.</td></tr>';
// }
//
// async function viewHistoryDetail(id) {
//   clearError();
//   try {
//     openDetail(await api.get(`/forecasts/${id}`));
//   } catch (err) {
//     showError(err.message);
//   }
// }
//
// // ---------- Lịch sử ----------
//
// function buildHistoryQuery(page) {
//   return historyFilterBar.params({
//     filterYear: "year", filterDepartment: "departmentId", filterKeyword: "keyword", filterKind: "kind",
//     filterMinBudget: "minBudget", filterMaxBudget: "maxBudget", filterSort: "sort",
//   }, { page: String(page), pageSize: String(pageSize) }).toString();
// }
//
// let historySeq = 0;
//
// async function loadHistory(page = 1) {
//   const seq = ++historySeq;   // đổi bộ lọc nhanh: chỉ phản hồi mới nhất được hiển thị
//   try {
//     let result = await api.get(`/forecasts?${buildHistoryQuery(page)}`);
//     if (seq !== historySeq) return;
//     if (!(result.items || []).length && result.totalItems > 0 && page > 1) {
//       result = await api.get(`/forecasts?${buildHistoryQuery(result.totalPages)}`);   // đang ở trang cuối rồi dữ liệu giảm: lùi về trang còn dữ liệu
//       if (seq !== historySeq) return;
//     }
//     renderHistory(result.items || []);
//     renderPagination(result);
//     document.getElementById("totalItemsLabel").textContent = `Tổng cộng: ${result.totalItems} dự báo`;
//   } catch (err) {
//     showError(err.message);
//   }
// }
//
// function renderHistory(items) {
//   const body = document.getElementById("historyTableBody");
//   if (!items.length) {
//     body.innerHTML = '<tr><td colspan="9" class="text-center text-muted py-4">Chưa có dự báo nào.</td></tr>';
//     return;
//   }
//   body.innerHTML = items.map((f) => `
//     <tr>
//       <td>${f.year}</td>
//       <td>${escapeHtml(f.departmentName)}</td>
//       <td class="text-end">${f.estimatedReplacementCount}</td>
//       <td class="text-end">${f.estimatedExpansionCount}</td>
//       <td class="text-end fw-semibold">${f.totalPurchaseCount}</td>
//       <td class="text-end">
//         ${formatVnd(f.totalBudget)}
//         ${f.notes ? '<i class="bi bi-exclamation-triangle text-warning ms-1" title="Có loại chưa có đơn giá"></i>' : ""}
//       </td>
//       <td>${escapeHtml(formatDateTime(f.generatedAt))}<div class="small text-muted">Revision ${f.revision}</div></td>
//       <td>${staleBadge(f)}</td>
//       <td class="text-end">
//         <button class="btn btn-sm btn-outline-primary" data-action="history-detail" data-id="${f.id}">
//           <i class="bi bi-eye"></i> Xem chi tiết
//         </button>
//         <button class="btn btn-sm btn-outline-danger" data-action="history-delete" data-id="${f.id}">
//           <i class="bi bi-trash"></i> Xoá
//         </button>
//       </td>
//     </tr>`).join("");
// }
//
// function renderPagination(result) {
//   const el = document.getElementById("pagination");
//   el.innerHTML = "";
//   const total = result.totalPages;
//   if (total <= 1) return;
//   const pages = new Set([1, total]);
//   for (let p = result.page - 2; p <= result.page + 2; p += 1) if (p >= 1 && p <= total) pages.add(p);
//   let prev = 0;
//   for (const page of [...pages].sort((a, b) => a - b)) {
//     if (page - prev > 1) {
//       const gap = document.createElement("li");
//       gap.className = "page-item disabled";
//       gap.innerHTML = '<span class="page-link">…</span>';
//       el.appendChild(gap);
//     }
//     const li = document.createElement("li");
//     li.className = `page-item ${page === result.page ? "active" : ""}`;
//     li.innerHTML = `<button class="page-link" type="button">${page}</button>`;
//     li.querySelector("button").addEventListener("click", () => loadHistory(page));
//     el.appendChild(li);
//     prev = page;
//   }
// }
//
// document.getElementById("historyTableBody").addEventListener("click", async (event) => {
//   const detailBtn = event.target.closest('[data-action="history-detail"]');
//   if (detailBtn) {
//     viewHistoryDetail(Number(detailBtn.dataset.id));
//     return;
//   }
//
//   const deleteBtn = event.target.closest('[data-action="history-delete"]');
//   if (!deleteBtn) return;
//   if (!confirm("Xoá dự báo này khỏi danh sách? Dữ liệu vẫn được giữ trong hệ thống (xoá mềm) và dự báo có thể được tạo lại.")) return;
//
//   clearError();
//   deleteBtn.disabled = true;
//   try {
//     await api.del(`/forecasts/${deleteBtn.dataset.id}`);
//     await loadHistory(1);
//   } catch (err) {
//     showError(err.message);
//     deleteBtn.disabled = false;
//   }
// });
//
// const historyFilterBar = initFilterBar({
//   formId: "historyFilterForm",
//   fields: ["filterKeyword", "filterYear", "filterDepartment", "filterKind", "filterMinBudget", "filterMaxBudget", "filterSort"],
//   uncounted: ["filterSort"],
//   textFields: ["filterKeyword", "filterYear", "filterMinBudget", "filterMaxBudget"],
//   onChange: () => loadHistory(1),
// });
//
// // ---------- Đơn giá tham khảo ----------
//
// async function loadPrices() {
//   try {
//     renderPrices((await api.get("/forecasts/reference-prices")) || []);
//   } catch (err) {
//     showError(err.message);
//   }
// }
//
// let priceCache = new Map();
// let ratioCache = new Map();
// let planCache = new Map();
//
// function renderPrices(items) {
//   const body = document.getElementById("priceTableBody");
//   if (!items.length) {
//     body.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Chưa có loại tài sản nào.</td></tr>';
//     return;
//   }
//   priceCache = new Map(items.map((p) => [p.categoryId, p]));
//   body.innerHTML = items.map((p) => {
//     const configured = p.unitPrice != null;
//     return `
//       <tr data-category-id="${p.categoryId}">
//         <td class="fw-semibold">${escapeHtml(p.categoryName)}</td>
//         <td>
//           <div class="input-group input-group-sm">
//             <input type="text" inputmode="numeric" autocomplete="off" class="form-control price-input"
//                    value="${configured ? groupThousands(String(Math.round(p.unitPrice))) : ""}" placeholder="VD: 25.000.000"
//                    aria-label="Đơn giá ${escapeHtml(p.categoryName)}">
//             <span class="input-group-text">₫</span>
//           </div>
//         </td>
//         <td>${configured
//           ? '<span class="badge text-bg-success">Đã cấu hình</span>'
//           : '<span class="badge text-bg-warning">Chưa cấu hình</span>'}</td>
//         <td>${updatedCell(p)}</td>
//         <td class="text-end text-nowrap">
//           <button class="btn btn-sm btn-primary" data-action="save-price"><i class="bi bi-check2"></i> Lưu</button>
//           ${configured ? '<button class="btn btn-sm btn-outline-danger" data-action="delete-price"><i class="bi bi-trash"></i> Xoá</button>' : ""}
//         </td>
//       </tr>`;
//   }).join("");
// }
//
// document.getElementById("priceTableBody").addEventListener("click", async (event) => {
//   const button = event.target.closest("[data-action]");
//   if (!button) return;
//
//   const row = button.closest("tr");
//   const categoryId = Number(row.dataset.categoryId);
//   clearError();
//
//   try {
//     if (button.dataset.action === "save-price") {
//       const value = parseVndInput(row.querySelector(".price-input").value);
//       if (!Number.isFinite(value) || value <= 0) {
//         showError("Đơn giá phải là số lớn hơn 0.");
//         return;
//       }
//       if (value > 1_000_000_000_000) {
//         showError("Đơn giá vượt quá giới hạn cho phép.");
//         return;
//       }
//       const current = priceCache.get(categoryId);
//       if (current && current.unitPrice != null && Number(current.unitPrice) === value) {
//         showSuccess("Đơn giá không thay đổi nên không cần lưu.");
//         return;
//       }
//       await api.put(`/forecasts/reference-prices/${categoryId}`, { unitPrice: value, version: current && current.unitPrice != null ? current.version : null });
//       showSuccess("Đã lưu đơn giá. Dự báo đã tạo giữ nguyên số liệu cũ (hiện nhãn “Đã cũ”) — bấm “Tạo dự báo” lại để cập nhật.");
//     } else if (button.dataset.action === "delete-price") {
//       if (!confirm("Xoá đơn giá tham khảo của loại này? Loại này sẽ không còn được tính vào ngân sách (dữ liệu vẫn được giữ trong hệ thống; nhập lại đơn giá để dùng lại).")) return;
//       await api.del(`/forecasts/reference-prices/${categoryId}?version=${priceCache.get(categoryId).version}`);
//     }
//     await Promise.all([loadPrices(), loadHistory(1)]);
//   } catch (err) {
//     await handleInputError(err, loadPrices);
//   }
// });
//
// // Tự định dạng khi đang gõ/dán; Enter trong ô giá = Lưu.
// document.getElementById("priceTableBody").addEventListener("input", (event) => {
//   if (event.target.matches(".price-input")) formatVndInput(event.target);
// });
//
// document.getElementById("priceTableBody").addEventListener("keydown", (event) => {
//   if (event.key === "Enter" && event.target.matches(".price-input")) {
//     event.preventDefault();
//     event.target.closest("tr").querySelector('[data-action="save-price"]').click();
//   }
// });
//
// // ---------- Định mức thiết bị/người ----------
//
// async function loadRatios() {
//   try {
//     renderRatios((await api.get("/forecasts/device-ratios")) || []);
//   } catch (err) {
//     showError(err.message);
//   }
// }
//
// function renderRatios(items) {
//   const body = document.getElementById("ratioTableBody");
//   if (!items.length) {
//     body.innerHTML = '<tr><td colspan="5" class="text-center text-muted py-4">Chưa có loại tài sản nào.</td></tr>';
//     return;
//   }
//   ratioCache = new Map(items.map((r) => [r.categoryId, r]));
//   body.innerHTML = items.map((r) => {
//     const configured = r.devicesPerPerson != null;
//     const pair = configured ? ratioToPair(r.devicesPerPerson) : null;
//     return `
//       <tr data-category-id="${r.categoryId}">
//         <td class="fw-semibold">${escapeHtml(r.categoryName)}</td>
//         <td>
//           <div class="input-group input-group-sm">
//             <span class="input-group-text">Cứ</span>
//             <input type="text" inputmode="numeric" autocomplete="off" class="form-control ratio-people" style="max-width:70px"
//                    value="${pair ? pair.people : ""}" placeholder="10"
//                    aria-label="Số người (${escapeHtml(r.categoryName)})">
//             <span class="input-group-text">người → cấp</span>
//             <input type="text" inputmode="numeric" autocomplete="off" class="form-control ratio-devices" style="max-width:70px"
//                    value="${pair ? pair.devices : ""}" placeholder="1"
//                    aria-label="Số thiết bị ${escapeHtml(r.categoryName)}">
//             <span class="input-group-text">thiết bị</span>
//           </div>
//           <div class="form-text ratio-example">${pair ? escapeHtml(ratioHintText(pair.people, pair.devices).text) : ""}</div>
//         </td>
//         <td>${configured
//           ? '<span class="badge text-bg-success" title="Đã có tỷ lệ: loại này được tính vào nhu cầu mua thêm.">Đã cấu hình</span>'
//           : '<span class="badge text-bg-secondary text-wrap text-start" title="Chưa nhập tỷ lệ và bấm Lưu: loại này bị bỏ qua khi tính mua thêm.">Chưa cấu hình — không tính mua thêm</span>'}</td>
//         <td>${updatedCell(r)}</td>
//         <td class="text-end text-nowrap">
//           <button class="btn btn-sm btn-primary" data-action="save-ratio"><i class="bi bi-check2"></i> Lưu</button>
//           ${configured ? '<button class="btn btn-sm btn-outline-danger" data-action="delete-ratio"><i class="bi bi-trash"></i> Xoá</button>' : ""}
//         </td>
//       </tr>`;
//   }).join("");
// }
//
// document.getElementById("ratioTableBody").addEventListener("click", async (event) => {
//   const button = event.target.closest("[data-action]");
//   if (!button) return;
//
//   const row = button.closest("tr");
//   const categoryId = Number(row.dataset.categoryId);
//   clearError();
//
//   try {
//     if (button.dataset.action === "save-ratio") {
//       const { value, error } = readRatioPair(row.querySelector(".ratio-people").value, row.querySelector(".ratio-devices").value);
//       if (value === undefined) {
//         showError(error || "Hãy nhập cả hai ô: số người và số thiết bị được cấp cho số người đó.");
//         return;
//       }
//       const current = ratioCache.get(categoryId);
//       if (current && current.devicesPerPerson != null && Number(current.devicesPerPerson) === value) {
//         showSuccess("Định mức không thay đổi nên không cần lưu.");
//         return;
//       }
//       await api.put(`/forecasts/device-ratios/${categoryId}`, { devicesPerPerson: value, version: current && current.devicesPerPerson != null ? current.version : null });
//       showSuccess("Đã lưu định mức. Bấm “Tạo dự báo” lại để áp dụng.");
//     } else if (button.dataset.action === "delete-ratio") {
//       if (!confirm("Xoá định mức của loại này? Loại này sẽ không còn được tính vào nhu cầu mua thêm.")) return;
//       await api.del(`/forecasts/device-ratios/${categoryId}?version=${ratioCache.get(categoryId).version}`);
//       showSuccess("Đã xoá định mức. Bấm “Tạo dự báo” lại để áp dụng.");
//     }
//     await Promise.all([loadRatios(), loadHistory(1)]);
//   } catch (err) {
//     await handleInputError(err, loadRatios);
//   }
// });
//
// document.getElementById("ratioTableBody").addEventListener("input", (event) => {
//   if (!event.target.matches(".ratio-people, .ratio-devices")) return;
//   const cell = event.target.closest("td");
//   const { text, ok } = ratioHintText(cell.querySelector(".ratio-people").value, cell.querySelector(".ratio-devices").value);
//   const hint = cell.querySelector(".ratio-example");
//   hint.textContent = text;
//   hint.classList.toggle("text-danger", !ok);
// });
//
// document.getElementById("ratioTableBody").addEventListener("keydown", (event) => {
//   if (event.key === "Enter" && event.target.matches(".ratio-people, .ratio-devices")) {
//     event.preventDefault();
//     event.target.closest("tr").querySelector('[data-action="save-ratio"]').click();
//   }
// });
//
// // ---------- Kế hoạch mở rộng nhân sự ----------
//
// async function loadPlans() {
//   const year = document.getElementById("planYear").value;
//   try {
//     renderPlans((await api.get(`/forecasts/expansion-plans?year=${encodeURIComponent(year)}`)) || []);
//   } catch (err) {
//     showError(err.message);
//   }
// }
//
// function renderPlans(items) {
//   const body = document.getElementById("planTableBody");
//   if (!items.length) {
//     body.innerHTML = '<tr><td colspan="7" class="text-center text-muted py-4">Chưa có phòng ban nào.</td></tr>';
//     return;
//   }
//   planCache = new Map(items.map((p) => [p.departmentId, p]));
//   body.innerHTML = items.map((p) => `
//     <tr data-department-id="${p.departmentId}" data-year="${p.year}">
//       <td class="fw-semibold">${escapeHtml(p.departmentName)}</td>
//       <td class="text-center">${p.baselineHeadcount}</td>
//       <td>
//         <input type="number" min="-5000" max="5000" step="1" class="form-control form-control-sm text-center mx-auto plan-headcount ${p.hasPlan ? "" : "text-muted"}"
//                value="${p.additionalHeadcount}" aria-label="Nhân sự tăng thêm ${escapeHtml(p.departmentName)}">
//       </td>
//       <td class="text-center fw-semibold plan-projected">${p.projectedHeadcount}</td>
//       <td><input type="text" maxlength="500" class="form-control form-control-sm plan-notes" value="${escapeHtml(p.notes || "")}"
//                  placeholder="Ghi chú (tuỳ chọn)" aria-label="Ghi chú ${escapeHtml(p.departmentName)}"></td>
//       <td class="small text-muted">${p.hasPlan ? updatedCell(p) : "Chưa có kế hoạch"}</td>
//       <td class="text-end text-nowrap">
//         <button class="btn btn-sm btn-primary" data-action="save-plan"><i class="bi bi-check2"></i> Lưu</button>
//         ${p.hasPlan ? '<button class="btn btn-sm btn-outline-danger" data-action="delete-plan"><i class="bi bi-trash"></i> Xoá</button>' : ""}
//       </td>
//     </tr>`).join("");
// }
//
// document.getElementById("planYear").addEventListener("change", loadPlans);
//
// document.getElementById("planTableBody").addEventListener("click", async (event) => {
//   const button = event.target.closest("[data-action]");
//   if (!button) return;
//
//   const row = button.closest("tr");
//   const year = Number(row.dataset.year);
//   const departmentId = Number(row.dataset.departmentId);
//   clearError();
//
//   try {
//     if (button.dataset.action === "save-plan") {
//       const raw = row.querySelector(".plan-headcount").value.trim();
//       const headcount = Number(raw);
//       if (raw === "" || !Number.isInteger(headcount) || headcount < -5000 || headcount > 5000) {
//         showError("Thay đổi nhân sự phải là số nguyên từ −5.000 đến 5.000 (âm = giảm).");
//         return;
//       }
//       const notes = row.querySelector(".plan-notes").value.trim();
//       const current = planCache.get(departmentId);
//       if (current && current.hasPlan && current.additionalHeadcount === headcount && (current.notes || "") === notes) {
//         showSuccess("Kế hoạch không thay đổi nên không cần lưu.");
//         return;
//       }
//       await api.put(`/forecasts/expansion-plans/${year}/${departmentId}`, {
//         additionalHeadcount: headcount,
//         notes: notes || null,
//         version: current && current.hasPlan ? current.version : null,
//       });
//       showSuccess("Đã lưu kế hoạch mở rộng. Bấm “Tạo dự báo” lại để áp dụng.");
//     } else if (button.dataset.action === "delete-plan") {
//       if (!confirm("Xoá kế hoạch mở rộng của phòng ban này trong năm đã chọn?")) return;
//       await api.del(`/forecasts/expansion-plans/${year}/${departmentId}?version=${planCache.get(departmentId).version}`);
//       showSuccess("Đã xoá kế hoạch mở rộng. Bấm “Tạo dự báo” lại để áp dụng.");
//     }
//     await Promise.all([loadPlans(), loadHistory(1)]);
//   } catch (err) {
//     await handleInputError(err, loadPlans);
//   }
// });
//
// document.getElementById("planTableBody").addEventListener("input", (event) => {
//   if (!event.target.matches(".plan-headcount")) return;
//   const row = event.target.closest("tr");
//   const plan = planCache.get(Number(row.dataset.departmentId));
//   const value = Number(event.target.value);
//   if (plan && event.target.value.trim() !== "" && Number.isInteger(value)) {
//     row.querySelector(".plan-projected").textContent = Math.max(0, plan.baselineHeadcount + value);
//   }
// });
//
// document.getElementById("planTableBody").addEventListener("keydown", (event) => {
//   if (event.key === "Enter" && event.target.matches(".plan-headcount, .plan-notes")) {
//     event.preventDefault();
//     event.target.closest("tr").querySelector('[data-action="save-plan"]').click();
//   }
// });
//
// // ---------- Khởi động ----------
//
// if (!can("forecasts.manage")) {
//   document.getElementById("forbiddenAlert").textContent = "Chỉ Admin IT được phép sử dụng Dự báo ngân sách.";
//   document.getElementById("forbiddenAlert").classList.remove("d-none");
//   document.getElementById("forecastContent").classList.add("d-none");
// } else {
//   fillYearOptions();
//   loadDepartments()
//     .catch((err) => showError(`Không tải được danh sách phòng ban: ${err.message}`))
//     .then(() => Promise.all([loadHistory(1), loadPrices(), loadRatios(), loadPlans()]));
// }
