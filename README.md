# VeteWeb · El Refugio

Sistema web para gestionar clientes, perros, turnos y atenciones de peluquería canina. Incluye los roles Tutor, Veterinario y Administrador, con autenticación mediante ASP.NET Core Identity.

## Versión online

La aplicación está publicada en MonsterASP: [Acceder a VeteWeb](http://vetweb.runasp.net/).

El acceso a las funciones del sistema requiere una cuenta proporcionada por la veterinaria.

## Stack y versiones

| Componente | Versión |
| --- | --- |
| .NET / ASP.NET Core MVC e Identity | 8.0 (`net8.0`) |
| C# | 12, predeterminado de .NET 8 |
| Entity Framework Core | 8.0.30 |
| PostgreSQL | 17 en el entorno de desarrollo |
| Bootstrap | 5.1.0 |
| jQuery | 3.6.0 |
| DataTables | 2.3.5 |
| SweetAlert2 | 11.26.25 |

Las bibliotecas del navegador están incluidas en `wwwroot/lib`; no se necesita Node.js ni npm. Las versiones de NuGet están declaradas en `VetWeb.csproj` y la herramienta EF en `dotnet-tools.json`.

## Paquetes NuGet

| Paquete | Versión |
| --- | --- |
| Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore | 8.0.30 |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.30 |
| Microsoft.AspNetCore.Identity.UI | 8.0.30 |
| Microsoft.EntityFrameworkCore.Design | 8.0.30 |
| Microsoft.EntityFrameworkCore.Tools | 8.0.30 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.11 |
| QuestPDF | 2026.9.1 |
| Serilog.Extensions.Logging | 9.0.0 |
| Serilog.Sinks.File | 7.0.0 |

La herramienta local `dotnet-ef` usa la versión **8.0.30** y se restaura por separado de los paquetes NuGet.

## Instalación y ejecución local

### 1. Requisitos y descarga

Instalar el **SDK de .NET 8** y **PostgreSQL**. Para trabajar desde Visual Studio, instalar la carga **Desarrollo de ASP.NET y web** con soporte para .NET 8 y abrir `VetWeb.sln`.

```powershell
git clone https://github.com/LuAlzuri/vet-web.git
cd vet-web
dotnet restore
dotnet tool restore
```

Ejecutar los comandos siguientes desde esa carpeta. No es necesario crear un proyecto nuevo: el repositorio contiene la solución completa.

### 2. Configurar la conexión y el administrador inicial

Con PostgreSQL en ejecución, crear una base vacía, por ejemplo `vet_web`, desde pgAdmin. El usuario de conexión debe tener permisos para crear las tablas y aplicar las migraciones.

En Visual Studio: clic derecho en el proyecto **VetWeb → Administrar secretos de usuario**. Colocar la conexión y los datos iniciales dentro de **un único objeto JSON**, reemplazando los ejemplos:

```json
{
  "ConnectionStrings:DefaultConnection": "Host=localhost;Port=5432;Database=vet_web;Username=TU_USUARIO;Password=TU_CLAVE_POSTGRES",
  "AdministradorInicial:Email": "admin@ejemplo.com",
  "AdministradorInicial:Nombre": "Tu nombre",
  "AdministradorInicial:Apellido": "Tu apellido",
  "AdministradorInicial:Password": "REEMPLAZAR_POR_TU_CLAVE"
}
```

La contraseña de la cuenta debe tener al menos 8 caracteres, mayúscula, minúscula, número y símbolo. Verificar el puerto en las propiedades de conexión de pgAdmin; `5432` es el habitual. Los secretos son locales: no deben subirse al repositorio. Como alternativa a Visual Studio, se pueden configurar con `dotnet user-secrets set "Clave" "Valor"`.

### 3. Crear las tablas y la primera cuenta

```powershell
dotnet build VetWeb.sln
dotnet tool run dotnet-ef database update --project VetWeb.csproj
dotnet run --project VetWeb.csproj --launch-profile http -- --crear-administrador
```

Las migraciones incluidas crean el esquema de Identity, las entidades y los datos fijos. No se aplican automáticamente al iniciar la web. Revisar que la conexión apunte a la base local deseada antes de ejecutar `database update`.

`--crear-administrador` se ejecuta una sola vez, después de las migraciones, y termina sin iniciar el servidor. Si ya existe un administrador, omitirlo. Después de crearlo se pueden quitar las claves `AdministradorInicial:*` de los secretos, conservando la conexión.

### 4. Iniciar la web

```powershell
dotnet run --project VetWeb.csproj --launch-profile http
```

Abrir [http://localhost:5043](http://localhost:5043) e ingresar con el administrador creado. Desde **Veterinarios** se crean los profesionales y se pueden vincular clientes o asignar el rol Administrador. El registro público está deshabilitado; una cuenta con varios roles elige con cuál trabajar.

En Visual Studio, seleccionar el proyecto **VetWeb** como inicio, elegir el perfil **http** y ejecutar con **F5** o **Ctrl+F5**. Detener la instancia anterior antes de volver a compilar si los archivos están bloqueados.

Para HTTPS, ejecutar `dotnet dev-certs https --trust` y usar el perfil `https`: [https://localhost:7215](https://localhost:7215).

## Datos de demostración y pruebas

Opcional, después de aplicar las migraciones:

```powershell
dotnet run --project VetWeb.csproj --launch-profile http -- --semilla-demo
```

Crea cuentas de prueba: `veterinario.demo@vetweb.invalid`, `tutor.demo@vetweb.invalid` y `doble.demo@vetweb.invalid`, con contraseña `DemoVet2026!` para las cuentas nuevas. Incluye perros y turnos de muestra; solo usar en desarrollo.

```powershell
dotnet run --project Tests/Integracion/Integracion.csproj --configuration VerificacionIdentity
```

Las pruebas crean y eliminan una base temporal y requieren permiso para crear bases y el puerto `5057` libre. Para usar una instancia PostgreSQL aislada en CI o en la nube, configurá `VETWEB_TEST_ConnectionStrings__DefaultConnection` en el entorno de la suite. Tiene prioridad sobre User Secrets. Reservá esta conexión para pruebas y usá una instancia separada del hosting. Las fotos cargadas se guardan en `App_Data/Perros`; conservar esa carpeta junto con la base para respaldar los datos.

## Documentación adicional

El chat escrito se conecta a Gemini configurando `Gemini:ApiKey`, `Gemini:Modelo` y `Gemini:TimeoutSegundos` en secretos de usuario. Para verificarlo: `dotnet run --project VetWeb.csproj --launch-profile http -- --probar-gemini`. Con Gemini, tutores y veterinarios pueden iniciar desde texto la selección guiada para solicitar un turno. La confirmación se hace con el botón del resumen. Las acciones guiadas siguen disponibles sin clave.

[API](docs/API.md) · [Modelo y DER](docs/DER.md) · [Diseño visual](docs/DISENO_VISUAL.md) · [Asistente de chat](docs/CHATBOT.md)
