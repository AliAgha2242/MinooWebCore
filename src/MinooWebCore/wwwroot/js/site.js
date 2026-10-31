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

    const mappingModal = document.querySelector("[data-mapping-modal]");
    if (mappingModal) {
        const serverList = mappingModal.querySelector("[data-server-list]");
        const serverCount = mappingModal.querySelector("[data-server-count]");
        const selectionSummary = mappingModal.querySelector("[data-selection-summary]");
        const resultsPanel = mappingModal.querySelector("[data-mapping-results]");
        const runButton = mappingModal.querySelector("[data-run-mapping]");
        const antiForgeryToken = mappingModal.querySelector('input[name="__RequestVerificationToken"]')?.value;
        let mappingServers = [];
        let isSubmittingMapping = false;

        const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, char => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
        }[char]));

        const formatRemaining = seconds => {
            const minutes = Math.max(1, Math.ceil(Number(seconds || 0) / 60));
            const hours = Math.floor(minutes / 60);
            const rest = minutes % 60;
            return hours ? `${hours} ساعت و ${rest} دقیقه` : `${minutes} دقیقه`;
        };

        const selectedIds = () => Array.from(serverList.querySelectorAll('input[type="checkbox"]:checked'))
            .map(input => Number(input.value));

        const updateSelection = () => {
            const count = selectedIds().length;
            selectionSummary.textContent = count
                ? `${count} سرور برای اجرای نگاشت انتخاب شده است.`
                : "هنوز سروری انتخاب نشده است.";
            runButton.disabled = count === 0 || isSubmittingMapping;
        };

        const renderServers = servers => {
            mappingServers = servers || [];
            serverCount.textContent = `${mappingServers.length} سرور`;
            if (!mappingServers.length) {
                serverList.innerHTML = '<p class="mapping-empty">هیچ سروری در تنظیمات پیدا نشد.</p>';
                return;
            }

            serverList.innerHTML = mappingServers.map(server => {
                const limited = !server.canExecute;
                const state = limited
                    ? `محدود تا ${formatRemaining(server.remainingSeconds)}`
                    : "آماده اجرا";
                return `
                    <label class="mapping-server-option ${limited ? "is-limited" : ""}">
                        <input type="checkbox" value="${Number(server.serverId)}" ${limited ? "disabled" : ""}>
                        <span class="mapping-server-copy">
                            <strong>${escapeHtml(server.serverName)}</strong>
                            <small>${escapeHtml(server.databaseName)}</small>
                        </span>
                        <span class="mapping-server-state">${state}</span>
                    </label>`;
            }).join("");

            serverList.querySelectorAll('input[type="checkbox"]').forEach(input =>
                input.addEventListener("change", updateSelection));
            updateSelection();
        };

        const loadServers = async () => {
            serverList.innerHTML = '<p class="mapping-empty">در حال دریافت فهرست سرورها...</p>';
            serverCount.textContent = "در حال دریافت...";
            runButton.disabled = true;
            try {
                const response = await fetch("/api/mapping/servers", { headers: { "Accept": "application/json" } });
                if (!response.ok) throw new Error("دریافت فهرست سرورها ناموفق بود.");
                renderServers(await response.json());
            } catch (error) {
                serverList.innerHTML = `<p class="mapping-empty">${escapeHtml(error.message)}</p>`;
                serverCount.textContent = "خطا";
            }
        };

        const openMapping = async () => {
            mappingModal.hidden = false;
            mappingModal.setAttribute("aria-hidden", "false");
            document.body.style.overflow = "hidden";
            resultsPanel.innerHTML = "";
            await loadServers();
            mappingModal.querySelector("[data-close-mapping]")?.focus();
        };

        const closeMapping = () => {
            if (isSubmittingMapping) return;
            mappingModal.hidden = true;
            mappingModal.setAttribute("aria-hidden", "true");
            document.body.style.overflow = "";
        };

        document.querySelectorAll("[data-open-mapping]").forEach(button =>
            button.addEventListener("click", openMapping));
        mappingModal.querySelectorAll("[data-close-mapping]").forEach(button =>
            button.addEventListener("click", closeMapping));
        document.addEventListener("keydown", event => {
            if (event.key === "Escape" && !mappingModal.hidden) closeMapping();
        });

        runButton.addEventListener("click", async () => {
            const ids = selectedIds();
            if (!ids.length || isSubmittingMapping) return;

            isSubmittingMapping = true;
            runButton.disabled = true;
            runButton.querySelector("span").textContent = "در حال ارسال درخواست...";
            resultsPanel.innerHTML = '<p class="mapping-empty">درخواست‌ها در حال اجرا هستند؛ لطفاً پنجره را نبندید.</p>';

            try {
                const response = await fetch("/api/mapping/execute", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": antiForgeryToken || ""
                    },
                    body: JSON.stringify({ serverIds: ids })
                });
                const payload = await response.json();
                if (!response.ok) throw new Error(payload.message || "اجرای نگاشت ناموفق بود.");

                resultsPanel.innerHTML = (payload || []).map(item => {
                    const statusClass = item.status === "Success" ? "is-success"
                        : item.status === "Limited" ? "is-warning" : "is-error";
                    return `
                        <div class="mapping-result-item ${statusClass}">
                            <strong>${escapeHtml(item.serverName)}</strong>
                            <span>${escapeHtml(item.message)}</span>
                        </div>`;
                }).join("");
                await loadServers();
            } catch (error) {
                resultsPanel.innerHTML = `<div class="mapping-result-item is-error"><strong>خطا</strong><span>${escapeHtml(error.message)}</span></div>`;
            } finally {
                isSubmittingMapping = false;
                runButton.querySelector("span").textContent = "اجرای نگاشت سرورهای انتخاب‌شده";
                updateSelection();
            }
        });
    }

})();