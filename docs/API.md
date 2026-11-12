# API de VetWeb

API del backend web, independiente del backend móvil. Las rutas usan la cookie de Identity de VetWeb y el rol activo de la sesión. Todas las operaciones POST, PUT y DELETE requieren antifalsificación (encabezado `RequestVerificationToken` para JSON y multipart). El cambio de rol conserva el flujo existente de `Cuenta/SeleccionarRol` y `Cuenta/CambiarRol`.

Las colecciones paginadas reciben `pagina` (1 a 1000000) y `tamano` (1 a 100). Perros, historial, tutores y veterinarios usan tamaño 20 por defecto; los catálogos usan 100. Se ordenan de manera estable antes de paginar. Los cupos y las asociaciones conservan la consulta completa del módulo. Las respuestas son DTOs; no se serializan entidades Identity ni sus credenciales.

## Inventario y comparación con el móvil

Referencia revisada: controladores de `LuAlzuri/tp-final-movil/turnero_peluqueria`. Los permisos siguientes son los de **VetWeb**.

| Módulo | Rutas de VetWeb | Acceso y adaptación |
| --- | --- | --- |
| Perros | `GET, POST /api/perros`; `GET, PUT, DELETE /api/perros/{id}` | Lectura: Veterinario y Tutor propietario. Escritura: Veterinario. Equivalente al módulo móvil; listado paginado y filtro opcional `tutorId`, siempre dentro de la visibilidad del usuario. |
| Historial | `GET /api/perros/{id}/historial` | Tutor propietario; Veterinario recibe solo sus turnos y los de No especifica. Conserva la consulta tras la baja lógica del perro. |
| Imágenes | `GET, POST, DELETE /api/perros/{perroId}/imagen` | Lectura privada ya existente; escritura Veterinario, multipart con campo `Archivo`, validación de formato y tamaño del servicio existente. |
| Patologías del perro | `GET, POST /api/perros/{perroId}/patologias`; `PUT, DELETE /api/perros/{perroId}/patologias/{patologiaId}` | Lectura de las asociaciones: Veterinario y Tutor propietario. Escritura: Veterinario. La ruta fija el perro y la asociación; la web conserva además su contexto de formulario protegido. |
| Tutores | `GET, POST /api/tutores`; `GET, PUT, DELETE /api/tutores/{id}` | Veterinario. Usa CuentaService y las mismas reglas de Identity, vinculación, protección de cuentas y baja lógica. |
| Teléfonos | `GET, POST /api/tutores/{tutorId}/telefonos`; `GET, PUT, DELETE /api/tutores/{tutorId}/telefonos/{id}` | Veterinario. Las rutas anidadas no permiten operar sobre un teléfono de otro tutor. |
| Veterinarios | `GET, POST /api/veterinarios`; `GET, PUT, DELETE /api/veterinarios/{id}` | Administrador activo. Reutiliza CuentaService; la baja desactiva la cuenta compartida con todos sus perfiles según las reglas existentes. |
| Servicios | `GET, POST /api/servicios`; `GET, PUT, DELETE /api/servicios/{id}` | Administrador y Veterinario gestionan; Tutor solo lee. |
| Grupos y tamaños | `GET, POST /api/grupos-etarios`, `/api/tamanios`; `GET, PUT, DELETE` con `/{id}` | Administrador y Veterinario gestionan; Tutor solo lee. Acceso web compartido desde Catálogos. |
| Patologías y tratamientos | `GET, POST /api/patologias`, `/api/tratamientos`; `GET, PUT, DELETE` con `/{id}` | Administrador y Veterinario gestionan. Tutor consulta únicamente los datos de sus recursos privados. |
| Estados | `GET /api/estados`, `GET /api/estados/{id}` | Administrador, Veterinario y Tutor consultan la API. Pantalla de gestión para los dos primeros, solo lectura: IDs fijos de EstadosTurno. Los métodos API de escritura no existen. |
| Cupos | `GET, POST /api/cupos-diarios`; `GET, PUT, DELETE /api/cupos-diarios/{id}` | Tutor consulta cupos futuros disponibles; Veterinario administra. `/api/cupos` y sus rutas por ID son alias compatibles. `incluirPasados=true` en el listado solo amplía la consulta de Veterinario. |
| Turnos | `GET, POST /api/turnos`; `GET, PUT /api/turnos/{id}` | Solicitud por Tutor, edición por Veterinario autorizado mediante TurnoService. La edición no cambia perro, cupo ni profesional. Listado y detalle conservan el filtro por recurso. |
| Circuito de turnos | `POST /api/turnos/presencial`; `POST /api/turnos/{id}/confirmar`, `/rechazar`, `/cancelar`, `/completar` | Servicios compartidos con la web: cupos, vacunas, duplicados, vetos, auditoría y registro de atención. VetWeb usa `confirmar`, equivalente a `aprobar` en el móvil. |
| Servicios del turno | `GET, POST /api/turnos/{turnoId}/servicios`; `PUT, DELETE /api/turnos/{turnoId}/servicios/{servicioId}` | Lectura según la visibilidad del turno. Escritura por Veterinario autorizado y solo con turno activo. No permite borrar servicios realizados ni el último servicio. La sustitución de servicios por Tutor que existe en el móvil no forma parte del flujo web. |
| Atención | `GET /api/turnos/{id}/atencion` | Lectura privada con los servicios realizados y tratamientos con motivos. El registro definitivo se confirma mediante `/completar`, como en el hito anterior. |
| Vetos | `GET, POST /api/tutores/{tutorId}/vetos`; `GET /api/tutores/{tutorId}/vetos/{vetoId}`; `POST .../{vetoId}/levantar` | Veterinario; motivo obligatorio para la operación manual. Conserva veto automático, historial y reserva presencial de VetWeb. |
| Auditoría | `GET /api/auditoria?pagina=1&tutorId=...` | Administrador activo. Corresponde a movimientos del móvil; VetWeb conserva su DTO y ruta existentes. El registro técnico de errores del móvil no tiene un módulo equivalente en el modelo web. |
| Informes | `GET /api/reportes/frecuencia-solicitudes`, `/servicios-completos`, `/servicios-incompletos`; las mismas rutas con `/datos` para JSON | Administrador activo, con `desde` y `hasta`. PDF y grillas basados en la auditoría de VetWeb. |
| Autenticación | Rutas web de Identity y Cuenta | Se conserva el ingreso por formulario, cookie, antifalsificación y selección de rol de VetWeb. No se copia el inicio de sesión independiente del móvil. |

