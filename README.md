# VeteWeb

Aplicación ASP.NET Core MVC para gestionar tutores, perros y turnos de peluquería canina de una veterinaria. Usa Identity, PostgreSQL, EF Core y Bootstrap local.

## Identidad visual

La web usa la marca **VeteWeb**, con el logo y la estética de VeteApp adaptados a escritorio y móvil: turquesa, verde oscuro, rosa suave y tarjetas redondeadas. Ver [referencias, alcance y validación visual](docs/DISENO_VISUAL.md). `VetWeb` se conserva como nombre técnico de la solución y los namespaces.

## Estado actual

CRUDs principales implementados con acciones en español, DTOs de formulario, vistas parciales, modales Bootstrap y AJAX. Los controladores consultan EF Core directamente; CuentaService concentra las operaciones de Identity y TurnoService las reservas y transiciones con transacciones.

El repositorio conserva el historial de migraciones. Las cuentas y los perfiles disponibles dependen de cada instalación. Los seis estados del circuito se cargan por migración y no se pueden modificar desde la aplicación. Servicios conserva su CRUD en el menú; los demás catálogos se eligen y amplían desde los formularios correspondientes.

VeteWeb admite vetos manuales y automáticos, reservas presenciales y auditoría de turnos y vetos. Administrador dispone de grillas e informes PDF por período.

## Abrir y ejecutar

Abrir VetWeb.sln en Visual Studio con la carga de trabajo Desarrollo de ASP.NET y web y soporte para .NET 8. Detener cualquier instancia anterior antes de compilar para evitar archivos bloqueados.

```powershell
dotnet restore
dotnet tool restore
dotnet build VetWeb.sln
dotnet run --project VetWeb.csproj --launch-profile http
```

Abrir http://localhost:5043 e iniciar sesión con la cuenta de la veterinaria. Para HTTPS, configurar el certificado local con `dotnet dev-certs https --trust` y usar el perfil https.

## Configuración local

En Visual Studio: clic derecho en VetWeb y Administrar secretos de usuario. Las credenciales no deben guardarse en archivos versionados.

```json
{
  "ConnectionStrings:DefaultConnection": "Host=localhost;Port=5432;Database=vet_web;Username=TU_USUARIO;Password=TU_CLAVE_POSTGRES"
}
```

ASP.NET Core carga los secretos en Development. En otros entornos puede usarse ConnectionStrings__DefaultConnection. Los secretos de usuario son locales y no están cifrados.

Para una instalación nueva, configurar la conexión y aplicar las migraciones:

```powershell
dotnet tool run dotnet-ef database update --project VetWeb.csproj
```

Este comando modifica la base indicada. Revisar el destino antes de ejecutarlo. Las migraciones no se aplican automáticamente al arrancar. CargarEstadosTurno reserva los IDs 1 a 6: si una base externa ya usa esos IDs con otros datos, debe reconciliarse antes de aplicar esa migración.

## Cuenta veterinaria inicial

Solo para la primera instalación, agregar temporalmente los siguientes secretos, con valores propios:

```json
{
  "VeterinarioInicial:Email": "TU_CORREO",
  "VeterinarioInicial:Nombre": "TU_NOMBRE",
  "VeterinarioInicial:Apellido": "TU_APELLIDO",
  "VeterinarioInicial:Password": "TU_CONTRASEÑA_INICIAL"
}
```

Conservar también la conexión dentro del mismo objeto JSON. Ejecutar:

```powershell
dotnet run --project VetWeb.csproj --launch-profile http -- --crear-veterinario
```

El comando crea la cuenta de Identity y le asigna el rol Veterinario; no crea el perfil profesional ni su matrícula. En esta base local el perfil inicial ya está creado. En otra instalación deberá completarse antes de asignar turnos a ese profesional. El comando termina sin iniciar el servidor. Repetirlo no cambia datos ni contraseña de la cuenta existente. Quitar los secretos VeterinarioInicial después de la creación y conservar la conexión.

## Cuenta administrativa inicial

La migración `AgregarRolAdministrador` agrega el rol a Identity. Una vez aplicada, configurar en los secretos de usuario `AdministradorInicial:Email`, `AdministradorInicial:Nombre`, `AdministradorInicial:Apellido` y `AdministradorInicial:Password`. Usar un correo nuevo y una contraseña que cumpla la política de Identity. Ejecutar:

```powershell
dotnet run --project VetWeb.csproj --launch-profile http -- --crear-administrador
```

