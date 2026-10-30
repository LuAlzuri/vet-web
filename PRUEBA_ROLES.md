# Prueba de acceso por rol de sesión

Reiniciar la aplicación desde Visual Studio. Las cuentas demo ya están cargadas en la base local; esta corrección no agrega datos ni requiere migraciones.

| Cuenta | Roles asignados | Contraseña de prueba |
| --- | --- | --- |
| veterinario.demo@vetweb.invalid | Veterinario | DemoVet2026! |
| tutor.demo@vetweb.invalid | Tutor | DemoVet2026! |
| doble.demo@vetweb.invalid | Veterinario y Tutor | DemoVet2026! |

Son credenciales públicas de demostración para desarrollo. La cuenta inicial personal no se modificó.

## Un rol activo por sesión

Las cuentas con un solo rol ingresan directamente con ese rol. La cuenta con ambos roles primero ve la pantalla Elegir rol y debe confirmar Veterinario o Tutor.

Una vez elegido, solo se habilitan el menú, las pantallas y los endpoints de ese rol hasta confirmar un cambio de rol. La autorización del servidor usa únicamente el rol activo, aunque Identity conserve los dos roles asignados en la base. Escribir una URL del otro perfil no habilita sus permisos.

La elección se conserva en la cookie. Para cambiarla, abrir el nombre y pulsar Cambiar rol: se vuelve al selector sin cerrar sesión ni pedir otra vez la contraseña. Hasta confirmar el nuevo rol no quedan habilitadas las operaciones de ninguno de los perfiles. Las pestañas del mismo navegador comparten el rol actualizado en las siguientes solicitudes.

El menú muestra el rol activo junto al nombre. Una cuenta dada de baja sigue perdiendo el acceso completo con todos sus roles.

## Qué debe ver cada rol

Veterinario: Administración veterinaria y el menú Veterinaria y el acceso a Servicios. Puede administrar cuentas, perros, teléfonos y cupos; su agenda, historial y servicios de turnos se limitan a los asignados a ese veterinario y los marcados No especifica. No aparecen las opciones exclusivas Mi espacio de tutor, Mis perros o Mis turnos.

Tutor: Mi espacio como tutor, Mis perros, Mis turnos y Cupos disponibles. Solo consulta sus mascotas y su historial y solicita o cancela sus turnos. No aparecen herramientas administrativas y los endpoints veterinarios quedan bloqueados.

## Datos demo originales

- Luna (demo) pertenece a tutor.demo@vetweb.invalid.
- Milo (demo) pertenece a doble.demo@vetweb.invalid.
- veterinario.demo@vetweb.invalid atiende los dos turnos de ejemplo.
- Se incluyeron Demo - Adulto, Demo - Mediano, Demo - Baño y un cupo futuro.

Estos datos describen una siembra nueva. En la revisión del 30/10/2026 la base local ya tenía cambios manuales: el tutor demo no tenía perros activos, la cuenta doble tenía a Copito y los turnos originales estaban cancelados. La semilla no restablece esos datos ni garantiza cupos futuros al repetirla. El recorrido reproducible completo se verifica en la base temporal de integración.

## Recorrido de prueba

1. Ingresar como doble.demo@vetweb.invalid y elegir Tutor.
2. Verificar que solo aparezcan las opciones de tutor y que Mis perros muestre Milo y no Luna.
3. Abrir manualmente /Tutores/Listado, /Servicios/Listado o /Perros/ObtenerFormulario: debe mostrarse Acceso denegado.
4. Consultar Mis turnos y el historial de Milo. La cancelación se registra como Cancelado por tutor y devuelve el cupo una sola vez.
5. Abrir /Cuenta/SeleccionarRol: vuelve al inicio, sin permitir saltear el flujo de Cambiar rol.
6. Pulsar el nombre, abrir el desplegable y elegir Cambiar rol. Debe aparecer el selector sin volver al ingreso ni pedir contraseña. Elegir Veterinario.
7. Verificar que aparezcan las herramientas veterinarias y que Todos los perros incluya Luna y Milo. No deben aparecer opciones de tutor.
8. Abrir manualmente /Perros/MisPerros o /Turnos/MisTurnos: debe mostrarse Acceso denegado hasta usar Cambiar rol y elegir Tutor.

## Repetir la semilla

```powershell
dotnet run --project VetWeb.csproj --launch-profile http -- --semilla-demo
```

Solo está disponible en Development, requiere las migraciones aplicadas y nunca se ejecuta al iniciar normalmente. No duplica datos, restablece contraseñas, reactiva bajas ni repone turnos cancelados. Los datos existentes y la cuenta inicial se conservan. Si una cuenta demo fue modificada o dada de baja, la semilla informa el conflicto en lugar de sobrescribirla.

## Verificación

Las pruebas HTTP sobre PostgreSQL temporal comprobaron elección obligatoria, menús exclusivos, bloqueo de URLs del otro rol, antifalsificación de la elección, rechazo de cambios que salteen el flujo previsto y cambio de rol sin perder la autenticación. Se conservaron las pruebas de pertenencia, bajas de cuentas y reservas concurrentes. La base temporal se eliminó al terminar.

## Desplegable del nombre

Al pulsar el nombre aparece el rol activo y Cerrar sesión. Las cuentas con ambos roles también muestran Cambiar rol, una acción POST con antifalsificación que conserva la autenticación y vuelve al selector. Al confirmar se reemplaza el rol activo. Las cuentas de un solo rol no ven esa opción y tampoco pueden ejecutarla por URL.


## Agenda profesional

Los dos turnos de la semilla están asignados a Marina (veterinario.demo@vetweb.invalid). Alex con rol Veterinario no debe verlos en Mi agenda, aunque como Tutor sí consulta el turno de su propio perro. Para probar turnos compartidos, publicar un cupo futuro y solicitar un turno eligiendo No especifica: aparece en las agendas de todos los profesionales. Los turnos compartidos no se asignan automáticamente al confirmarlos.
