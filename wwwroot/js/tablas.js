(() => {
    if (!window.DataTable) return;

    const idioma = {
        search: 'Buscar:',
        lengthMenu: 'Mostrar _MENU_ filas',
        info: '_START_–_END_ de _TOTAL_ registros',
        infoEmpty: '0 registros',
        infoFiltered: '(de _MAX_ en total)',
        emptyTable: 'No hay registros para mostrar.',
        zeroRecords: 'No hay registros que coincidan con la búsqueda.',
        processing: 'Cargando…',
        paginate: { first: 'Primera', previous: 'Anterior', next: 'Siguiente', last: 'Última' },
        aria: { orderable: 'Ordenar esta columna', orderableReverse: 'Invertir el orden' }
    };

    const telefonos = document.getElementById('tabla-telefonos');
    if (telefonos) {
        new DataTable(telefonos, {
            language: idioma,
            scrollX: true,
            order: [[0, 'asc']],
            columnDefs: [{ targets: -1, orderable: false, searchable: false }]
        });
    }

    const historico = document.getElementById('tabla-historico');
    if (historico) {
        const buscar = document.getElementById('buscar-turnos');
        const estado = document.getElementById('estado-turnos');
        const normalizar = texto => texto.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().trim();
        const tabla = new DataTable(historico, {
            language: idioma,
            scrollX: true,
            layout: { topEnd: null },
            order: [[0, 'desc']],
            columnDefs: [{ targets: [-2, -1], orderable: false }]
        });
        historico.addEventListener('click', evento => {
            const boton = evento.target.closest('[data-detalle-turno]');
            if (!boton) return;
            Swal.fire({
                title: 'Detalle del turno',
                text: `Plazas reservadas: ${boton.dataset.plazas}\n\nTratamientos\n${boton.dataset.tratamientos}`,
                icon: 'info',
                iconColor: '#358184',
                confirmButtonText: 'Cerrar',
                buttonsStyling: false,
                heightAuto: false,
                customClass: { confirmButton: 'btn btn-primary', htmlContainer: 'detalle-turno-texto' }
            });
        });
        let palabras = [];
        tabla.search.fixed('mascota-tutor-estado', (_, datos, indice) => {
            const fila = tabla.row(indice).node();
            const texto = normalizar(fila.querySelector('[data-mascota]').textContent + ' ' +
                fila.querySelector('[data-tutor]').textContent);
            return palabras.every(palabra => texto.includes(palabra)) &&
                (!estado.value || fila.dataset.estado === estado.value);
        });
        function filtrar() {
            palabras = normalizar(buscar.value).split(/\s+/).filter(Boolean);
            tabla.draw();
        }
        buscar.addEventListener('input', filtrar);
        estado.addEventListener('change', filtrar);
        filtrar();
    }

    const auditoria = document.getElementById('tabla-auditoria');
    if (auditoria) {
        const error = document.getElementById('auditoria-error');
        new DataTable(auditoria, {
            language: idioma,
            scrollX: true,
            processing: true,
            serverSide: true,
            searchDelay: 400,
            pageLength: 25,
            lengthMenu: [10, 25, 50, 100],
            order: [[0, 'desc']],
            columnDefs: [
                { targets: '_all', render: DataTable.render.text() },
                { targets: [1, 2, 3, 4, 5, 6], orderable: false }
            ],
            ajax: {
                url: auditoria.dataset.url,
                data: solicitud => {
                    solicitud.search.value = solicitud.search.value.trim().slice(0, 160);
                },
                dataSrc: respuesta => {
                    error.hidden = true;
                    document.getElementById('auditoria-paginacion').classList.add('d-none');
                    return respuesta.data;
                },
                error: () => { error.hidden = false; }
            }
        });
    }
})();