El comando crea la primera cuenta administrativa y termina. Después se pueden quitar esos secretos de inicialización. Las cuentas administrativas quedan protegidas frente a ediciones, vinculaciones y bajas desde los formularios de perfiles.

## Pantallas y permisos

Administrador gestiona veterinarios y consulta la auditoría. Su menú y sus permisos se habilitan al elegir ese rol, igual que para Tutor y Veterinario.

| Sección | Veterinario | Tutor |
| --- | --- | --- |
| Tutores y teléfonos | Alta, consulta, edición y baja | Sin acceso administrativo |
| Perros | Alta, consulta, edición y baja lógica | Consulta de sus perros |
| Patologías por perro | Alta, consulta, edición y eliminación | Sin administración |
| Servicios | CRUD desde el menú | Elegir servicios existentes al solicitar turno |
| Otros catálogos | Elegir o agregar una opción desde el formulario correspondiente | Solo opciones existentes en las funciones permitidas |
| Estados | Consulta fija y acciones del circuito | Consulta del estado de sus turnos |
| Cupos diarios | Publicar, consultar, editar disponibilidad y eliminar sin referencias | Consultar cupos futuros disponibles |
| Turnos | Solicitar y administrar los asignados al veterinario o sin profesional especificado | Solicitar, consultar y cancelar sus turnos |
| Servicios por turno | Agregar, consultar, editar y eliminar según estado | Consultar servicios de sus turnos |
| Historial | Consulta administrativa | Solo sus perros |

Los endpoints usan Listado, ObtenerFormulario, Guardar y Eliminar, o las acciones del circuito de turnos. Los POST exigen token antifalsificación. El registro público de Identity sigue deshabilitado. Sus páginas están en Areas/Identity/Pages/Account con rutas públicas /Identity/Cuenta/Ingresar, /Salir y /AccesoDenegado.

## Cuentas y baja lógica

ApplicationUser.Activo gobierna el acceso completo. Dar de baja un tutor o veterinario desactiva su cuenta para todos sus roles y marca como eliminado el perfil seleccionado. El ingreso rechaza cuentas inactivas y el middleware cierra sus sesiones en las siguientes solicitudes. La baja no elimina usuarios ni datos históricos.

No se puede dar de baja la cuenta propia, el último veterinario activo ni una cuenta con turnos activos pendientes de resolver. No hay pantalla de restauración en esta entrega. Al crear otro perfil para una cuenta existente, marcar Vincular una cuenta existente e indicar su correo: se conservan sus datos y contraseña y se agrega el rol y perfil correspondiente. Editar nombre, apellido o correo afecta la cuenta compartida por todos sus perfiles. La edición no cambia contraseñas.

Las cuentas nuevas exigen contraseña inicial de al menos 8 caracteres, con mayúscula, minúscula, número y símbolo. Identity guarda un hash. No se incluyen contraseñas operativas en el repositorio. El bloqueo por intentos fallidos está configurado en cinco intentos durante cinco minutos.

## Circuito de turnos

Los estados con IDs fijos son Solicitado (1), Confirmado (2), Rechazado (3), Completado (4), Cancelado por tutor (5) y Cancelado por veterinario (6). No pueden agregarse, eliminarse ni renombrarse estados desde la aplicación, incluso mediante llamadas directas a los endpoints. Solo cambian los estados de los turnos mediante las acciones del circuito.

Una solicitud consume un cupo y debe incluir al menos un servicio. Se verifica pertenencia del perro, actividad de las cuentas, disponibilidad, fecha y ausencia de otro turno activo para el perro en esa misma fecha. Un cachorro menor de 45 días en la fecha del cupo y con vacunas incompletas no puede reservar. Los turnos nuevos quedan solicitados; el veterinario los confirma o rechaza. Un turno confirmado puede completarse. Los turnos activos pueden cancelarse.

Al confirmar, el veterinario puede ajustar la cantidad de plazas reservadas según la disponibilidad del día. Rechazar o cancelar devuelve toda la cantidad reservada exactamente una vez. Completar conserva las plazas consumidas. La solicitud y la confirmación ajustan la disponibilidad dentro de una transacción con bloqueo de la fila del cupo.

La migración `AgregarCantidadReservada` agrega `Turno.CantidadReservada` con valor inicial 1. Cada turno conserva su relación con un único cupo diario.

