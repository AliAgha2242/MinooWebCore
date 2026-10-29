(() => {
    const toggle = document.querySelector("[data-password-toggle]");
    const password = document.querySelector("#Password");
    if (!toggle || !password) return;

    toggle.addEventListener("click", () => {
        const show = password.type === "password";
        password.type = show ? "text" : "password";
        toggle.setAttribute("aria-label", show ? "مخفی کردن رمز عبور" : "نمایش رمز عبور");
        toggle.classList.toggle("is-visible", show);
    });
})();
