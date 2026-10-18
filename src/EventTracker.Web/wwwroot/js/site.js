(function () {
    "use strict";

    // ---------- Toasts ----------
    const TOAST_DURATION_MS = 5000;

    function dismissToast(toast) {
        toast.style.transition = "opacity .2s ease";
        toast.style.opacity = "0";
        setTimeout(() => toast.remove(), 200);
    }

    function mountToast(toast) {
        const stack = document.getElementById("toastStack");
        if (!stack) return;

        stack.appendChild(toast);
        toast.hidden = false;
        toast.querySelector("[data-toast-close]")?.addEventListener("click", () => dismissToast(toast));
        setTimeout(() => dismissToast(toast), TOAST_DURATION_MS);
    }

    /** Shows a toast from script, e.g. EventTracker.toast("Kaydedildi"). */
    function showToast(message, type = "success") {
        const toast = document.createElement("div");
        const isError = type === "error";
        toast.className = `app-toast ${isError ? "app-toast-error" : "app-toast-success"}`;
        toast.setAttribute("role", "status");
        toast.innerHTML = `<i class="bi ${isError ? "bi-exclamation-circle" : "bi-check-circle"}"></i><span></span>` +
            `<button type="button" class="btn-close" aria-label="Kapat" data-toast-close></button>`;
        toast.querySelector("span").textContent = message;
        mountToast(toast);
    }

    // ---------- Confirm dialog ----------
    let pendingAction = null;

    function openConfirm(trigger, onAccept) {
        const modalElement = document.getElementById("confirmModal");
        if (!modalElement || !window.bootstrap) {
            if (window.confirm(trigger.dataset.confirm)) onAccept();
            return;
        }

        modalElement.querySelector("#confirmModalTitle").textContent = trigger.dataset.confirmTitle || "Emin misiniz?";
        modalElement.querySelector("[data-confirm-message]").textContent = trigger.dataset.confirm;

        const accept = modalElement.querySelector("[data-confirm-accept]");
        accept.textContent = trigger.dataset.confirmOk || "Onayla";
        accept.className = `btn ${trigger.dataset.confirmVariant === "primary" ? "btn-primary" : "btn-danger"}`;

        pendingAction = onAccept;
        bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    document.addEventListener("click", (event) => {
        const accept = event.target.closest("[data-confirm-accept]");
        if (accept && pendingAction) {
            const action = pendingAction;
            pendingAction = null;
            bootstrap.Modal.getInstance(document.getElementById("confirmModal"))?.hide();
            action();
            return;
        }

        const link = event.target.closest("a[data-confirm]");
        if (link) {
            event.preventDefault();
            openConfirm(link, () => { window.location.href = link.href; });
        }
    });

    // The confirmation text can sit on the form or on the clicked submit button.
    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (form.dataset.confirmed === "true") { delete form.dataset.confirmed; return; }

        const submitter = event.submitter;
        const source = submitter?.dataset.confirm ? submitter : form.dataset.confirm ? form : null;
        if (!source) return;

        event.preventDefault();
        openConfirm(source, () => {
            form.dataset.confirmed = "true";
            submitter ? form.requestSubmit(submitter) : form.requestSubmit();
        });
    });

    // ---------- Loading state ----------
    // Disables the submit button while a POST form is on its way, so it cannot be sent twice.
    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || event.defaultPrevented) return;
        if ((form.method || "get").toLowerCase() !== "post") return;
        if (window.jQuery && jQuery(form).data("validator") && !jQuery(form).valid()) return;

        const button = event.submitter || form.querySelector("button[type=submit]");
        if (!button) return;

        // Keep the clicked button's name/value in the request after it is disabled.
        if (button.name) {
            const hidden = document.createElement("input");
            hidden.type = "hidden";
            hidden.name = button.name;
            hidden.value = button.value;
            form.appendChild(hidden);
        }

        setTimeout(() => {
            button.classList.add("is-loading");
            button.setAttribute("aria-busy", "true");
            button.disabled = true;
        }, 0);
    });

    window.addEventListener("pageshow", () => {
        document.querySelectorAll(".btn.is-loading").forEach((button) => {
            button.classList.remove("is-loading");
            button.removeAttribute("aria-busy");
            button.disabled = false;
        });
    });

    // ---------- Field errors ----------
    // Re-check a field that shows an error as soon as its value changes (typing, pasting or autofill).
    // Otherwise the message only disappears when the field loses focus, which happens on mousedown on the
    // submit button: the button then moves up and the click can miss it.
    document.addEventListener("input", (event) => {
        const field = event.target;
        if (!(field instanceof HTMLElement) || !field.classList.contains("input-validation-error")) return;
        if (!window.jQuery || !field.form || !jQuery(field.form).data("validator")) return;
        jQuery(field).valid();
    });

    // ---------- Event form ----------
    // Moving the start date moves the end date by the same number of days, so a one-day event stays one day.
    document.addEventListener("focusin", (event) => {
        const start = event.target.closest("[data-sync-date]");
        if (start) start.dataset.previous = start.value;
    });

    document.addEventListener("change", (event) => {
        const start = event.target.closest("[data-sync-date]");
        if (!start) return;
        const end = document.querySelector(start.dataset.syncDate);
        const before = start.dataset.previous;
        start.dataset.previous = start.value;
        if (!end || !start.value) return;
        if (!end.value || !before) { end.value = start.value; return; }

        const day = 24 * 60 * 60 * 1000;
        const shift = (Date.parse(start.value) - Date.parse(before)) / day;
        const moved = new Date(Date.parse(end.value) + shift * day);
        end.value = moved.toISOString().slice(0, 10);
    });

    // Shows the picked cover image before the form is sent.
    document.addEventListener("change", (event) => {
        const input = event.target.closest("[data-cover-input]");
        if (!input) return;
        const preview = input.form?.querySelector("[data-cover-preview]");
        const file = input.files?.[0];
        if (!preview || !file || !file.type.startsWith("image/")) return;

        const image = document.createElement("img");
        image.alt = "Seçilen kapak görseli";
        image.src = URL.createObjectURL(file);
        preview.replaceChildren(image);
        const remove = input.form.querySelector("input[name=RemoveCover][type=checkbox]");
        if (remove) remove.checked = false;
    });

    // ---------- Copy to clipboard ----------
    document.addEventListener("click", async (event) => {
        const button = event.target.closest("[data-copy]");
        if (!button) return;
        const source = document.querySelector(button.dataset.copy);
        if (!source) return;
        try {
            await navigator.clipboard.writeText(source.value ?? source.textContent);
            showToast("Bağlantı kopyalandı.");
        } catch {
            source.select?.();
        }
    });

    // ---------- Boot ----------
    document.addEventListener("DOMContentLoaded", () => {
        document.querySelectorAll("[data-toast]").forEach(mountToast);
    });

    window.EventTracker = { toast: showToast };
})();
