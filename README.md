# VetWeb

Aplicación MVC para gestionar turnos de peluquería canina en una veterinaria.

## Estado actual

Base MVC con .NET 8 y persistencia de Identity configurada para PostgreSQL. ApplicationUser agrega Nombre y Apellido obligatorios, de hasta 50 caracteres. La migración InicialIdentity crea las siete tablas estándar de Identity y carga los roles Veterinario y Tutor. Un usuario puede tener ambos roles mediante AspNetUserRoles. En la base local se aplicaron InicialIdentity, AgregarVeterinario y AgregarTutor; existe la cuenta inicial y su perfil veterinario. Las demás migraciones están generadas y pendientes de aplicar. Las páginas Razor de ingreso, cierre de sesión y acceso denegado están implementadas en español dentro de Areas/Identity. La creación explícita de la cuenta inicial con rol Veterinario está preparada; los CRUDs quedan para las siguientes entregas.

## Requisitos

- SDK de .NET 8 o un SDK posterior compatible con net8.0.
- PostgreSQL para las siguientes etapas de persistencia.
- Acceso a NuGet durante la primera restauración.

## Ejecutar

Desde la carpeta del proyecto:

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --launch-profile http
```

Abrir la dirección que informa la consola. Deben verse Inicio y Privacidad en español y los estilos de Bootstrap. Detener con Ctrl+C.

Para usar el perfil HTTPS, preparar el certificado local con `dotnet dev-certs https --trust` y ejecutar `dotnet run --launch-profile https`.

## Conexión a PostgreSQL

La clave prevista es `ConnectionStrings:DefaultConnection`. No guardar contraseñas en los archivos versionados. El proyecto ya tiene un UserSecretsId propio; en desarrollo ASP.NET Core carga automáticamente los secretos de usuario.

Configurar localmente, reemplazando los valores del ejemplo:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=vet_web;Username=TU_USUARIO;Password=TU_CLAVE"
```

Este comando guarda la configuración fuera del repositorio. Los secretos de usuario son para desarrollo y no se cifran. En otros entornos puede usarse la variable `ConnectionStrings__DefaultConnection`.

ApplicationDbContext utiliza esta conexión. El inicio de la aplicación no abre la conexión ni aplica migraciones automáticamente. No se registran las páginas predeterminadas de Identity, por lo que no hay registro público habilitado. Las rutas públicas son /Identity/Cuenta/Ingresar, /Identity/Cuenta/Salir y /Identity/Cuenta/AccesoDenegado. Se conservan los nombres de archivo convencionales Login, Logout y AccessDenied dentro de Areas/Identity/Pages/Account. Las páginas son implementaciones locales editables de Razor Pages; no se utilizó el generador de scaffolding ni se habilitó AddDefaultUI.

## Dependencias de referencia

| Paquete | Versión |
| --- | --- |
| Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore | 8.0.30 |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.30 |
| Microsoft.AspNetCore.Identity.UI | 8.0.30 |
| Microsoft.EntityFrameworkCore.Design | 8.0.30 |
| Microsoft.EntityFrameworkCore.Tools | 8.0.30 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.11 |
| QuestPDF | 2026.9.1 |

Los paquetes están declarados; su funcionalidad se configurará por etapas. QuestPDF se utilizará cuando se aborde la generación de reportes.

## Organización

- `Controllers`: controladores MVC.
- `Models`: modelos de la aplicación.
- `Views`: vistas Razor y componentes compartidos.
- `wwwroot`: Bootstrap, jQuery y recursos estáticos locales de la plantilla.
- `Properties/launchSettings.json`: perfiles de ejecución local.

## Forma de trabajo

Avanzar por entidad o capa, explicar previamente el alcance y esperar confirmación o prueba manual antes de continuar. Usar acciones en español, preservar el historial mediante borrado lógico donde corresponda y crear las migraciones al incorporar entidades. No publicar cambios al remoto sin autorización.

## Migración inicial de Identity

La herramienta local dotnet-ef está fijada en 8.0.30 en dotnet-tools.json. Al clonar el proyecto, restaurarla con:

