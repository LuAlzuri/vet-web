// Mensajes compartidos. Usar text evita interpretar como HTML los mensajes del servidor.
window.VetAlertas = {
    confirmar: async texto => {
        const resultado = await Swal.fire({
            title: 'Confirmar operación', text: texto, icon: 'question',
            showCancelButton: true, confirmButtonText: 'Confirmar', cancelButtonText: 'Cancelar',
            focusCancel: true, reverseButtons: true, heightAuto: false,
            buttonsStyling: false,
            customClass: { confirmButton: 'btn btn-primary ms-2', cancelButton: 'btn btn-secondary' }
        });
        return resultado.isConfirmed;
    },
    error: (texto, target = document.body) => Swal.fire({
        title: 'No se pudo completar', text: texto, icon: 'error',
        confirmButtonText: 'Entendido', target, heightAuto: false,
        buttonsStyling: false, customClass: { confirmButton: 'btn btn-primary' }
    }),
    exito: (texto = 'La operación se realizó correctamente.') => Swal.fire({
        title: 'Listo', text: texto, icon: 'success',
        timer: 1100, timerProgressBar: true, showConfirmButton: false, heightAuto: false
    })
};
