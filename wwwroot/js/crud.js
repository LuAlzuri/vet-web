(() => {
    const modalElement = document.getElementById('modalCrud');
    if (!modalElement) return;
    const modal = new bootstrap.Modal(modalElement);
    const contenido = document.getElementById('contenidoCrud');
    const headers = { 'X-Requested-With': 'XMLHttpRequest' };
    function informar(texto) {
        const target = modalElement.classList.contains('show') ? modalElement : document.body;
        return VetAlertas.error(texto, target);
    }
    async function finalizar() {
        modal.hide();
        await VetAlertas.exito();
        window.location.reload();
    }
    function actualizarCatalogo(select) {
        const contenedor = document.getElementById(select.dataset.catalogoOtro);
        if (!contenedor) return;
        const mostrar = select.value === '-1' && !select.disabled;
        contenedor.hidden = !mostrar;
        const input = contenedor.querySelector('input');
        input.disabled = !mostrar;
        input.required = mostrar;
    }
    contenido.addEventListener('change', event => {
        if (event.target.matches('select[data-catalogo-otro]')) actualizarCatalogo(event.target);
    });
    function preparar(html) {
        contenido.innerHTML = html;
        contenido.querySelectorAll('select[data-catalogo-otro]').forEach(actualizarCatalogo);
        const form = contenido.querySelector('form');
        if (form && window.jQuery?.validator?.unobtrusive) $.validator.unobtrusive.parse(form);
    }
    async function respuesta(response) {
        const destino = new URL(response.url, window.location.origin).pathname;
        if (response.status === 401 || response.redirected && destino.includes('/Identity/Cuenta/Ingresar')) {
            window.location.assign('/Identity/Cuenta/Ingresar');
            throw new Error('Iniciá sesión nuevamente.');
        }
        if (response.redirected && destino.includes('/Cuenta/SeleccionarRol')) {
            window.location.assign('/Cuenta/SeleccionarRol');
            throw new Error('Elegí el rol con el que vas a trabajar.');
        }
        if (response.status === 403 || response.redirected && destino.includes('/Identity/Cuenta/AccesoDenegado'))
            throw new Error('El rol activo no tiene permisos para esta operación.');
        if (!response.ok) {
            const datos = await response.json().catch(() => null);
            throw new Error(datos?.mensaje || 'No se pudo completar la operación.');
        }
        return response;
    }
    document.addEventListener('click', async event => {
        const boton = event.target.closest('[data-form-url], [data-delete-url]');
        if (!boton || boton.disabled) return;
        event.preventDefault();
        boton.disabled = true;
        try {
            if (boton.dataset.formUrl) {
                const response = await respuesta(await fetch(boton.dataset.formUrl, { headers }));
                preparar(await response.text()); modal.show();
            } else {
                if (!await VetAlertas.confirmar(boton.dataset.confirm || '¿Confirmás esta operación?')) return;
                const datos = new FormData(document.getElementById('tokenCrud'));
                const response = await respuesta(await fetch(boton.dataset.deleteUrl, { method: 'POST', headers, body: datos }));
                const resultado = await response.json();
                if (resultado.success) await finalizar();
                else await informar(resultado.mensaje || 'No se pudo completar la operación.');
            }
        } catch (error) { await informar(error.message); }
        finally { boton.disabled = false; }
    });
    document.addEventListener('submit', async event => {
        const form = event.target.closest('form[data-crud]');
        if (!form) return;
        event.preventDefault();
        const boton = form.querySelector('button[type="submit"]');
        if (boton.disabled) return;
        if (window.jQuery?.validator && !$(form).valid()) return;
        boton.disabled = true;
        try {
            const response = await respuesta(await fetch(form.action, { method: 'POST', headers, body: new FormData(form) }));
            if (response.headers.get('content-type')?.includes('application/json')) {
                const datos = await response.json();
                if (datos.success) await finalizar(); else await informar(datos.mensaje);
            } else preparar(await response.text());
        } catch (error) { await informar(error.message); }
        finally { boton.disabled = false; }
    });
})();
