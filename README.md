# VetWeb

Aplicación ASP.NET Core MVC para gestionar tutores, perros y turnos de peluquería canina de una veterinaria. Usa Identity, PostgreSQL, EF Core y Bootstrap local.

## Estado actual

CRUDs principales implementados con acciones en español, DTOs de formulario, vistas parciales, modales Bootstrap y AJAX. Los controladores consultan EF Core directamente; CuentaService concentra las operaciones de Identity y TurnoService las reservas y transiciones con transacciones.

La base local tiene las 20 migraciones aplicadas, incluyendo AgregarFotoPerro. Se conservaron la cuenta inicial y el perfil veterinario con matrícula 45789. Los seis estados del circuito se cargan por migración y no se pueden modificar desde la aplicación. Servicios conserva su CRUD en el menú; los demás catálogos se eligen y amplían desde los formularios correspondientes.

Auditoría, administración de vetos, veto automático y reportes PDF quedan fuera de este hito por decisión del usuario. La entidad Veto existe, pero no tiene controlador, DTO ni servicio. Estos CRUDs no aplican vetos al solicitar turnos.

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

## Pantallas y permisos

| Sección | Veterinario | Tutor |
| --- | --- | --- |
| Tutores y teléfonos | Alta, consulta, edición y baja | Sin acceso administrativo |
| Veterinarios | Alta, consulta, edición y baja | Sin acceso administrativo |
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

Rechazos y cancelaciones devuelven el cupo exactamente una vez. Completar conserva la plaza consumida. Las reservas usan transacciones serializables y bloqueo de la fila del cupo para evitar sobreventa; si una operación concurrente obliga a reintentar, se informa al usuario. El cupo no se interpreta como un horario: es disponibilidad para un día.

Cada veterinario consulta y administra solo sus turnos y los que tienen No especifica. Esa restricción también se aplica al historial, las URLs de edición y los servicios del turno. Un tutor sigue viendo todos sus propios turnos.

La fecha de un cupo con turnos no se modifica. Para cambiar perro, fecha o veterinario de un turno se cancela y se solicita otro. Los servicios de turnos finalizados quedan de solo lectura. Un servicio realizado no se elimina y un turno debe conservar al menos uno. No se permite cambiar de tutor a un perro con historial. Un perro con turnos activos debe resolverlos antes de su baja lógica.

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

## Pruebas automatizadas

Tests/Integracion contiene un ejecutable de integración sin paquetes adicionales de pruebas. Usa la conexión local solo para crear una base temporal vetweb_prueba_ con un sufijo aleatorio. Requiere permisos para crear bases. Aplica las migraciones y genera cuentas y contraseñas de prueba únicamente allí, levanta una instancia local en el puerto 5057 y elimina esa base al terminar. No modifica la base de trabajo. No ejecutar mientras otro proceso use ese puerto.

```powershell
dotnet run --project Tests/Integracion/Integracion.csproj --configuration VerificacionIdentity
```

Se verificaron altas, ediciones y bajas por HTTP; formularios Razor; antifalsificación; permisos; cuentas con dos roles; baja con sesión abierta; historial; servicios por turno; transiciones; consumo y devolución única de cupos; y dos reservas simultáneas para la última plaza. La compilación de la aplicación pasó sin errores ni advertencias. La revisión visual de cierre y su alcance están documentados en VERIFICACION_HITO.md.

## Dependencias

.NET 8, nullable e implicit usings habilitados. Paquetes Microsoft de Identity y EF Core 8.0.30; Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11; QuestPDF 2026.9.1. La herramienta local dotnet-ef está fijada en 8.0.30. Bootstrap, jQuery y validación están incluidos localmente. QuestPDF queda disponible para un hito posterior de reportes.

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

El menú del veterinario muestra únicamente Servicios como catálogo independiente. Grupo etario y tamaño se eligen desde el formulario del perro; las patologías se vinculan desde el botón Patologías de cada perro; los tratamientos se registran desde el turno. Grupo etario y tamaño permiten únicamente seleccionar opciones existentes, sin Otro…. Las listas de patologías y tratamientos consultan la base y ofrecen Otro… al veterinario para ingresar una descripción de hasta 100 caracteres. Esa opción muestra un campo adicional y lo exige solo mientras esté seleccionada.

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

FotoArchivo guarda únicamente un nombre generado, nunca el nombre o la ruta enviados por el navegador. Las imágenes se guardan por defecto en App_Data/Perros, fuera de wwwroot y del repositorio. La carpeta puede configurarse mediante FotosPerros:Directorio. Debe conservarse y respaldarse junto con la base al mover o publicar la aplicación; la migración no contiene los archivos de imagen.

La acción Perros/Foto verifica autenticación, rol activo y pertenencia: el veterinario puede consultar las fotos de los perros y el tutor solo las de sus mascotas. Las imágenes no se exponen como archivos estáticos y se envían con caché privada deshabilitada y tipo de contenido explícito. La baja lógica del perro conserva su archivo, pero deja de servirlo desde la consulta normal.

El listado ahora usa tarjetas Bootstrap de hasta 320 px, adaptables al ancho de pantalla. Cada tarjeta incluye imagen o indicador Sin foto, nombre, tabla con tutor, nacimiento, grupo etario, tamaño, reactividad y vacunas, y el botón principal Historial. El rol Veterinario conserva Patologías, Editar y Dar de baja; el tutor no ve esas acciones.

AgregarFotoPerro fue aplicada en la base local y agrega una columna opcional sin cambiar los perros anteriores. Se probaron carga multipart, reemplazo, conservación al editar sin foto, rechazo de formatos no admitidos y archivos grandes, privacidad de la imagen y datos de la tarjeta sobre una base y carpeta temporales. No se agregaron paquetes de imágenes ni datos demo con fotos en la base de trabajo.