La API ofrece `GET /api/cupos`, `GET /api/turnos`, `GET /api/turnos/{id}` y `POST /api/turnos` para solicitar como Tutor. Las transiciones se envían con `POST /api/turnos/{id}/confirmar`, `/rechazar`, `/completar` y `/cancelar`. Para confirmar, enviar JSON con `cantidad`. Los POST requieren la cookie de Identity y el encabezado `RequestVerificationToken` con el token antifalsificación de la sesión. La API y la agenda web consultan los turnos mediante el mismo filtro de visibilidad.

Cada veterinario consulta y administra solo sus turnos y los que tienen No especifica. Esa restricción también se aplica al historial, las URLs de edición y los servicios del turno. Un tutor sigue viendo todos sus propios turnos.

La fecha de un cupo con turnos no se modifica. Para cambiar perro, fecha o veterinario de un turno se cancela y se solicita otro. Los servicios de turnos finalizados quedan de solo lectura. Un servicio realizado no se elimina y un turno debe conservar al menos uno. No se permite cambiar de tutor a un perro con historial. Un perro con turnos activos debe resolverlos antes de su baja lógica.

## Vetos y reservas presenciales

Desde **Tutores → Vetos**, el veterinario puede consultar el historial, aplicar un veto con motivo y levantarlo con el motivo de finalización. Solo se puede aplicar un veto mientras el tutor no tenga otro vigente. El veto manual comienza inmediatamente y queda vigente hasta que un veterinario lo levanta.

El veto bloquea las solicitudes nuevas. Mientras esté vigente, el veterinario puede abrir **Reserva presencial** desde esa misma pantalla. El turno queda solicitado y asignado al profesional que lo registró. La excepción conserva la verificación de vacunas, fechas, cupos disponibles y turnos duplicados.

La API ofrece `GET` y `POST /api/tutores/{tutorId}/vetos`, `POST /api/tutores/{tutorId}/vetos/{vetoId}/levantar` y `POST /api/turnos/presencial`. Se requiere Veterinario activo. Para aplicar o levantar el veto, enviar JSON con `motivo`. Los POST requieren antifalsificación de la sesión de Identity. Se usa la tabla `Vetos` existente.

## Administración de catálogos

**Veterinario** y **Administrador** pueden ingresar a **Catálogos** desde el menú o su panel de inicio. La pantalla reúne los CRUDs de servicios, grupos etarios, tamaños, patologías y tratamientos, con un enlace de regreso desde cada listado. Se comparten los servicios, validaciones, control de duplicados y restricciones de borrado de opciones referenciadas; las escrituras siguen exigiendo antifalsificación.

Los **estados de turnos** se consultan desde esa pantalla y permanecen fijos. **Tutor** no tiene acceso a la gestión, aunque su cuenta también tenga roles de Administrador o Veterinario asignados. La API de catálogos admite las mismas escrituras para Administrador y Veterinario activos. No se requiere migración.

## Cobertura de la API

La API permite gestionar perros, su historial y patologías, tutores y teléfonos, veterinarios, catálogos, cupos y servicios por turno. Reutiliza los servicios de la web para las altas, ediciones, bajas y reglas de negocio. Mantiene la cookie de Identity, la antifalsificación y el rol activo; los permisos y la pertenencia se comprueban desde cada endpoint.

Ver el [inventario de endpoints, comparación con el móvil, permisos y contratos JSON](docs/API.md). Se conserva `/api/cupos` y se agrega el alias `/api/cupos-diarios`. Los estados siguen siendo de solo lectura, y los turnos de otro profesional quedan fuera de las consultas privadas. No se agregan entidades ni migraciones. `Tests/Integracion` verifica los nuevos módulos por HTTP junto con los recorridos web existentes, siempre contra PostgreSQL temporal.

## Registro de la atención

Un Veterinario puede abrir **Registrar atención** desde un turno confirmado de su agenda, a partir de la fecha del cupo (día argentino) o después. El servidor rechaza cierres anticipados aunque se intente llamar directamente a la API. Se conserva la visibilidad de los turnos asignados al profesional y los de **No especifica**.

El formulario confirma la lista definitiva de servicios realizados, varios tratamientos con un motivo opcional por tratamiento y las observaciones generales. Los servicios sin marcar quedan pendientes; un turno puede completarse con servicios incompletos. El tratamiento previsto de la creación o edición aparece seleccionado para su revisión; si se desmarca al cerrar, no queda registrado como aplicado. Los tratamientos del catálogo pueden ampliarse con **Otro…** desde la edición del turno antes de cerrar. No se permiten servicios ajenos o duplicados ni tratamientos inexistentes o repetidos. Los motivos admiten hasta 1000 caracteres y las observaciones hasta 2000.

