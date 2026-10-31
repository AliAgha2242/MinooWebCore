(() => {
    const toggle = document.querySelector("[data-password-toggle]");
    const password = document.querySelector("#Password");
    if (toggle && password) {
        toggle.addEventListener("click", () => {
            const show = password.type === "password";
            password.type = show ? "text" : "password";
            toggle.setAttribute("aria-label", show ? "مخفی کردن رمز عبور" : "نمایش رمز عبور");
            toggle.classList.toggle("is-visible", show);
        });
    }

    const profilePage = document.querySelector(".profile-page");
    const theme = localStorage.getItem("minoo-theme") || "dark";
    const icon = localStorage.getItem("minoo-profile-icon") || "م";
    document.documentElement.dataset.theme = theme;
    if (profilePage) profilePage.dataset.theme = theme;

    document.querySelectorAll("[data-user-avatar]").forEach(item => item.textContent = icon);
    document.querySelectorAll("[data-theme-value]").forEach(button => {
        button.classList.toggle("selected", button.dataset.themeValue === theme);
        button.addEventListener("click", () => {
            const value = button.dataset.themeValue;
            localStorage.setItem("minoo-theme", value);
            document.documentElement.dataset.theme = value;
            if (profilePage) profilePage.dataset.theme = value;
            document.querySelectorAll("[data-theme-value]").forEach(item => item.classList.toggle("selected", item.dataset.themeValue === value));
        });
    });
    document.querySelectorAll("[data-icon-value]").forEach(button => {
        button.classList.toggle("selected", button.dataset.iconValue === icon);
        button.addEventListener("click", () => {
            const value = button.dataset.iconValue;
            localStorage.setItem("minoo-profile-icon", value);
            document.querySelectorAll("[data-user-avatar]").forEach(item => item.textContent = value);
            document.querySelectorAll("[data-icon-value]").forEach(item => item.classList.toggle("selected", item.dataset.iconValue === value));
        });
    });
})();