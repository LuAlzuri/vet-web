# Asistente de VeteWeb

El botón flotante abre el asistente en las páginas con sesión. Tutor y Veterinario tienen acciones guiadas que consultan datos reales y permiten solicitar turnos. Funcionan sin Gemini: se eligen opciones, no se interpretan todavía instrucciones escritas en lenguaje natural. Administrador dispone de consultas exclusivas de informes y auditoría, también mediante acciones guiadas.

## Acciones del dueño (rol Tutor)

- Consultar días con cupos disponibles: grilla con el total disponible por día, excluyendo agendas pasadas y sin plazas.
- Consultar sus turnos: grilla de número, perro, día y estado actual, incluyendo turnos finalizados.
- Elegir uno de sus perros y consultar patologías, si están controladas y sus observaciones.
- Consultar las atenciones completadas del perro: profesional, tratamientos y sus motivos, y observaciones registradas. La fecha mostrada es la del turno, no una fecha de atención inferida.
- Solicitar un turno: elegir perro, agenda del día, uno o varios servicios, revisar el resumen y confirmar.

Solo se muestran los perros y datos clínicos que pertenecen al usuario autenticado. Las grillas y opciones tienen paginación de 10 registros. La solicitud queda en estado **Solicitado**, como en el flujo web; no equivale a una confirmación de atención por el profesional.

## Acciones del veterinario

- Buscar tutores activos por nombre o apellido. Se muestran los identificadores para distinguir homónimos.
- Consultar las cancelaciones realizadas por ese tutor, con número de turno y fecha real del evento en Argentina, obtenidas de auditoría.
- Consultar todos sus vetos registrados: motivo, inicio, fin, vigencia y motivo de finalización.
- Elegir un tutor y uno de sus perros, consultar cupos, elegir servicios y agendar con confirmación previa. El resumen siempre identifica al perro y su tutor; el turno se asigna al profesional de la sesión.

**Faltas:** actualmente el dominio no tiene un estado de inasistencia. El asistente informa esa limitación y muestra las cancelaciones auditadas del Tutor. No interpreta una cancelación del veterinario ni una ausencia de auditoría como falta del dueño.

**Presencial:** se conserva la regla existente. Si el tutor tiene veto vigente, se usa `TurnoService.ReservarPresencialAsync`, con auditoría de reserva presencial. Si no tiene veto, el veterinario agenda mediante `TurnoService.GuardarAsync`. Ambos caminos conservan las validaciones de la aplicación. El chat no levanta vetos ni cambia estados.

## Acciones exclusivas del Administrador

- **Servicios más solicitados:** ranking paginado de servicios según la primera fecha de solicitud del turno. Incluye reservas presenciales y turnos luego cancelados. Se puede alternar entre el mes actual hasta hoy y el mes anterior completo.
- **Turnos completados y cancelados:** totales de solicitudes, completados y cancelaciones, distinguiendo Tutor y Veterinario. Cada indicador usa la fecha de su evento de auditoría; no la fecha agendada. Los rechazados no son cancelaciones.
- **Comparar este mes con el anterior:** grilla con solicitudes, completados, cancelaciones por rol y total cancelado, más diferencia absoluta. Se explicita que el mes actual está en curso y el anterior es un mes completo, sin proyectar resultados ni presentar una variación entre períodos de igual duración.
- **Últimas acciones de auditoría:** eventos paginados de 25 en 25, por fecha e identificador descendentes, con usuario, rol, tutor y estados anterior/nuevo.

Los límites de mes y las fechas visibles usan la zona de Argentina. Los indicadores deduplican los eventos por turno y conservan el historial de bajas lógicas. Los informes indican desde cuándo hay cobertura de auditoría; no inventan fechas para turnos sin eventos. Las consultas no modifican turnos, cupos ni auditoría. Administrador no obtiene acciones de reserva ni de consulta clínica por tener otros roles asignados: rige el rol activo.

## Confirmación y seguridad

Preparar una reserva no escribe un turno ni consume plazas. El servidor guarda una propuesta temporal por cuenta y rol, devuelve un identificador aleatorio de confirmación y muestra el resumen. Solo el botón **Confirmar solicitud de turno** la ejecuta. La propuesta vence a los 5 minutos; preparar otra reemplaza la anterior. **Descartar**, **Acciones disponibles** o **Nueva conversación** invalidan la propuesta pendiente.

La confirmación se consume una sola vez, de manera atómica, y se vincula al usuario y rol de la sesión. El cliente no puede cambiar los datos aprobados enviando un perro, tutor, agenda o servicio distinto al confirmar. La disponibilidad, pertenencia, veto, edad/vacunas y duplicación de turno se vuelven a validar mediante los servicios de negocio al guardar. El bloqueo transaccional existente evita sobreventa de plazas.

Las propuestas se guardan en memoria del servidor y se pierden al reiniciar. Un despliegue con múltiples instancias necesita sesiones afines o reemplazar este almacén por uno compartido con consumo atómico. Si la confirmación falla, se prepara una propuesta nueva; ante un timeout se deben consultar los turnos antes de volver a reservar.

