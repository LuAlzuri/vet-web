(() => {
    "use strict";
    const root = document.querySelector("[data-chatbot]");
    if (!root) return;
    const open = root.querySelector(".chatbot-abrir");
    const panel = root.querySelector(".chatbot-panel");
    const form = root.querySelector("form");
    const input = root.querySelector("textarea");
    const log = root.querySelector(".chatbot-mensajes");
    const status = root.querySelector(".chatbot-estado");
    const send = form.querySelector('[type="submit"]');
    const clear = root.querySelector("[data-chatbot-limpiar]");
    let history = [];
    let busy = false;
    function close() {
        panel.hidden = true;
        open.setAttribute("aria-expanded", "false");
        open.focus();
    }
    open.addEventListener("click", () => {
        if (!panel.hidden) return close();
        panel.hidden = false;
        open.setAttribute("aria-expanded", "true");
        input.focus();
    });
    root.querySelector("[data-chatbot-cerrar]").addEventListener("click", close);
    root.addEventListener("keydown", event => {
        if (event.key === "Escape" && !panel.hidden) close();
    });
    function append(role, text) {
        const message = document.createElement("p");
        message.className = "chatbot-mensaje chatbot-" + role;
        const label = document.createElement("strong");
        label.textContent = role === "user" ? "Vos: " : "Asistente: ";
        message.append(label, document.createTextNode(text));
        log.append(message);
        while (log.children.length > 24) log.firstElementChild.remove();
        log.scrollTop = log.scrollHeight;
    }
    clear.addEventListener("click", () => {
        if (busy) return;
        history = [];
        log.replaceChildren();
        input.value = "";
        status.textContent = "Conversación reiniciada.";
        input.focus();
    });
    form.addEventListener("submit", async event => {
        event.preventDefault();
        const text = input.value.trim();
        if (busy || !text) return;
        busy = true;
        send.disabled = clear.disabled = input.disabled = true;
        status.textContent = "Enviando…";
        append("user", text);
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 30000);
        try {
            const response = await fetch(root.dataset.url, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": form.querySelector('[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({ mensaje: text, historial: history.slice(-12) }),
                signal: controller.signal
            });
            if (!response.ok) {
                if (response.status === 401 || response.status === 403) throw new Error("Tu sesión cambió. Recargá la página para continuar.");
                if (response.status === 400) throw new Error("No se pudo validar el mensaje. Revisalo o recargá la página.");
                throw new Error("No pudimos enviar el mensaje. Intentá nuevamente.");
            }
            const data = await response.json();
            if (typeof data.mensaje !== "string" || !data.mensaje.trim()) throw new Error("El asistente no devolvió una respuesta válida.");
            append("assistant", data.mensaje);
            // Las respuestas del modo sin conexión no forman parte del contexto futuro.
            if (data.conectado) history = [...history, { rol: "user", texto: text }, { rol: "assistant", texto: data.mensaje }].slice(-12);
            input.value = "";
            status.textContent = data.conectado ? "" : "Modo sin conexión.";
        } catch (error) {
            status.textContent = error.name === "AbortError" ? "La consulta tardó demasiado. Intentá nuevamente." : error.message;
        } finally {
            clearTimeout(timeout);
            busy = false;
            send.disabled = clear.disabled = input.disabled = false;
            if (!panel.hidden) input.focus();
        }
    });
})();
