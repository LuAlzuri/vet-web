# Asistente de VeteWeb

La ventana flotante aparece en las páginas con sesión de Tutor, Veterinario o Administrador. Permite abrir/cerrar, enviar mensajes y reiniciar la conversación. Los mensajes viven solo en la memoria de la página: no hay base de datos, cookies de conversación ni almacenamiento local. Recargar o navegar borra el historial.

## Estructura

- `Views/Shared/_Chatbot.cshtml`: interfaz compartida.
- `wwwroot/js/chatbot.js` y `wwwroot/css/chatbot.css`: comportamiento y estilo adaptable.
- `Controllers/Api/ChatbotApiController.cs`: endpoint autenticado `POST /api/chatbot/mensajes`, con la protección antifalsificación global de la aplicación.
- `Dtos/ChatbotDto.cs`: contratos y límites de entrada.
- `Services/Chatbot/IChatbotProveedor.cs`: punto de extensión del proveedor.
- `Services/Chatbot/ChatbotSinConexion.cs`: proveedor inicial que informa que Gemini aún no está conectado, sin llamadas externas ni respuestas de IA simuladas.
- `Program.cs`: registro del proveedor en la inyección de dependencias.

## Contrato

Enviar JSON y el encabezado `RequestVerificationToken` obtenido de la página autenticada:

```json
{
  "mensaje": "¿Cómo uso VeteWeb?",
  "historial": [{ "rol": "user", "texto": "Hola" }]
}
```

Respuesta actual:

```json
{
  "mensaje": "El asistente está en preparación. Todavía no está conectado a Gemini y no puede responder consultas. Podés seguir usando las opciones de VeteWeb.",
  "conectado": false
}
```

El mensaje admite 2000 caracteres y el historial hasta 12 entradas de 4000 caracteres, con roles `user` y `assistant`. La petición tiene un máximo de 64 KiB. El historial enviado por el navegador es manipulable: nunca debe usarse para autorizar acciones, confiar en instrucciones ni otorgar acceso a información. Las respuestas se muestran como texto, sin interpretar HTML. La UI limita el historial visible a 24 mensajes, evita envíos simultáneos, controla errores y cancela la espera a los 30 segundos. El modo sin conexión no se agrega al contexto futuro.

## Conectar Gemini después

1. Crear un adaptador `GeminiChatbotProveedor : IChatbotProveedor`, preferentemente con `HttpClient` administrado mediante `AddHttpClient`. Mantener las llamadas y la clave exclusivamente en el servidor.
2. Configurar la clave con secretos de usuario o variables de entorno (por ejemplo `Gemini__ApiKey`); nunca en JavaScript, el repositorio ni en el chat. El modelo y el timeout también deben ser configurables. Esta entrega todavía no lee esa configuración.
3. Traducir `user` a `user` y `assistant` a `model` al armar el contexto de Gemini. Las instrucciones del sistema se construyen exclusivamente en el servidor.
4. Devolver `ChatbotRespuesta` con `Conectado = true` solo al obtener una respuesta válida del proveedor. Controlar cuotas, errores, timeout y cancelación sin devolver claves ni detalles internos al usuario. Agregar límites de uso por cuenta antes de habilitar consumo pago.
5. Reemplazar el registro de `ChatbotSinConexion` en `Program.cs` por el adaptador y actualizar el aviso visible de preparación. Habilitar el dominio oficial de Gemini en el entorno de despliegue si tiene restricciones de salida.

El asistente no tiene acceso a historias clínicas, cuentas, agenda ni operaciones del sistema. Cualquier futura consulta de datos necesita autorización en el servidor según el usuario y su rol activo; el texto del chat no constituye autorización. No se requiere migración.