El estado final, los servicios, los tratamientos, los motivos, las observaciones y el evento de auditoría se guardan en la misma transacción. Completar conserva las plazas reservadas y reinicia el conteo de cancelaciones del tutor. El orden de bloqueo continúa siendo tutor y turno (cupo cuando la transición afecta plazas). Los servicios se administran bajo el bloqueo del mismo turno, por lo que el cierre no puede competir con su edición. Una segunda finalización se rechaza. Los turnos completados no admiten cambios clínicos posteriores.

`POST /api/turnos/{id}/completar` exige Veterinario activo, cookie de Identity y antifalsificación. El cuerpo es:

```json
{
  "servicioIdsCompletados": [1, 2],
  "tratamientos": [
    { "tratamientoId": 3, "razon": "Piel sensible" },
    { "tratamientoId": 4, "razon": "Inquietud durante el baño" }
  ],
  "observaciones": "Atención finalizada"
}
```

Los identificadores deben existir en la instalación. Una lista vacía confirma que no se realizó ningún servicio o tratamiento de ese tipo; no conserva automáticamente marcas anteriores. La respuesta correcta es `204`. El POST web usa `Turnos/Completar` y el mismo servicio. `GET /api/turnos/{id}/atencion` devuelve estado, fecha de finalización UTC tomada de auditoría, observaciones, IDs de servicios realizados y tratamientos con descripción y motivo. Lo pueden consultar el tutor propietario y los veterinarios autorizados para ese turno; los recursos ajenos devuelven `404`. El historial web muestra todos los tratamientos y sus motivos.

La migración **AgregarTratamientoPorTurno** crea `TratamientosPorTurno`, con clave compuesta y FK restringidas, y reemplaza el campo único del turno. Copia las referencias anteriores sin inventar motivos. La reversión solo se permite si el modelo anterior puede representar los datos; se rechaza si hay motivos o varios tratamientos por turno. Ver [DER actualizado](docs/DER.md). Aplicar la migración únicamente a la base de destino indicada por el usuario; el arranque no la aplica automáticamente.

Los informes continúan usando la fecha real de finalización de auditoría y distinguen las atenciones por los servicios finalmente marcados. `Tests/Integracion` verifica actualización desde el esquema anterior con datos, reversión compatible y protección contra pérdida de motivos, instalación desde cero, cierre web/API, fecha mínima, permisos por profesional y tutor, antifalsificación, servicios y tratamientos inválidos, cierre repetido y concurrente, rollback ante fallo de auditoría, conservación de plazas, historial e impacto en informes. Solo utiliza la base temporal del ejecutable.

## Veto automático por cancelaciones

La tercera cancelación realizada por el Tutor sobre sus propios turnos aplica un veto automático. Una atención completada o el levantamiento de cualquier veto reinician el conteo de ese tutor. No cuentan los rechazos, las cancelaciones del Veterinario ni los intentos fallidos o repetidos. Solo se usan eventos registrados desde la incorporación de auditoría; no se reconstruyen cancelaciones históricas. Los eventos se ordenan por fecha UTC e ID para resolver empates.

Se reutilizan `Vetos` y `AuditoriaAcciones`: no hay cambios de modelo, DER ni migraciones nuevas. El veto automático tiene `VeterinarioId = null` y motivo «Superó número de cancelaciones». El evento `VetoAutomaticoAplicado` conserva el usuario y rol Tutor de la cancelación que lo disparó; no atribuye una intervención a un veterinario. **Tutores → Vetos** distingue el origen automático del manual y permite levantarlo o registrar una reserva presencial con las reglas existentes.

Web y API usan `TurnoService`. La cancelación, devolución de plazas, evento de cambio de estado, veto y evento automático se confirman en una sola transacción. Las reservas y transiciones bloquean primero el tutor; las transiciones bloquean luego el turno y finalmente el cupo cuando corresponde. Las lecturas bajo Read Committed después del bloqueo ven los cambios ya confirmados por la operación anterior, incluido un veto recién aplicado. Dos cancelaciones simultáneas no duplican un veto ni devuelven plazas dos veces.

