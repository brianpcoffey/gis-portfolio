// Shared toast helper used across pages
(function () {
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

        const toast = document.createElement("div");
        toast.className = `toast align-items-center text-bg-${type} border-0 show`;
        toast.setAttribute("role", "alert");
        toast.setAttribute("aria-live", "assertive");
        toast.setAttribute("aria-atomic", "true");
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    <i class="fa-solid ${icon} me-1"></i>${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto"
                        data-bs-dismiss="toast" aria-label="Close"></button>
            </div>`;
        container.appendChild(toast);

        // Animate in
        requestAnimationFrame(() => toast.classList.add("toast-slide-in"));

        setTimeout(() => {
            toast.classList.add("toast-slide-out");
            toast.addEventListener("transitionend", () => toast.remove(), { once: true });
            setTimeout(() => toast.remove(), 500);
        }, 4000);
    }

    // Expose globally
    window.showToast = showToast;
    window.fiberflowToast = function (message, type) {
        const mapped = type === 'error' ? 'danger' : type;
        showToast(message, mapped);
    };
})();