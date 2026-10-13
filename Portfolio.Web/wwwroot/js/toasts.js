// Shared toast helper used across pages
(function () {
    const AUTO_HIDE_MS = 6000;

    function createToastContainer() {
        const c = document.createElement("div");
        c.id = "toastContainer";
        c.className = "toast-container position-fixed bottom-0 end-0 p-3";
        c.style.zIndex = "1090";
        document.body.appendChild(c);
        return c;
    }

    function showToast(message, type = "success") {
        const container = document.getElementById("toastContainer") || createToastContainer();
        const iconMap = {
            success: "fa-circle-check",
            danger: "fa-circle-xmark",
            warning: "fa-triangle-exclamation",
            info: "fa-circle-info"
        };
        const icon = iconMap[type] || iconMap.info;

        // Identical toasts don't stack: reuse the live one (timed types restart their timer).
        const key = type + "|" + message;
        for (const live of container.children) {
            if (live.dataset.key === key && !live.classList.contains("toast-slide-out")) {
                if (typeof live._restart === "function") { live._restart(); }
                return;
            }
        }

        const assertive = type === "danger" || type === "warning";
        const toast = document.createElement("div");
        toast.className = `toast align-items-center text-bg-${type} border-0 show`;
        toast.dataset.key = key;
        toast.setAttribute("role", assertive ? "alert" : "status");
        // Errors/warnings interrupt (assertive); routine success/info announce politely.
        toast.setAttribute("aria-live", assertive ? "assertive" : "polite");
        toast.setAttribute("aria-atomic", "true");

        // Build via DOM so the message is inserted as text, never HTML — any server- or
        // user-derived string is safe (no injection through the toast body).
        const flex = document.createElement("div");
        flex.className = "d-flex";
        const body = document.createElement("div");
        body.className = "toast-body";
        const iconEl = document.createElement("i");
        iconEl.className = `fa-solid ${icon} me-1`;
        iconEl.setAttribute("aria-hidden", "true");
        body.appendChild(iconEl);
        body.appendChild(document.createTextNode(message));
        const closeBtn = document.createElement("button");
        closeBtn.type = "button";
        // White glyph only on the dark fills (success, danger); warning/info fills are light.
        const whiteClose = type === "success" || type === "danger";
        closeBtn.className = "btn-close" + (whiteClose ? " btn-close-white" : "") + " me-2 m-auto";
        // WCAG 2.5.8: at least 24x24 CSS px target.
        closeBtn.style.boxSizing = "border-box";
        closeBtn.style.minWidth = "24px";
        closeBtn.style.minHeight = "24px";
        closeBtn.setAttribute("aria-label", "Close");
        flex.appendChild(body);
        flex.appendChild(closeBtn);
        toast.appendChild(flex);
        container.appendChild(toast);

        // Animate in
        requestAnimationFrame(() => toast.classList.add("toast-slide-in"));

        function dismiss() {
            clearTimeout(timer);
            toast.classList.add("toast-slide-out");
            toast.addEventListener("transitionend", () => toast.remove(), { once: true });
            setTimeout(() => toast.remove(), 500);
        }
        closeBtn.addEventListener("click", dismiss);

        // Danger and warning persist until dismissed (WCAG 2.2.1). Success and info last
        // 6 s and pause while the pointer or keyboard focus is inside the toast.
        let timer = null;
        if (!assertive) {
            let remaining = AUTO_HIDE_MS;
            let startedAt = 0;
            const start = () => {
                startedAt = Date.now();
                timer = setTimeout(dismiss, remaining);
            };
            const pause = () => {
                if (timer === null) { return; }
                clearTimeout(timer);
                timer = null;
                remaining = Math.max(1000, remaining - (Date.now() - startedAt));
            };
            const resume = () => { if (timer === null && !toast.matches(":hover, :focus-within")) { start(); } };
            toast.addEventListener("mouseenter", pause);
            toast.addEventListener("mouseleave", resume);
            toast.addEventListener("focusin", pause);
            toast.addEventListener("focusout", resume);
            toast._restart = function () {
                if (timer !== null) { clearTimeout(timer); timer = null; }
                remaining = AUTO_HIDE_MS;
                if (!toast.matches(":hover, :focus-within")) { start(); }
            };
            start();
        }
    }

    // Expose globally
    window.showToast = showToast;

    // Themed confirm dialog — returns a Promise<boolean>. Replaces window.confirm so
    // destructive actions use the site's Bootstrap modal (themed, dark-mode aware) instead
    // of the browser's unstyled dialog. The message is inserted as text (no markup injection).
    let confirmSeq = 0;
    function confirmDialog(message, opts) {
        opts = opts || {};
        return new Promise(function (resolve) {
            const wrap = document.createElement("div");
            wrap.className = "modal fade";
            wrap.setAttribute("tabindex", "-1");
            wrap.setAttribute("aria-modal", "true");
            wrap.setAttribute("role", "dialog");
            const messageId = "confirmDialogMessage" + (++confirmSeq);
            wrap.setAttribute("aria-labelledby", messageId);

            const dialog = document.createElement("div");
            dialog.className = "modal-dialog modal-dialog-centered";
            const content = document.createElement("div");
            content.className = "modal-content theme-surface";

            const body = document.createElement("div");
            body.className = "modal-body";
            const p = document.createElement("p");
            p.className = "mb-0";
            p.id = messageId;
            p.textContent = message;
            body.appendChild(p);

            const footer = document.createElement("div");
            footer.className = "modal-footer";
            const cancelBtn = document.createElement("button");
            cancelBtn.type = "button";
            cancelBtn.className = "btn btn-secondary";
            cancelBtn.textContent = opts.cancelText || "Cancel";
            const okBtn = document.createElement("button");
            okBtn.type = "button";
            okBtn.className = "btn " + (opts.confirmClass || "btn-danger");
            okBtn.textContent = opts.confirmText || "Delete";
            footer.appendChild(cancelBtn);
            footer.appendChild(okBtn);

            content.appendChild(body);
            content.appendChild(footer);
            dialog.appendChild(content);
            wrap.appendChild(dialog);
            document.body.appendChild(wrap);

            const modal = new bootstrap.Modal(wrap);
            let result = false;
            okBtn.addEventListener("click", function () { result = true; modal.hide(); });
            cancelBtn.addEventListener("click", function () { modal.hide(); });
            wrap.addEventListener("hidden.bs.modal", function () {
                wrap.remove();
                resolve(result);
            });
            modal.show();
            // Focus the safe choice (Cancel), not the destructive one.
            setTimeout(function () { cancelBtn.focus(); }, 150);
        });
    }
    window.confirmDialog = confirmDialog;
})();