Para probar manualmente, solicitar y cancelar tres turnos de un mismo tutor desde **Mis turnos** o con `POST /api/turnos` y `POST /api/turnos/{id}/cancelar` (cookie y antifalsificación). Tras la tercera cancelación, otra solicitud devuelve un mensaje de veto vigente. Como Veterinario, revisar **Tutores → Vetos**, probar **Reserva presencial** y **Levantar veto**. Tras levantarlo, la siguiente cancelación vuelve a ser la primera. Como Administrador, consultar **Auditoría** o `GET /api/auditoria?tutorId=...` para comprobar el evento automático.

`Tests/Integracion` comprueba el umbral, reinicios por levantamiento y atención completada, exclusiones, aislamiento, empates por fecha/ID, concurrencia, devolución única, rollback ante fallo de auditoría y el recorrido HTTP por web y API. Crea PostgreSQL temporal desde cero, aplica todas las migraciones y elimina esa base al terminar, sin modificar la base de trabajo.

## Auditoría

La migración `AgregarAuditoriaAccion` crea el historial de eventos. Cada registro relaciona el usuario de Identity que actuó con el tutor afectado y guarda el rol activo, la fecha en UTC, el tipo e ID del registro y, para cambios de estado, los estados anterior y nuevo.

Se registran solicitudes, reservas presenciales, ediciones y cambios de estado de turnos, además de la aplicación y el levantamiento de vetos. Los servicios guardan los eventos en la misma transacción que la operación. La auditoría empieza a registrar eventos al aplicar la migración y usar la versión actualizada.

El Administrador puede consultar el historial desde **Auditoría** o mediante `GET /api/auditoria?pagina=1&tutorId=123`. El filtro por tutor es opcional. La pantalla muestra fechas de Argentina y la API devuelve fechas UTC.

## Informes para Administrador

El rol **Administrador activo** puede abrir **Informes** desde el menú o Inicio, elegir fechas inclusivas y consultar tres grillas, cada una con su descarga PDF:

- **Frecuencia de solicitudes:** solicitudes normales y reservas presenciales por día de registro, cualquiera sea su estado posterior. Cada turno se cuenta una sola vez.
- **Atenciones con servicios completos:** turnos completados en el período con al menos un servicio asociado y todos realizados.
- **Atenciones con servicios incompletos:** turnos completados en el período con al menos un servicio pendiente; muestra las observaciones y las patologías actuales del perro. Cada patología se cuenta una vez por atención, aunque haya varios servicios pendientes.

Las fechas de solicitud y finalización provienen de `AuditoriaAcciones`, no de la fecha del cupo. Se usan días de Buenos Aires: desde las 00:00 de la fecha inicial hasta las 00:00 del día posterior a la final, excluyendo ese último instante. Los turnos sin eventos de auditoría no se reconstruyen. Se muestra la fecha y hora del **primer evento de auditoría registrado**, o se avisa que no hay eventos. Ese dato describe el registro disponible, no garantiza cobertura anterior ni demuestra ausencia de actividad en períodos sin eventos.

Las bajas lógicas de perros y tutores no eliminan las atenciones del informe. Los nombres, observaciones y patologías reflejan los datos actuales; no se inventan versiones históricas. Las consultas de un mismo informe comparten un snapshot PostgreSQL para mantener coherencia entre el total y los detalles.

La web ofrece `GET /Informes/Listado` (por defecto, del primer día del mes hasta hoy) y `GET /Informes/Pdf?tipo=0&desde=2026-03-10&hasta=2026-03-10`. `tipo` admite `0` (frecuencia), `1` (completos) y `2` (incompletos). El formulario conserva el período y explica fechas inválidas.

La API, con la cookie de sesión de VetWeb y Administrador activo, ofrece:

| Informe | JSON | PDF |
| --- | --- | --- |
| Frecuencia | `GET /api/reportes/frecuencia-solicitudes/datos` | `GET /api/reportes/frecuencia-solicitudes` |
| Completos | `GET /api/reportes/servicios-completos/datos` | `GET /api/reportes/servicios-completos` |
| Incompletos | `GET /api/reportes/servicios-incompletos/datos` | `GET /api/reportes/servicios-incompletos` |

Todas requieren `desde` y `hasta` en formato `yyyy-MM-dd`. El JSON incluye tipo, período, `auditoriaDesdeUtc`, total y las filas correspondientes (`solicitudes`, `atenciones`, `patologias`). Los períodos vacíos devuelven cero y PDF sin datos; fechas ausentes, inválidas o invertidas devuelven `400`. El acceso anónimo a la API devuelve `401`; Tutor, Veterinario y una cuenta administrativa con otro rol activo reciben `403`. Son consultas GET sin escritura. No se agregan entidades, cambios de DER ni migraciones.