```powershell
dotnet tool restore
```

Con PostgreSQL disponible y la conexión local configurada, el siguiente comando aplica la migración a la base indicada:

```powershell
dotnet tool run dotnet-ef database update
```

Este comando modifica la base de destino: crea las tablas AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins y AspNetUserTokens, además del historial de migraciones. Inserta los dos roles; no incluye usuarios ni contraseñas iniciales. Confirmar la base de destino antes de ejecutarlo. No revertir la migración sobre datos que se necesiten conservar: su reversión elimina estas tablas.

Para revisar el SQL sin conectarse a PostgreSQL:

```powershell
dotnet tool run dotnet-ef migrations script
```

Identity usa correo único y contraseñas de al menos 8 caracteres, con mayúscula, minúscula, número y símbolo. El bloqueo se configura en 5 intentos fallidos durante 5 minutos; la pantalla de ingreso cuenta los intentos con lockoutOnFailure. El formulario valida correo y contraseña con mensajes en español, tanto en el servidor como en el navegador.

### Verificación de esta entrega

Compilación y generación del script SQL realizadas sin conexión a la base. Se utilizó la configuración VerificacionIdentity para evitar el bloqueo de bin/Debug por la instancia local abierta. Para compilar normalmente, detener primero la aplicación con Ctrl+C. La aplicación efectiva de la migración y el inicio de sesión con un usuario real todavía no se han probado: requieren configurar PostgreSQL, aplicar la migración y ejecutar el comando de creación de la cuenta inicial.

## Páginas de Identity

El menú muestra Iniciar sesión a visitantes y Nombre, Apellido y Cerrar sesión a usuarios autenticados. No hay registro público ni páginas predeterminadas adicionales de Identity. Las cuentas de tutor se crearán desde la administración veterinaria en una entrega posterior.

La salida se realiza mediante POST con token antifalsificación. Visitar la página de salida mediante GET solo presenta una confirmación. El ingreso también exige token antifalsificación y solo permite redirecciones de retorno locales. Acceso denegado devuelve HTTP 403.

Para revisar esta entrega, detener la instancia anterior con Ctrl+C, ejecutar `dotnet run --launch-profile http` y abrir Iniciar sesión. Se puede verificar el formulario y sus mensajes sin una base configurada; autenticar una cuenta requiere completar la preparación de la base y del primer veterinario. Si falta la conexión, un envío válido del formulario informa que el acceso todavía no está disponible.

## Crear la cuenta inicial con rol Veterinario

Este paso crea una cuenta de ApplicationUser y su relación con el rol Veterinario. La entidad de dominio Veterinario y su matrícula se incorporarán en otra entrega. No se crea ninguna cuenta al iniciar el sitio normalmente.

1. En Visual Studio, hacer clic derecho en VetWeb y seleccionar **Administrar secretos de usuario**. Completar las claves locales, reemplazando todos los valores de ejemplo:

```json
{
  "ConnectionStrings:DefaultConnection": "Host=localhost;Port=5432;Database=vet_web;Username=TU_USUARIO;Password=TU_CLAVE_POSTGRES",
  "VeterinarioInicial:Email": "TU_CORREO",
  "VeterinarioInicial:Nombre": "TU_NOMBRE",
  "VeterinarioInicial:Apellido": "TU_APELLIDO",
  "VeterinarioInicial:Password": "TU_CONTRASEÑA_INICIAL"
}
```

