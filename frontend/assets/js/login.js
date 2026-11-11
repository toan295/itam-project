// Đã đăng nhập sẵn thì vào thẳng trang chủ — nhưng chỉ khi máy chủ còn công nhận phiên đó (chạy lại hệ thống = phải đăng nhập lại).
const existingSession = getSession();
if (existingSession?.token) {
  fetch(`${API_BASE_URL}/auth/me`, { headers: { Authorization: `Bearer ${existingSession.token}` } })
    .then((res) => {
      if (res.ok) {
        window.location.href = existingSession.role === "Employee" ? "my-assets.html" : "../index.html";
      } else {
        clearSession();
      }
    })
    .catch(() => { /* máy chủ chưa sẵn sàng: cứ hiện form đăng nhập */ });
}

function showFormError(message) {
  const el = document.getElementById("formError");
  el.textContent = message;
  el.classList.remove("d-none");
}

function clearFormError() {
  document.getElementById("formError").classList.add("d-none");
}

document.getElementById("loginForm").addEventListener("submit", async (e) => {
  e.preventDefault();
  clearFormError();
  try {
    const result = await api.post("/auth/login", {
      email: document.getElementById("loginEmail").value.trim(),
      password: document.getElementById("loginPassword").value,
    });
    setSession(result);
    window.location.href = result.role === "Employee" ? "my-assets.html" : "../index.html";
  } catch (err) {
    showFormError(err.message);
  }
});
