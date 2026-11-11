# Verificación de cierre del hito

Fecha: 30/10/2026. Rama: codex/entidades-y-migraciones.

## Alcance de la entrega

EF Core y PostgreSQL, Identity, cuentas y perfiles Tutor/Veterinario, CRUDs principales, catálogos contextuales, cupos, circuito de turnos, historial, fotos privadas y tarjetas de perros, selección de rol por sesión, SweetAlert2 e inicio público adaptable.

Se incluyen las migraciones AgregarEstadoCuenta, CargarEstadosTurno, PermitirTurnoSinVeterinario y AgregarFotoPerro, además de las entidades ya versionadas. No se agrega una migración por cambios de estilo o validación.

## Pruebas ejecutadas

- Integración ejecutada dos veces con resultado satisfactorio. La conexión local se utilizó para crear bases temporales; se eliminaron al finalizar. Las pruebas no modificaron la base de trabajo.
- Altas, ediciones y bajas por HTTP de cuentas, teléfonos, perros, asociaciones, catálogos y cupos; servicios e historial.
- Solicitud, confirmación, rechazo, finalización y cancelación; devolución única de cupos y reservas simultáneas para la última plaza.
- Permisos Tutor/Veterinario, pertenencia de perros, agenda privada por profesional y turnos compartidos con No especifica.
- Cuenta con dos roles: selección obligatoria, cambio sin reingreso, menús exclusivos y bloqueo de URLs del otro perfil.
- Baja de cuentas, bloqueo con sesión abierta, antifalsificación y protección de la cuenta propia.
- Fotos: carga, reemplazo, conservación, límite y formatos, acceso privado y datos de tarjetas.
- Catálogos: Otro contextual, creación y reutilización, validaciones y estados fijos.
- Compilación de solución en Release: cero errores y cero advertencias.
- git diff --check: sin errores de espacios; avisos de conversión LF/CRLF de Git.

Comandos reproducibles:

```powershell
dotnet run --project Tests/Integracion/Integracion.csproj --configuration VerificacionIdentity --no-restore
dotnet build VetWeb.sln --configuration Release --no-restore
```

VerificacionIdentity es una configuración del proyecto de pruebas, no una configuración de la solución. Las pruebas requieren PostgreSQL con permiso para crear bases y el puerto 5057 libre.

## Revisión en navegador

Se ingresó con las tres cuentas demo locales. Se comprobó el selector de la cuenta doble y el cambio de Tutor a Veterinario sin contraseña. Se revisaron inicio público, ingreso, menús, tarjetas de perros, agenda, selector de rol y formularios de perro, turno y cuenta.

Se inspeccionaron capturas en escritorio de 1366 × 900 y celular emulado de 390 × 844. El inicio no presentó desbordamiento horizontal y su foto cargó correctamente en ambos tamaños. Los modales de perro y turno se adaptaron al ancho móvil; las tablas anchas conservan desplazamiento horizontal. Esto no sustituye una prueba en un teléfono físico ni una revisión de todos los navegadores.

La revisión local fue de consulta y formularios sin guardado; las operaciones completas se probaron en la base temporal. Se cerró la sesión de prueba y se restauró el tamaño del navegador.

## Correcciones de cierre

Se agregaron mensajes Required explícitos en español para campos numéricos obligatorios de DTOs. El formulario vacío de turno mostraba mensajes en inglés; se verificó en la versión Release que Perro y Cupo diario ahora muestran «Este campo es obligatorio.». Veterinario sigue siendo opcional para permitir No especifica. Se actualizaron las referencias antiguas al menú Catálogos.

## Datos demo locales

Las cuentas demo se conservaron. Los datos originales ya fueron modificados: el tutor demo no tenía perros activos, la cuenta doble tenía a Copito y los turnos originales estaban cancelados. No se restablecieron nombres, perros, cupos ni turnos. PRUEBA_ROLES.md describe la semilla original y esta diferencia. Los escenarios completos reproducibles usan sus propios datos temporales.

## Pendiente fuera del hito

Auditoría, vetos manuales/automáticos y reportes PDF. La revisión y fusión con main quedan a cargo del usuario. No se realizó despliegue. Para publicar deben configurarse la conexión del entorno, aplicarse migraciones y respaldarse tanto PostgreSQL como App_Data/Perros.