Todas las acciones requieren cookie autenticada y antifalsificación. Las consultas administrativas exigen el rol activo Administrador; las acciones de Tutor y Veterinario mantienen sus permisos propios. Tener varios roles asignados en la cuenta no combina sus permisos durante una sesión. Las respuestas usan `Cache-Control: no-store`; los mensajes y las grillas se renderizan como texto, sin HTML de usuarios o proveedores.

## Estructura

- `Views/Shared/_Chatbot.cshtml`: ventana compartida, acciones y búsqueda.
- `wwwroot/js/chatbot.js`: conversación por texto e historial temporal.
- `wwwroot/js/chatbot-acciones.js`: flujo guiado, grillas, paginación y confirmación.
- `wwwroot/css/chatbot.css`: diseño adaptable.
- `Controllers/Api/ChatbotApiController.cs`: `POST /api/chatbot/mensajes` y `POST /api/chatbot/acciones`.
- `Dtos/ChatbotDto.cs` y `Dtos/ChatbotAccionDto.cs`: contratos y validaciones.
- `Services/Chatbot/ChatbotAccionesService.cs`: fachada de acciones, autorización y consultas.
- `Services/Chatbot/ChatbotAdministradorService.cs`: consultas de informes y auditoría exclusivas del Administrador.
- `Services/Chatbot/ChatbotReservasPendientes.cs`: propuestas temporales con confirmación de un solo uso.
- `Services/Chatbot/IChatbotProveedor.cs` y `ChatbotSinConexion.cs`: extensión para el proveedor de lenguaje natural.
- `Tests/Integracion/ChatbotAccionesPruebas.cs`: pruebas HTTP y base temporal de consultas privadas, reservas, vetos, confirmación y concurrencia.

No se requiere migración. Las reservas confirmadas sí escriben turnos, consumen cupos y registran auditoría mediante los servicios existentes. El historial de conversación vive solo en la memoria de la página: navegar o recargar lo borra.

## API de acciones

Enviar JSON y el encabezado `RequestVerificationToken` obtenido de una página autenticada:

```json
{ "accion": "inicio" }
```

El servidor devuelve `mensaje`, `tablas` (título, columnas y filas), `acciones` (etiqueta y solicitud del siguiente paso) y `buscarTutor`. El cliente envía la solicitud de la opción elegida, sin tomar decisiones de autorización.

Acciones disponibles: `inicio`, `cupos`, `turnos`, `perros-clinica`, `clinica`, `patologias`, `atenciones`, `tutores-faltas`, `faltas`, `vetos`, `tutores-reserva`, `perros-reserva`, `reserva-cupos`, `reserva-servicios`, `preparar`, `confirmar`, `descartar`, `admin-servicios`, `admin-estados`, `admin-comparar` y `admin-auditoria`. No todas están permitidas para cada rol. Los parámetros incluyen `perroId`, `tutorId`, `cupoId`, `servicioIds`, `pagina`, `busqueda`, `confirmacion` y `periodo` (`mes-actual` o `mes-anterior`). Las acciones de selección devuelven los parámetros necesarios para seguir.

## Conversación por texto y futura conexión con Gemini

`POST /api/chatbot/mensajes` conserva el contrato:

```json
{ "mensaje": "¿Cómo uso VeteWeb?", "historial": [{ "rol": "user", "texto": "Hola" }] }
```

Todavía devuelve `conectado: false` y un mensaje que informa que Gemini no está conectado. No hay llamadas externas ni una clave configurada. El mensaje admite 2000 caracteres y el historial 12 entradas de 4000 caracteres, con roles `user` y `assistant`.

Para conectar Gemini:

1. Crear un adaptador `GeminiChatbotProveedor : IChatbotProveedor` con `HttpClient` administrado. Configurar clave, modelo y timeout exclusivamente en el servidor, con secretos de usuario o variables de entorno. No poner claves en JavaScript ni en el repositorio.
2. Traducir el historial de `assistant` a `model`. El historial del cliente es manipulable; las instrucciones del sistema se construyen en el servidor y no conceden acceso a datos.
3. Registrar herramientas de consulta y preparación que llamen a `ChatbotAccionesService` con la identidad autenticada del servidor. No aceptar usuario ni rol desde la IA. Mantener las respuestas estructuradas para mostrar grillas y resolver ambigüedades mediante selección explícita.
4. **No exponer `confirmar` como herramienta ejecutable por el modelo.** Gemini puede preparar una propuesta; la escritura debe continuar dependiendo del botón de confirmación y del token vinculado a la sesión. El texto “confirmo” del historial no prueba consentimiento.
5. Reemplazar el proveedor sin conexión en `Program.cs`, controlar cuotas, límites por cuenta, errores y cancelaciones, y habilitar el dominio oficial de Gemini si el despliegue restringe salida a Internet.

El chatbot consulta registros; no inventa diagnósticos ni modifica datos clínicos. No tiene acciones para eliminar turnos, cambiar estados, editar patologías ni levantar vetos.
