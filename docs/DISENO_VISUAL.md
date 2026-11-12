# Identidad visual de VeteWeb

Se adapta a la web la identidad de **VeteApp**, usando el nombre visible **VeteWeb**.

## Referencia y recursos

La referencia es `LuAlzuri/tp-final-movil`, en particular:

- `Turnero.App/Components/Pages/Login.razor`: logo de huella bicolor. Se extrajo el mismo SVG, conservando sus trazos y colores, a `wwwroot/images/brand/veteweb-logo.svg`.
- `Turnero.App/wwwroot/images/patita.svg`: huella decorativa reutilizada como `wwwroot/images/brand/huella.svg`.
- `Login.razor.css`, `InicioLayout.razor.css`, `Home.razor.css` y `wwwroot/css/gestion.css`: paleta turquesa y verde oscuro, rosa suave, paneles y tarjetas redondeados, superficies claras y botones amplios.

Colores principales: turquesa `#63d3d5`, verde `#153e42`, menta `#d8f4f2`, rosa `#ddb6b8` y superficie `#eef3f2`. La tipografía usa la familia de sistema, como los estilos Bootstrap del móvil. Los recursos se sirven localmente; no se agregan fuentes ni dependencias externas.

## Aplicación en la web

- Marca compartida en cabecera, ingreso y portada; favicon SVG y color del navegador.
- Portada pública y formulario de ingreso con identidad del móvil, adaptados a escritorio y pantallas pequeñas.
- Paneles de inicio con accesos distintos para Administrador, Veterinario y Tutor.
- Accesos inferiores en móvil según el rol activo, junto con el menú superior desplegable.
- Estilo común para formularios, tablas, tarjetas de perros, modales y mensajes, compatible con Bootstrap del repositorio.
- Nombre y paleta coherentes en el encabezado de los informes PDF.
- Foco de teclado visible, enlace para saltar al contenido y respeto de la preferencia de movimiento reducido.

Se conserva `VetWeb` como nombre técnico del proyecto, solución, namespaces y configuración existente. La marca presentada al usuario es **VeteWeb**. No se renombran rutas, cuentas ni datos, ni se modifica el repositorio móvil.

## Validación

- Compilación y publicación Release correctas.
- Suite completa de integración PostgreSQL con resultado satisfactorio, incluidas las comprobaciones del modelo y DER, permisos, cookies, formularios y API.
- Recorridos reales en Chromium: alta de veterinario por Administrador; altas de tutor y perro por Veterinario; solicitud del Tutor; confirmación y registro de atención; historial, auditoría e informe PDF.
- Portada, ingreso y paneles y menús de los tres roles revisados a 1366 × 900 y 390 × 844. Sin desbordamientos horizontales en esas páginas.
- Confirmación de los colores computados de botones, carga de imágenes y recursos, accesos móviles por rol y ausencia de errores JavaScript.

Las capturas y resultados locales están en `/workspace/.vetweb-visual/artifacts/`. Las pruebas usaron bases temporales; la base de trabajo no fue modificada. No se requieren migraciones. No se realizó despliegue.