2. Detener la aplicación antes de compilar. Aplicar la migración a la base de destino revisada:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update
```

3. Crear la cuenta explícitamente en entorno de desarrollo, donde se cargan los secretos de usuario:

```powershell
dotnet run --launch-profile http -- --crear-veterinario
```

El comando termina al completar la operación, sin levantar el servidor web. Repetirlo con el mismo correo no cambia la contraseña ni los datos. Si el correo corresponde a una cuenta sin el rol Veterinario, se rechaza la operación sin elevar sus permisos. La creación de cuenta y asignación del rol se realizan en una transacción.

4. Quitar las cuatro claves VeterinarioInicial de los secretos locales después de crear la cuenta. Conservar la conexión y guardar la contraseña en un lugar personal seguro.
5. Ejecutar normalmente y probar Iniciar sesión con las credenciales elegidas, verificar el nombre en el menú y probar Cerrar sesión.

No hay contraseñas de ejemplo operativas ni credenciales versionadas. La contraseña debe cumplir la política configurada de Identity. La migración y la creación de la cuenta modifican la base configurada; verificar siempre el destino antes de ejecutar.

## Entidad Veterinario

Se agregó Models/Veterinario.cs con Id, UserId obligatorio, Matricula entre 1 y 99999 y Eliminado (false por defecto). Nombre, apellido y correo permanecen en ApplicationUser.

Cada perfil requiere una cuenta existente de Identity. El índice único en UserId admite como máximo un perfil veterinario por cuenta; una cuenta de tutor puede existir sin perfil veterinario. La clave foránea restringe el borrado físico de una cuenta que tenga un perfil asociado. La base también valida el rango de matrícula mediante una restricción CHECK. No se impone unicidad a Matricula porque todavía no se definió esa regla.

El filtro global de consultas oculta los perfiles marcados Eliminado. Para una futura consulta administrativa de bajas puede usarse IgnoreQueryFilters. Este filtro no convierte Remove en borrado lógico ni bloquea por sí mismo el inicio de sesión: el controlador y las reglas de acceso se implementarán en sus entregas correspondientes.

La migración AgregarVeterinario fue generada, revisada y aplicada en la base local de desarrollo. Se creó y verificó el perfil activo de la cuenta inicial con rol Veterinario y matrícula 45789. Este registro es un dato local; la migración no incluye usuarios ni matrículas personales y no lo reproduce al instalar en otra base. No se agregaron controladores ni vistas en esta entrega. La compilación y la comprobación de sincronización entre modelo y migración pasaron correctamente.

## Entidad Tutor

Se agregó Models/Tutor.cs con Id, UserId obligatorio y Eliminado (false por defecto). ApplicationUser conserva nombre, apellido y correo; esos datos no se duplican en el perfil.

Cada tutor se vincula a una cuenta existente de Identity mediante una relación uno a uno con índice único en UserId y borrado restringido. Una misma cuenta puede tener simultáneamente perfil Tutor y perfil Veterinario. Crear un perfil no asigna automáticamente un rol de Identity: esa operación se incorporará al implementar el alta administrada por el veterinario.

Las consultas habituales excluyen perfiles Eliminado mediante un filtro global. El filtro no convierte Remove en una baja lógica ni bloquea por sí mismo la autenticación. Estas reglas se completarán en la capa de administración y acceso. Los teléfonos, perros y vetos se incorporarán en sus respectivas entregas.

La migración AgregarTutor fue generada, revisada y aplicada a PostgreSQL en la base local de desarrollo. No se crearon tutores, controladores ni vistas en esta entrega. Se verificaron la compilación, la sincronización del modelo con la migración y la generación del SQL incremental.

## Entidad TelefonoTutor

Se agregó TelefonoTutor con Id, TutorId, NombrePropietario obligatorio (hasta 100 caracteres) y Numero obligatorio (hasta 30 caracteres, con validación Phone). Los límites de longitud son decisiones de implementación; el contexto no fijaba longitudes. El número se almacena como texto para conservar prefijos y ceros iniciales. El propietario puede ser una persona distinta del tutor y no necesita una cuenta de Identity.

La colección Tutor.Telefonos permite varios números por tutor; no se exige unicidad global del teléfono. La clave foránea impide registros huérfanos y restringe el borrado físico en cascada. Las consultas habituales excluyen teléfonos de tutores eliminados, sin borrar esos registros. No se agregó Eliminado al teléfono porque el diagrama no lo contempla; la política de eliminación individual se resolverá al implementar su administración.

La migración AgregarTelefonoTutor crea únicamente TelefonosTutores y su índice sobre TutorId. Fue generada y revisada; todavía no se aplicó a PostgreSQL. Se verificaron compilación, sincronización del modelo y generación de SQL incremental. No se agregaron controladores ni vistas.

## Entidad GrupoEtario

Se agregó el catálogo GrupoEtario con Id y Descripcion obligatoria de hasta 100 caracteres. El límite es una decisión de implementación para mantener simples y consistentes los catálogos. No se agregaron rangos de edad, registros iniciales ni restricciones de unicidad de descripción porque esas reglas no están definidas en el contexto. La relación con Perro se incorporará al crear esa entidad.

La migración AgregarGrupoEtario crea únicamente la tabla GruposEtarios. Fue generada y revisada; todavía no se aplicó a PostgreSQL. Se verificaron compilación, sincronización del modelo y generación del SQL incremental. No se agregaron controladores ni vistas.

## Entidad Tamanio

Se agregó el catálogo Tamanio con Id y Descripcion obligatoria de hasta 100 caracteres, siguiendo la estructura de GrupoEtario. Se registra mediante Tamanios en ApplicationDbContext. La relación con Perro se incorporará al crear esa entidad. No se cargaron categorías iniciales ni se agregaron restricciones de unicidad porque el contexto no las define.

La migración AgregarTamanio crea únicamente Tamanios. Fue generada y revisada; todavía no se aplicó a PostgreSQL. Se verificaron compilación, sincronización del modelo y generación de SQL incremental. No se agregaron controladores ni vistas.

## Entidad Patologia

Se agregó el catálogo Patologia con Id y Descripcion obligatoria de hasta 100 caracteres, siguiendo la estructura de los otros catálogos. Se registra mediante Patologias en ApplicationDbContext. La asociación con perros, con Controlado y Observacion, se incorporará al crear PerroPatologia. No se cargaron patologías iniciales ni se agregaron restricciones de unicidad porque el contexto no las define.

La migración AgregarPatologia crea únicamente Patologias. Fue generada y revisada; todavía no se aplicó a PostgreSQL. Se verificaron compilación, sincronización del modelo y generación de SQL incremental. No se agregaron controladores ni vistas.

## Entidad Perro

Se agregó Perro con Id, Nombre obligatorio (hasta 100 caracteres), FechaNacimiento, Reactivo, CalendarioVacunasCompleto y relaciones obligatorias con Tutor, GrupoEtario y Tamanio. Tutor.Perros permite consultar las mascotas asociadas. La fecha usa DateTime en C# y date en PostgreSQL para conservar el día sin hora ni zona horaria; es una adaptación del datetime del diagrama a una fecha de nacimiento.

Se agregó Eliminado (false por defecto), adicional al SVG, para cumplir el requisito de conservar el historial mediante bajas lógicas. El filtro global excluye perros eliminados y perros cuyo tutor esté eliminado, sin borrar esos datos. El controlador deberá realizar la baja lógica explícitamente; Remove no se transforma automáticamente en una baja lógica.

Las tres relaciones restringen el borrado físico en cascada. No se agregaron patologías ni turnos en esta entrega. La validación de una fecha de nacimiento razonable y la regla de solicitud para cachorros menores de 45 días con vacunas incompletas se completarán en las capas de administración y turnos; el grupo etario no se calcula automáticamente porque todavía no se definieron sus rangos.

La migración AgregarPerro crea únicamente Perros con sus claves e índices. Fue generada y revisada; todavía no se aplicó a PostgreSQL. Se verificaron compilación, sincronización del modelo y generación del SQL incremental. No se agregaron controladores ni vistas.

## Entidad PerroPatologia

Se agregó la relación muchos a muchos PerroPatologia con clave compuesta PerroId y PatologiaId, que impide repetir una misma patología para el mismo perro. Incluye Controlado y Observacion opcional de hasta 1000 caracteres; este límite es una decisión de implementación. Perro.Patologias y Patologia.Perros contienen las asociaciones, incluyendo sus datos adicionales.

Ambas claves foráneas restringen el borrado físico en cascada. El filtro global oculta asociaciones de perros eliminados o cuyo tutor esté eliminado, sin eliminar los datos. No se agregó un campo Id independiente porque el SVG define la clave compuesta.

La migración AgregarPerroPatologia crea únicamente PerrosPatologias, sus claves y el índice de PatologiaId. Fue generada y revisada; todavía no se aplicó a PostgreSQL. Se verificaron compilación, sincronización del modelo y generación del SQL incremental. No se agregaron controladores ni vistas.

## Entidades restantes y migraciones individuales

Se completó el modelo del SVG, con una migración independiente por entidad:

| Entidad | Migración | Contenido |
| --- | --- | --- |
| PerroPatologia | AgregarPerroPatologia | Asociación compuesta, Controlado y Observacion opcional |
| Estado | AgregarEstado | Catálogo con Id y Descripcion |
| Tratamiento | AgregarTratamiento | Catálogo con Id y Descripcion |
| Servicio | AgregarServicio | Catálogo con Id y Descripcion |
| CupoDiario | AgregarCupoDiario | FechaCupo y NumeroCuposDisponibles |
| Turno | AgregarTurno | Veterinario, perro, cupo, estado, tratamiento opcional y observaciones |
| ServicioPorTurno | AgregarServicioPorTurno | Asociación compuesta y Completado |
| Veto | AgregarVeto | Tutor, veterinario opcional, fechas y motivos |

Los catálogos conservan la estructura simple de las guías, con descripción obligatoria de hasta 100 caracteres. No tienen registros iniciales. CupoDiario guarda una fecha sin hora y valida que la cantidad disponible no sea negativa, tanto en el modelo como mediante CHECK en PostgreSQL. No se definieron horarios ni unicidad por fecha, porque faltan las reglas definitivas de publicación de cupos.

Turno conserva un tratamiento opcional por turno, como el SVG; sus servicios se vinculan mediante ServicioPorTurno. La clave compuesta TurnoId + ServicioId impide repetir un servicio en el mismo turno. Observaciones admite hasta 2000 caracteres. Las relaciones restringen el borrado físico en cascada. Un turno se cancelará mediante su estado, sin eliminar su historial.

Veto tiene FechaFin nullable: sin fecha final representa una sanción abierta. FechaInicio y FechaFin son instantes UTC (timestamp with time zone); al asignarlos desde código debe usarse DateTime con Kind UTC, por ejemplo DateTime.UtcNow. El CHECK de fechas evita un fin anterior al inicio. Los motivos son opcionales de hasta 1000 caracteres. VeterinarioId es nullable, a diferencia del SVG, para permitir sanciones automáticas sin atribuirlas a una persona. El levantamiento y la trazabilidad de quién lo realiza se resolverán en su capa de negocio y auditoría.

Las consultas habituales de Turno y ServicioPorTurno ocultan registros asociados a perros, tutores o veterinarios eliminados; Veto excluye tutores eliminados. No se borran estos registros: las futuras consultas administrativas de historial deberán usar IgnoreQueryFilters con autorización adecuada para incluir bajas lógicas.

Esta entrega no implementa CRUDs, transiciones de estado, descuento o devolución de cupos, controles de concurrencia de reservas, veto automático ni la regla de edad y vacunas. Tampoco agrega auditoría, cuyo modelo y acceso siguen pendientes de definición. Estas reglas deben implementarse antes de habilitar solicitudes reales.

Las ocho migraciones de esta entrega quedaron generadas y sin aplicar a PostgreSQL, junto con las cinco migraciones anteriores que todavía estaban pendientes. En la base local solo se habían aplicado InicialIdentity, AgregarVeterinario y AgregarTutor. Un futuro database update aplicará todas las pendientes en orden, por lo que debe revisarse la conexión de destino antes de ejecutarlo.

Verificación de la entrega conjunta: las ocho migraciones crean exactamente una tabla cada una, sin operaciones sobre tablas existentes. Se comprobaron claves compuestas, cinco relaciones de Turno, campos opcionales, filtros de bajas, rechazo de cupos negativos y ausencia de claves foráneas implícitas. Se generó y revisó el SQL incremental de las ocho tablas. No se ejecutaron pruebas contra PostgreSQL ni se aplicaron estas migraciones.
