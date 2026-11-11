// La delegación también funciona con formularios cargados en modales por AJAX.
document.addEventListener('click', event => {
    const boton = event.target.closest('[data-toggle-password]');
    if (!boton) return;

    const campo = document.getElementById(boton.getAttribute('aria-controls'));
    if (!campo) return;

    const mostrar = campo.type === 'password';
    campo.type = mostrar ? 'text' : 'password';
    const etiqueta = mostrar ? 'Ocultar contraseña' : 'Mostrar contraseña';
    boton.setAttribute('aria-label', etiqueta);
    boton.setAttribute('aria-pressed', String(mostrar));
    boton.title = etiqueta;
    boton.querySelector('[data-eye-open]').classList.toggle('d-none', mostrar);
    boton.querySelector('[data-eye-closed]').classList.toggle('d-none', !mostrar);
});