`Tests/Integracion` prueba instalación sin auditoría, datos de control con fechas de solicitud y finalización distintas del cupo, límites del día argentino, conteo único, reservas presenciales, servicios completos/incompletos/sin servicios, observaciones, patologías, bajas lógicas, períodos vacíos e inválidos, grillas y descargas PDF de web y API, y permisos por rol activo. La base de prueba se crea desde cero y se elimina al terminar. Opcionalmente, `VETWEB_PDF_VERIFICACION` indica un directorio para conservar PDF y HTML de revisión, con datos ficticios del test.

## Datos y conservación del historial

Cada entidad tiene su migración individual. PerroPatologia y ServicioPorTurno usan claves compuestas para impedir asociaciones duplicadas. Las relaciones del dominio restringen el borrado en cascada. Nombre, apellido y correo se conservan en ApplicationUser.

Tutor, Veterinario y Perro tienen borrado lógico. Los catálogos con referencias no se pueden eliminar por las claves foráneas. Los teléfonos y asociaciones se eliminan físicamente mediante sus CRUDs; los turnos se cancelan por estado, sin borrarlos. La auditoría de estos movimientos está diferida a otro hito.

Los filtros de consultas ocultan bajas lógicas; las pantallas de historial y servicios del historial usan IgnoreQueryFilters junto con comprobaciones explícitas de pertenencia y rol para conservar la consulta de datos anteriores.

FechaNacimiento y FechaCupo se almacenan como date. Los instantes del modelo Veto se almacenan en UTC. El día de operación se calcula en la zona horaria de Buenos Aires.

## Prueba manual sugerida

1. Reiniciar la aplicación e ingresar como veterinario. Verificar el menú Veterinaria y el acceso independiente a Servicios.
2. Crear un servicio desde Servicios. Al cargar un perro, elegir grupo etario y tamaño entre las opciones existentes. Cargar patologías desde el botón Patologías de ese perro; el tratamiento se elige o agrega al registrar el turno.
3. Crear un tutor con contraseña inicial, agregarle un teléfono y registrar su perro. Asociar una patología y editar sus observaciones.
4. Publicar un cupo disponible para hoy o una fecha futura.
5. Ingresar como tutor, verificar que solo aparezcan sus perros y solicitar un turno con un servicio.
6. Ingresar como veterinario, confirmar el turno, registrar servicios realizados y completar. Revisar su historial desde ambas cuentas.
7. Solicitar y cancelar otro turno; comprobar que el cupo se devuelva una sola vez.
8. Con una cuenta de prueba sin turnos activos, verificar la baja y el bloqueo de acceso incluso si tiene los dos roles.

## Verificación final

La instalación limpia, los recorridos de Administrador, Veterinario y Tutor, y la correspondencia entre DER, EF y PostgreSQL quedaron verificados. Ver [resultados, alcance y comandos de reproducción](VERIFICACION_FINAL.md). El informe anterior `VERIFICACION_HITO.md` conserva el estado histórico de su entrega.

## Pruebas automatizadas

Tests/Integracion contiene un ejecutable de integración sin paquetes adicionales de pruebas. Usa la conexión local solo para crear una base temporal vetweb_prueba_ con un sufijo aleatorio. Requiere permisos para crear bases. Aplica las migraciones y genera cuentas y contraseñas de prueba únicamente allí, levanta una instancia local en el puerto 5057 y elimina esa base al terminar. No modifica la base de trabajo. No ejecutar mientras otro proceso use ese puerto.

```powershell
dotnet run --project Tests/Integracion/Integracion.csproj --configuration VerificacionIdentity
```

Se verificaron altas, ediciones y bajas por HTTP; formularios Razor; antifalsificación; permisos; cuentas con dos roles; baja con sesión abierta; historial; servicios por turno; transiciones; consumo y devolución única de cupos; y dos reservas simultáneas para la última plaza. La compilación de la aplicación pasó sin errores ni advertencias. La revisión visual de cierre y su alcance están documentados en VERIFICACION_HITO.md.

## Dependencias

.NET 8, nullable e implicit usings habilitados. Paquetes Microsoft de Identity y EF Core 8.0.30; Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11; QuestPDF 2026.9.1. La herramienta local dotnet-ef está fijada en 8.0.30. Bootstrap, jQuery y validación están incluidos localmente. QuestPDF genera los informes administrativos en PDF.

## Organización

