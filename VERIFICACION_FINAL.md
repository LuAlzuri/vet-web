# Verificación final de VetWeb

Fecha: 12/11/2026 (Argentina).

Resultado: **verificación satisfactoria** de la instalación limpia, los recorridos de los tres roles y la correspondencia entre DER, modelo EF y esquema PostgreSQL. Se corrigió un mensaje del PDF y se incorporó una comprobación reproducible del esquema a la suite existente.

Base revisada: `89f79f98193efe6d50bc8c6903b237193503b9a6` de `main`, que integra el PR #11 de cobertura de API. La validación final incluye el ajuste de presentación en `InformesPdfService`, el nuevo `ModeloPruebas` y su llamada desde el ejecutable de integración. No se modificó el modelo ni se agregó una migración.

## Instalación y compilación

Se extrajo una copia de las fuentes sin `.git`, `bin`, `obj`, archivos privados ni datos de la instalación de trabajo. Se usaron un directorio NuGet inicialmente vacío y directorios separados de configuración y herramientas de usuario. La restauración descargó las dependencias manteniendo la verificación TLS y la configuración del repositorio.

Se verificó con .NET SDK 8.0.428 y PostgreSQL 17 en Linux. Se ejecutaron restauración, restauración de la herramienta EF 8.0.30 y publicación de `VetWeb.csproj` en Release. La aplicación se ejecutó desde la carpeta publicada, con sus archivos estáticos, en Development por HTTP local y contra una base vacía de nombre aleatorio `vetweb_final_*`.

La base de trabajo no recibió migraciones ni escrituras. La configuración local usada por la suite solo permitió crear sus bases `vetweb_prueba_*`.

| Comprobación | Resultado |
| --- | --- |
| Restauración de dependencias en caché nueva | Correcta |
| Compilación y publicación Release desde copia limpia | Correctas |
| Aplicación de las 25 migraciones sobre PostgreSQL vacío | Correcta |
| Modelo EF sin cambios pendientes de migración | Confirmado |
| Suite completa de integración desde la copia | Exit 0 |
| Recorridos reales en Chromium con Release | Correctos; cero errores JavaScript |
| PDF descargado desde la interfaz | Contenido y corrección verificados |
| Limpieza de servidor y bases temporales | Completada |

## DER, modelo y esquema real

`Tests/Integracion/ModeloPruebas.cs` compara el modelo relacional de EF con los catálogos de PostgreSQL y las relaciones del diagrama de `docs/DER.md`. Verifica tablas, columnas, tipos, nulabilidad, claves primarias, claves foráneas, orden de sus columnas, políticas de borrado, índices secundarios y unicidad. Compara también las expresiones de los CHECK después de canonizarlas con PostgreSQL en tablas temporales.

Resultado de la ejecución:

| Elemento | Cantidad verificada |
| --- | ---: |
| Migraciones aplicadas, coincidentes con el proyecto | 25 |
| Tablas modeladas, incluidas las de Identity | 25 |
| Columnas de esas tablas | 114 |
| Claves primarias | 25 |
| Claves foráneas | 27 |
| Índices secundarios, sin contar los de PK | 27 |
| Restricciones CHECK | 5 |
| Relaciones del dominio y cardinalidades del DER | 21 |

Los recuentos de tablas y columnas excluyen `__EFMigrationsHistory`. Las cinco restricciones verificadas corresponden a matrícula, disponibilidad no negativa, plazas reservadas, tamaño de imagen y fechas de veto. Se comprobaron las claves compuestas de asociaciones y las relaciones opcionales de imagen, veterinario de turno y veterinario de veto.

La suite también comprobó la actualización desde el esquema anterior conservando el tratamiento único, la reversión compatible y el rechazo de una reversión que perdería motivos de tratamiento.

## Recorridos de los tres roles

Se usaron cuentas y datos ficticios en una base distinta de las bases de integración. Las acciones siguientes se realizaron con los controles reales de la interfaz, incluidos sus formularios AJAX y cookie de sesión:

1. **Administrador:** ingreso y alta de otro veterinario con matrícula y contraseña inicial de prueba.
2. **Veterinario:** ingreso, alta de tutor y alta de su perro, con grupo etario, tamaño y vacunas.
3. **Tutor:** ingreso con la cuenta creada por el veterinario, consulta de su perro y solicitud de un turno con dos servicios.
4. **Veterinario:** consulta de la agenda, confirmación de dos plazas y registro de atención con un servicio realizado, otro pendiente, tratamiento, motivo y observaciones. El turno quedó completado.
5. **Tutor:** consulta del historial y coincidencia del registro clínico mediante la API usando la misma sesión web.
6. **Administrador:** consulta de los eventos de solicitud y cambios de estado, grilla de atenciones con servicios incompletos y descarga PDF con esa atención.

Se comprobaron rechazos `403` al intentar consultar módulos de otro rol. Las pantallas de perro e informes se revisaron en escritorio de 1366 × 900 y móvil emulado de 390 × 844; no desbordaron el ancho de página. Las tablas anchas mantienen desplazamiento dentro de su contenedor. No se registraron errores JavaScript en los recorridos.

## Suite de integración

La ejecución final pasó todos los bloques del ejecutable existente, incluidos:

- CRUDs web y API, cuentas vinculadas, roles activos y selección obligatoria de rol.
- Pertenencia de perros, historial privado, agenda por profesional y No especifica.
- Cookies, antifalsificación, cuentas inactivas y protección de cuentas administrativas.
- Fotos privadas, formatos, tamaño, reemplazo y conservación.
- Reservas, vacunas, cupos y devolución única; concurrencia por la última plaza.
- Vetos manuales y automáticos, reinicios del conteo, reservas presenciales y auditoría.
- Registro de atención, fecha mínima, varios tratamientos, motivos, servicios incompletos, concurrencia y rollback por fallo de auditoría.
- Informes por fechas reales de auditoría y días argentinos, patologías, observaciones, bajas lógicas, períodos vacíos o inválidos y PDF.
- Esquema PostgreSQL y cardinalidades del DER mediante la nueva comprobación.

Los casos negativos y los fallos de auditoría provocados son parte de las aserciones esperadas. No quedaron fallos de prueba pendientes. La base temporal del ejecutable se eliminó al finalizar.

## Corrección durante el cierre

Un informe con atenciones pero sin patologías mostraba el mensaje genérico «No hay datos para el período seleccionado» dentro de la sección de patologías. Se cambió a «No hay patologías asociadas». La descarga real de Release se volvió a probar: conservó `Total: 1`, la observación del turno y la nueva leyenda. No cambió el cálculo del informe.

## Reproducción y evidencias

Con .NET 8, PostgreSQL y la conexión de pruebas configurada en los secretos locales de `VetWeb-Desarrollo-2026`:

```sh
dotnet restore VetWeb.sln
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet publish VetWeb.csproj --configuration Release --no-restore
dotnet run --project Tests/Integracion/Integracion.csproj --configuration VerificacionIdentity
```

El ejecutable requiere permisos para crear bases y el puerto 5057 libre. No migra ni escribe en la base de trabajo. `ModeloPruebas` queda integrado en ese mismo comando. Una publicación no inicia procesos ni aplica migraciones por sí misma.

Las evidencias locales de esta ejecución están en `/workspace/.vetweb-final/artifacts/`: registros de restauración, publicación y suite, `schema.json`, `browser.json`, `pdf-check.json`, capturas de los roles y el PDF descargado. No se versionan capturas ni credenciales. Se compararon 284 archivos con la copia utilizada en la validación antes de redactar este informe.

Se detuvo únicamente el servidor de esta verificación, se eliminó su base, se confirmó que no quedaron bases `vetweb_final_*` ni `vetweb_prueba_*` de las pruebas y se eliminó la contraseña efímera del recorrido.

## Alcance y límites

No se realizó despliegue ni publicación de una imagen del entorno. La comprobación visual fue en Chromium con escritorio y móvil emulado; no sustituye pruebas en teléfonos físicos, otros navegadores o bajo carga. Los prerrequisitos .NET y PostgreSQL estaban instalados; se verificaron fuentes, dependencias, compilación y bases nuevas, no la instalación de un sistema operativo.

EF Core conserva su advertencia 10622 por la relación obligatoria con Tutor y su filtro de baja lógica. Las consultas históricas usan `IgnoreQueryFilters` y los casos de bajas e historial pasaron. La redirección HTTPS avisa que no hay puerto HTTPS en esta ejecución HTTP local; no se deshabilitó la redirección ni se verificó un certificado de producción.

Los cambios de cierre incluyen: mensaje del PDF, comprobación de esquema y documentación. La base de trabajo permanece intacta.
