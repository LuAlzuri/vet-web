(() => {
    const historico = document.getElementById('historico-turnos');
    const buscar = document.getElementById('buscar-turnos');
    const estado = document.getElementById('estado-turnos');
    const vacio = document.getElementById('historico-vacio');
    const normalizar = texto => texto.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().trim();
    const filas = Array.from(historico.querySelectorAll('tr[data-estado]'), fila => ({
        fila,
        texto: normalizar(fila.querySelector('[data-mascota]').textContent + ' ' +
            fila.querySelector('[data-tutor]').textContent)
    }));

    function filtrar() {
        const palabras = normalizar(buscar.value).split(/\s+/);
        let visibles = 0;
        for (const { fila, texto } of filas) {
            const coincide = palabras.every(palabra => texto.includes(palabra)) &&
                (!estado.value || fila.dataset.estado === estado.value);
            fila.hidden = !coincide;
            if (coincide) visibles++;
        }
        vacio.hidden = visibles > 0;
    }

    buscar.addEventListener('input', filtrar);
    estado.addEventListener('change', filtrar);
    filtrar();
})();
