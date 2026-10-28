# VetWeb

Aplicación MVC para gestionar turnos de peluquería canina en una veterinaria.

## Estado actual

Base MVC con .NET 8 y persistencia de Identity configurada para PostgreSQL. ApplicationUser agrega Nombre y Apellido obligatorios, de hasta 50 caracteres. La migración InicialIdentity crea las siete tablas estándar de Identity y carga los roles Veterinario y Tutor. Un usuario puede tener ambos roles mediante AspNetUserRoles. No se han aplicado migraciones ni creado usuarios. Las páginas Razor de ingreso, cierre de sesión y acceso denegado están implementadas en español dentro de Areas/Identity. El alta inicial del veterinario y los CRUDs quedan para las siguientes entregas.

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

Compilación y generación del script SQL realizadas sin conexión a la base. Se utilizó la configuración VerificacionIdentity para evitar el bloqueo de bin/Debug por la instancia local abierta. Para compilar normalmente, detener primero la aplicación con Ctrl+C. La aplicación efectiva de la migración y el inicio de sesión con un usuario real todavía no se han probado: requieren configurar PostgreSQL, aplicar la migración y crear el veterinario inicial.

## Páginas de Identity

El menú muestra Iniciar sesión a visitantes y Nombre, Apellido y Cerrar sesión a usuarios autenticados. No hay registro público ni páginas predeterminadas adicionales de Identity. Las cuentas de tutor se crearán desde la administración veterinaria en una entrega posterior.

La salida se realiza mediante POST con token antifalsificación. Visitar la página de salida mediante GET solo presenta una confirmación. El ingreso también exige token antifalsificación y solo permite redirecciones de retorno locales. Acceso denegado devuelve HTTP 403.

Para revisar esta entrega, detener la instancia anterior con Ctrl+C, ejecutar `dotnet run --launch-profile http` y abrir Iniciar sesión. Se puede verificar el formulario y sus mensajes sin una base configurada; autenticar una cuenta requiere completar la preparación de la base y del primer veterinario. Si falta la conexión, un envío válido del formulario informa que el acceso todavía no está disponible.