- Controllers: controladores MVC y filtro de errores de CRUD.
- Dtos: campos admitidos por los formularios y validaciones en español.
- Services: cuentas, reservas, estados y fecha local.
- Middleware: comprobación del acceso de cuentas activas.
- Models y Data: entidades, contexto EF y migraciones.
- Views y wwwroot/js/crud.js: vistas, parciales y comportamiento AJAX compartido.
- Tests/Integracion: pruebas aisladas contra PostgreSQL temporal.

Los cambios de esta etapa quedan locales para revisión. El usuario autorizó el commit y la publicación en la rama codex/entidades-y-migraciones para revisión; la fusión con main queda pendiente.

## Cuenta con varios roles y semilla de desarrollo

Cada sesión trabaja con un solo rol activo. Las cuentas con un rol entran directamente; las que tienen Veterinario y Tutor primero deben elegir en Cuenta/SeleccionarRol. El menú muestra solamente el perfil elegido y la autorización del servidor bloquea los endpoints del otro rol. Para cambiar, pulsar el nombre y Cambiar rol: se vuelve al selector sin cerrar sesión ni pedir la contraseña. Solo se habilita el nuevo rol después de confirmarlo. La elección se conserva durante las renovaciones de la cookie y no modifica los roles asignados en la base.

Mi espacio como tutor y las rutas personales solo están disponibles con Tutor activo. La cancelación usa Cancelado por tutor. Con Veterinario activo se habilitan las herramientas administrativas y se bloquean las rutas exclusivas del tutor. La baja de una cuenta sigue bloqueando ambos perfiles.

El comando --semilla-demo, exclusivo de Development, crea tres cuentas de prueba, dos perros y dos turnos. Ya fue ejecutado en la base local. Las credenciales y el recorrido actualizado están en PRUEBA_ROLES.md. La selección de rol no requiere nuevas migraciones.

Las pruebas de integración verifican selección obligatoria, permisos y menús exclusivos por sesión, rechazo de cambios que salteen el selector y cambio de rol sin perder la autenticación, pertenencia y semilla repetible. Auditoría y vetos continúan fuera de este hito.


## Confirmaciones y mensajes

SweetAlert2 11.26.25 está incluido localmente en wwwroot/lib/sweetalert2, con su licencia MIT. No requiere conexión a un CDN al usar el sitio. Las confirmaciones de bajas y de cambios de estado usan botones Confirmar y Cancelar en español. Si se cancela no se envía la operación. Los mensajes de éxito y error se muestran con SweetAlert2; los errores de validación de campos se conservan junto al formulario. Los mensajes recibidos del servidor se insertan como texto, sin interpretarlos como HTML.


## Catálogos dentro de los formularios

Veterinario y Administrador disponen del acceso Catálogos desde el menú y el inicio, con CRUD de servicios, grupos etarios, tamaños, patologías y tratamientos. Estados queda disponible solo para consulta. Servicios conserva además su acceso directo para Veterinario. Grupo etario y tamaño se eligen desde el formulario del perro; las patologías se vinculan desde el botón Patologías de cada perro; los tratamientos se registran desde el turno. Grupo etario y tamaño permiten únicamente seleccionar opciones existentes, sin Otro…. Las listas de patologías y tratamientos consultan la base y ofrecen Otro… al veterinario para ingresar una descripción de hasta 100 caracteres. Esa opción muestra un campo adicional y lo exige solo mientras esté seleccionada.

La opción nueva y el registro que la utiliza se guardan en la misma transacción. Se normalizan espacios y se reutiliza una descripción existente sin distinguir mayúsculas y minúsculas; un formulario inválido no crea opciones sueltas. Las altas simultáneas de una misma descripción se coordinan con un bloqueo transaccional de PostgreSQL. En la edición de una asociación PerroPatologia no se modifican sus claves; para cambiarla se agrega otra asociación.

Los tutores no ven campos de alta de catálogos y los endpoints verifican el rol activo. Servicios mantiene su administración independiente y los tutores solo seleccionan servicios ya cargados. Los estados son de solo lectura y no tienen opciones de alta, edición ni baja. No se modificó el esquema ni se agregaron migraciones en esta entrega.

Al agregar o editar una patología desde un perro, su nombre se muestra como dato de solo lectura, sin lista de otras mascotas. El formulario conserva ese perro tras los errores de validación y envía un contexto protegido vinculado al usuario y al perro; el servidor rechaza cambios manuales del identificador. No requiere migraciones.


## Agenda por veterinario