## Cuerpos de escritura

Los identificadores de las rutas son autoritativos. En los DTOs que también admiten `id`, debe omitirse/enviarse cero o coincidir con la ruta de edición. Los campos de contexto web no son necesarios para las rutas anidadas de la API.

**Perros:** JSON con `nombre`, `fechaNacimiento` (`yyyy-MM-dd`), `tutorId`, `grupoEtarioId`, `tamanioId`, `reactivo` y `calendarioVacunasCompleto`. La foto se carga por la ruta específica de imagen. No se acepta una fecha futura ni referencias inválidas, y no se puede trasladar un perro con turnos históricos a otro tutor. La baja exige resolver los turnos activos y conserva historial e imágenes privadas.

**Perfiles:** CuentaDto con `nombre`, `apellido`, `email`, `password` para el alta, `vincularExistente` para vincular un perfil y `matricula` para Veterinario. La edición conserva la contraseña. Se mantienen los controles de cuenta propia, último veterinario activo, cuentas administrativas y turnos pendientes. La respuesta contiene únicamente ID de perfil, nombre, apellido, correo y matrícula cuando corresponde.

**Teléfonos:** `nombrePropietario`, `numero`. El tutor proviene de la ruta.

**Catálogos:** `descripcion`. Se normalizan los espacios y se rechazan duplicados sin distinguir mayúsculas. Las altas usan el mismo bloqueo transaccional que las opciones contextuales de la web. Las FK impiden borrar opciones referenciadas.

**Patologías del perro:** en el alta, `patologiaId`, `controlado`, `observacion`; para crear una opción contextual se usa `patologiaId: -1` y `nuevaPatologia`. En la edición, la patología proviene de la ruta y no se cambia su clave. Las altas nuevas y su asociación se guardan en la misma transacción.

**Cupos:** `fechaCupo`, `numeroCuposDisponibles`. La disponibilidad es la cantidad libre (igual que en el formulario web). No se publican fechas pasadas, no se cambia la fecha ni se elimina un cupo con turnos, y la edición se coordina con las reservas mediante bloqueo de fila.

**Servicios del turno:** en el alta, `servicioId` y `completado`; en la edición, `completado` y opcionalmente el mismo `servicioId` de la ruta. No cambia las plazas. El bloqueo del turno coordina la edición con el registro de atención.

El contrato de reserva y registro clínico, incluidos los cuerpos de `/confirmar` y `/completar`, está descrito en el README.

## Respuestas y pruebas

- `201` y `Location` para altas; `204` para ediciones, bajas y transiciones correctas.
- `400` para entradas inválidas, referencias o reglas de negocio incumplidas, duplicados y falta de antifalsificación.
- `401` para acceso anónimo a una ruta protegida; `403` para un rol activo sin permiso.
- `404` para recursos inexistentes u ocultos al usuario en las consultas privadas y asociaciones anidadas.
- `405` para escrituras de estados, que son inmutables.

Las operaciones administrativas sobre un recurso de otro profesional pueden devolver `403`; sus consultas privadas devuelven `404`. Los errores mantienen el filtro de la web y la validación automática de los controladores API.

`Tests/Integracion/ApiCoberturaPruebas.cs` comprueba los nuevos CRUDs con clientes HTTP autenticados, roles activos, seguimiento de Location, validaciones, pertenencia de perros y asociaciones, bajas lógicas, historial, catálogos referenciados, cupos con turnos, servicios realizados, recursos finalizados y antifalsificación. La suite completa también verifica las reglas de reservas, concurrencia, fotos, atención, vetos y reportes. Todas las pruebas usan PostgreSQL temporal creado desde cero y eliminado al terminar.

No se agregan entidades ni migraciones para este hito. La extracción de servicios conserva los formularios y reglas de la web.
