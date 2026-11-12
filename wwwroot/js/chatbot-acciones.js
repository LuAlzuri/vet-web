(() => {
    "use strict";
    const root = document.querySelector('[data-chatbot][data-herramientas="true"]');
    if (!root) return;
    root.classList.add("chatbot-con-herramientas");
    const container = root.querySelector(".chatbot-herramientas");
    const result = root.querySelector(".chatbot-resultado");
    const search = root.querySelector(".chatbot-busqueda");
    const searchInput = search.querySelector("input");
    const home = root.querySelector("[data-chatbot-inicio]");
    const status = root.querySelector(".chatbot-estado");
    container.hidden = false;
    let loaded = false;
    let busy = false;
    let searchRequest = null;
    function element(tag, text, className) {
        const node = document.createElement(tag);
        if (text !== undefined) node.textContent = text;
        if (className) node.className = className;
        return node;
    }
    function render(data) {
        result.replaceChildren();
        result.append(element("p", data.mensaje));
        for (const table of data.tablas) {
            const wrapper = element("div", undefined, "chatbot-grilla");
            wrapper.tabIndex = 0;
            wrapper.setAttribute("role", "region");
            wrapper.setAttribute("aria-label", table.titulo);
            const grid = element("table", undefined, "table table-sm");
            grid.append(element("caption", table.titulo));
            const head = element("thead");
            const headings = element("tr");
            for (const column of table.columnas) {
                const cell = element("th", column);
                cell.scope = "col";
                headings.append(cell);
            }
            head.append(headings);
            grid.append(head);
            const body = element("tbody");
            if (!table.filas.length) {
                const row = element("tr");
                const cell = element("td", "Sin registros.");
                cell.colSpan = table.columnas.length;
                row.append(cell);
                body.append(row);
            }
            for (const values of table.filas) {
                const row = element("tr");
                for (const value of values) row.append(element("td", value));
                body.append(row);
            }
            grid.append(body);
            wrapper.append(grid);
            result.append(wrapper);
        }
        const buttons = element("div", undefined, "chatbot-opciones");
        for (const action of data.acciones) {
            const button = element("button", action.etiqueta, "btn btn-outline-primary btn-sm");
            button.type = "button";
            if (action.solicitud.accion === "confirmar") button.className = "btn btn-primary";
            button.addEventListener("click", () => execute(action.solicitud));
            buttons.append(button);
        }
        result.append(buttons);
        search.hidden = !data.buscarTutor;
        container.scrollTop = 0;
    }
    async function execute(request) {
        if (busy) return;
        busy = true;
        container.setAttribute("aria-busy", "true");
        container.querySelectorAll("button, input").forEach(node => node.disabled = true);
        status.textContent = request.accion === "confirmar" ? "Guardando solicitud…" : "Consultando…";
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 30000);
        try {
            const response = await fetch(root.dataset.accionesUrl, {
                method: "POST", credentials: "same-origin", signal: controller.signal,
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": root.querySelector('[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify(request)
            });
            if (!response.ok) {
                if (response.status === 401 || response.status === 403) throw new Error("Tu sesión cambió o no tenés permiso. Recargá la página.");
                const problem = await response.json().catch(() => ({}));
                throw new Error(problem.mensaje || "No se pudo realizar la acción. Revisá las opciones e intentá nuevamente.");
            }
            const data = await response.json();
            render(data);
            searchRequest = data.buscarTutor ? { ...request, pagina: 1 } : null;
            status.textContent = "";
            loaded = true;
        } catch (error) {
            status.textContent = error.name === "AbortError"
                ? "La operación tardó demasiado. Consultá tus turnos antes de volver a reservar."
                : error.message;
        } finally {
            clearTimeout(timeout);
            busy = false;
            container.setAttribute("aria-busy", "false");
            container.querySelectorAll("button, input").forEach(node => node.disabled = false);
        }
    }
    home.addEventListener("click", () => execute({ accion: "descartar" }));
    search.addEventListener("submit", event => {
        event.preventDefault();
        if (searchRequest) execute({ ...searchRequest, busqueda: searchInput.value.trim(), pagina: 1 });
    });
    root.querySelector(".chatbot-abrir").addEventListener("click", () => {
        if (!loaded && !root.querySelector(".chatbot-panel").hidden) execute({ accion: "inicio" });
    });
    root.querySelector("[data-chatbot-limpiar]").addEventListener("click", () => execute({ accion: "descartar" }));
})();