El campo Veterinario del turno permite elegir un profesional activo o No especifica. Esta última opción se guarda como VeterinarioId nulo; no se crea un profesional ficticio ni se usa el identificador 0.

Mi agenda muestra únicamente los turnos asignados al usuario veterinario actual y los que no especifican profesional. El historial usa el mismo alcance. Se valida el permiso al consultar formularios o servicios y dentro de las transacciones de edición, cancelación y cambios de estado, para que cambiar una URL o enviar un formulario manualmente no permita operar turnos ajenos.

Los turnos con No especifica permanecen compartidos entre todos los veterinarios, incluso después de confirmarlos; no se asignan automáticamente al primero que los abre. Los tutores consultan sus propios turnos con independencia del profesional seleccionado. El rol activo de la sesión continúa determinando con qué permisos se trabaja.

La migración PermitirTurnoSinVeterinario hace opcional únicamente Turnos.VeterinarioId, conservando las claves foráneas y los turnos existentes. Para revertirla, primero deben asignarse los turnos que sigan sin profesional; la reversión no los rellena con un identificador ficticio.

Las pruebas HTTP verificaron dos agendas independientes, el bloqueo de acceso y operaciones sobre turnos ajenos, la consulta de servicios y la solicitud y confirmación de turnos compartidos. Se mantuvieron las pruebas de pertenencia y reservas concurrentes.

## Fotos y tarjetas de perros

El alta y la edición permiten adjuntar una foto opcional en PNG o JPG de hasta 2 MB. Se valida el tamaño, la extensión y la firma del archivo; no se admite SVG. Al editar sin seleccionar otro archivo se conserva la imagen actual. Una carga inválida no modifica la foto guardada. Al reemplazarla, se elimina el archivo anterior solo después de confirmar el guardado de la base.

La entidad `Imagen` almacena el nombre generado, el tipo de contenido y el tamaño. La relación en el DER es `Perro 1 → 0..1 Imagen`, con `Imagen.PerroId` único. Los archivos se guardan en `App_Data/Perros`, fuera de `wwwroot`, o en el directorio configurado mediante `FotosPerros:Directorio`.

La migración `AgregarEntidadImagen` crea `Imagenes` y retira `Perros.FotoArchivo`. Está prevista para una base nueva. Al aplicarla a una base existente se pierden las referencias de las fotos anteriores. EF Core no elimina los archivos del disco.

La API de VetWeb ofrece `GET /api/perros/{perroId}/imagen` para consultar una foto, `POST` con multipart y campo `archivo` para cargarla o reemplazarla, y `DELETE` para quitarla. Usa la cookie de Identity de VetWeb. El tutor solo consulta imágenes de sus perros. La carga y el borrado requieren el rol Veterinario activo y token antifalsificación de la sesión.

La acción Perros/Foto verifica autenticación, rol activo y pertenencia: el veterinario puede consultar las fotos de los perros y el tutor solo las de sus mascotas. Las imágenes no se exponen como archivos estáticos y se envían con caché privada deshabilitada y tipo de contenido explícito. La baja lógica del perro conserva su archivo, pero deja de servirlo desde la consulta normal.

El listado ahora usa tarjetas Bootstrap de hasta 320 px, adaptables al ancho de pantalla. Cada tarjeta incluye imagen o indicador Sin foto, nombre, tabla con tutor, nacimiento, grupo etario, tamaño, reactividad y vacunas, y el botón principal Historial. El rol Veterinario conserva Patologías, Editar y Dar de baja; el tutor no ve esas acciones.

AgregarFotoPerro fue aplicada en la base local y agrega una columna opcional sin cambiar los perros anteriores. Se probaron carga multipart, reemplazo, conservación al editar sin foto, rechazo de formatos no admitidos y archivos grandes, privacidad de la imagen y datos de la tarjeta sobre una base y carpeta temporales. No se agregaron paquetes de imágenes ni datos demo con fotos en la base de trabajo.

### Asistente de chat

El asistente flotante permite a Tutor consultar cupos, sus turnos y registros clínicos, y solicitar turnos con confirmación. Veterinario puede consultar cancelaciones y vetos de un tutor y agendar para sus perros. Administrador dispone de consultas exclusivas de servicios solicitados, completados/cancelados, comparación mensual y auditoría. Estas acciones guiadas funcionan sin IA; la interpretación por texto todavía no está conectada a Gemini. Ver [acciones, permisos y guía de conexión a Gemini](docs/CHATBOT.md).
