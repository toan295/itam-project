// Nếu đã đăng nhập sẵn (session còn hạn), vào thẳng trang chủ thay vì bắt đăng nhập lại.
const existingSession = getSession();
if (existingSession?.token && (!existingSession.expiresAt || new Date(existingSession.expiresAt) > new Date())) {
  window.location.href = "../index.html";
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
    window.location.href = "../index.html";
  } catch (err) {
    showFormError(err.message);
  }
});
