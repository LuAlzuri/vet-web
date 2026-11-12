using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;

namespace VetWeb.Services.Chatbot;

// Fachada común para el flujo guiado actual y las futuras herramientas de Gemini.
// La identidad siempre se toma de la sesión: nunca del mensaje o de los parámetros.
public sealed class ChatbotAccionesService(ApplicationDbContext db, TurnoService turnos, VetoService vetos,
    ChatbotReservasPendientes pendientes, ChatbotAdministradorService administrador)
{
    private const int Tamano = 10;
    private static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static string FechaUtc(DateTime fecha) => Reloj.EnArgentina(fecha).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public async Task<ChatbotAccionRespuesta> EjecutarAsync(ChatbotAccionSolicitud solicitud, ClaimsPrincipal principal)
    {
        var usuario = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
        if (principal.IsInRole(RolSesion.Administrador)) return await administrador.EjecutarAsync(solicitud, principal);
        if (solicitud.Accion.StartsWith("admin-", StringComparison.Ordinal)) throw new UnauthorizedAccessException();
        var veterinario = principal.IsInRole("Veterinario");
        if (!veterinario && !principal.IsInRole("Tutor")) throw new UnauthorizedAccessException();
        if (solicitud.ServicioIds is null || solicitud.ServicioIds.Any(x => x <= 0)
            || solicitud.ServicioIds.Count != solicitud.ServicioIds.Distinct().Count())
            throw new InvalidOperationException("Seleccioná servicios válidos y distintos.");
        var acciones = new List<ChatbotAccion>();
        var tablas = new List<ChatbotTabla>();
        var mensaje = "";
        ChatbotAccionSolicitud Nueva(string accion, int? perro = null, int? tutor = null, int? cupo = null, int pagina = 1,
            List<int>? servicios = null, string? confirmacion = null) => new()
            { Accion = accion, PerroId = perro, TutorId = tutor, CupoId = cupo, Pagina = pagina,
                ServicioIds = servicios ?? [], Busqueda = solicitud.Busqueda, Confirmacion = confirmacion };
        void Boton(string etiqueta, ChatbotAccionSolicitud siguiente) => acciones.Add(new(etiqueta, siguiente));
        void Paginar(int cantidad, Func<int, ChatbotAccionSolicitud> siguiente)
        {
            if (solicitud.Pagina > 1) Boton("Página anterior", siguiente(solicitud.Pagina - 1));
            if (cantidad > Tamano) Boton("Página siguiente", siguiente(solicitud.Pagina + 1));
        }
        IQueryable<Perro> Perros() => db.Perros.AsNoTracking().Where(x => x.Tutor.Usuario.Activo
            && (veterinario || x.Tutor.UserId == usuario));
        async Task<Perro> PerroElegido()
        {
            var perro = await Perros().Include(x => x.Tutor).ThenInclude(x => x.Usuario)
                .SingleOrDefaultAsync(x => x.Id == solicitud.PerroId) ?? throw new UnauthorizedAccessException();
            if (veterinario && perro.TutorId != solicitud.TutorId) throw new UnauthorizedAccessException();
            return perro;
        }
        string TutorNombre(Perro perro) => perro.Tutor.Usuario.Nombre + " " + perro.Tutor.Usuario.Apellido;
        switch (solicitud.Accion)
        {
            case "descartar":
                pendientes.Descartar(usuario, veterinario);
                goto case "inicio";
            case "inicio":
                mensaje = "¿Qué querés consultar o hacer?";
                Boton("Cupos disponibles", Nueva("cupos"));
                if (veterinario)
                {
                    Boton("Cancelaciones y vetos de un tutor", Nueva("tutores-faltas"));
                    Boton("Agendar turno para un perro", Nueva("tutores-reserva"));
                }
                else
                {
                    Boton("Mis turnos y estados", Nueva("turnos"));
                    Boton("Patologías, tratamientos y observaciones", Nueva("perros-clinica"));
                    Boton("Solicitar un turno", Nueva("perros-reserva"));
                }
                break;
            case "tutores-faltas":
            case "tutores-reserva":
                if (!veterinario) throw new UnauthorizedAccessException();
                var tutores = db.Tutores.AsNoTracking().Where(x => x.Usuario.Activo);
                if (!string.IsNullOrWhiteSpace(solicitud.Busqueda))
                {
                    var texto = solicitud.Busqueda.Trim().ToLower();
                    tutores = tutores.Where(x => (x.Usuario.Nombre + " " + x.Usuario.Apellido).ToLower().Contains(texto));
                }
                var listaTutores = await tutores.OrderBy(x => x.Usuario.Apellido).ThenBy(x => x.Id)
                    .Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1)
                    .Select(x => new { x.Id, x.Usuario.Nombre, x.Usuario.Apellido }).ToListAsync();
                mensaje = "Elegí el tutor por nombre e identificador. Podés buscarlo por nombre o apellido.";
                foreach (var tutor in listaTutores.Take(Tamano))
                    Boton($"{tutor.Nombre} {tutor.Apellido} · Tutor #{tutor.Id}",
                        Nueva(solicitud.Accion == "tutores-faltas" ? "faltas" : "perros-reserva", tutor: tutor.Id));
                Paginar(listaTutores.Count, p => Nueva(solicitud.Accion, pagina: p));
                if (listaTutores.Count == 0) mensaje = "No se encontraron tutores activos con esa búsqueda.";
                return new(mensaje, tablas, acciones, BuscarTutor: true);
            case "perros-clinica":
            case "perros-reserva":
                if (veterinario && !await db.Tutores.AnyAsync(x => x.Id == solicitud.TutorId && x.Usuario.Activo))
                    throw new UnauthorizedAccessException();
                var perros = await Perros().Where(x => !veterinario || x.TutorId == solicitud.TutorId)
                    .OrderBy(x => x.Nombre).ThenBy(x => x.Id).Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1)
                    .Select(x => new { x.Id, x.Nombre, x.TutorId, Tutor = x.Tutor.Usuario.Nombre + " " + x.Tutor.Usuario.Apellido }).ToListAsync();
                mensaje = perros.Count == 0 ? "No hay perros disponibles para esta selección." : "Elegí el perro. Cada opción indica quién es su tutor.";
                foreach (var perro in perros.Take(Tamano))
                    Boton($"{perro.Nombre} · Perro #{perro.Id} · Tutor: {perro.Tutor} (#{perro.TutorId})",
                        Nueva(solicitud.Accion == "perros-clinica" ? "clinica" : "reserva-cupos", perro.Id, perro.TutorId));
                Paginar(perros.Count, p => Nueva(solicitud.Accion, tutor: solicitud.TutorId, pagina: p));
                break;
            case "cupos":
            case "reserva-cupos":
                Perro? elegido = null;
                if (solicitud.Accion == "reserva-cupos") elegido = await PerroElegido();
                if (elegido is null)
                {
                    var porDia = await db.CuposDiarios.AsNoTracking().Where(x => x.FechaCupo >= Reloj.Hoy && x.NumeroCuposDisponibles > 0)
                        .GroupBy(x => x.FechaCupo).OrderBy(x => x.Key).Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1)
                        .Select(x => new { Fecha = x.Key, Cantidad = x.Sum(c => (long)c.NumeroCuposDisponibles) }).ToListAsync();
                    tablas.Add(new("Cupos disponibles por día", ["Día", "Cupos disponibles"], porDia.Take(Tamano)
                        .Select(x => new[] { Fecha(x.Fecha), x.Cantidad.ToString() }).ToList()));
                    mensaje = porDia.Count == 0 ? "No hay días con cupos disponibles en esta página." : "Disponibilidad total por día. Consultar cupos no realiza una reserva.";
                    Paginar(porDia.Count, p => Nueva("cupos", pagina: p));
                    break;
                }
                var cupos = await db.CuposDiarios.AsNoTracking().Where(x => x.FechaCupo >= Reloj.Hoy && x.NumeroCuposDisponibles > 0)
                    .OrderBy(x => x.FechaCupo).ThenBy(x => x.Id).Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1).ToListAsync();
                tablas.Add(new("Cupos disponibles", ["Día", "Cupos disponibles"], cupos.Take(Tamano)
                    .Select(x => new[] { Fecha(x.FechaCupo), x.NumeroCuposDisponibles.ToString() }).ToList()));
                mensaje = elegido is null ? "Disponibilidad actual. Consultar cupos no realiza una reserva."
                    : $"Elegí el día para {elegido.Nombre}. Tutor: {TutorNombre(elegido)} (#{elegido.TutorId}).";
                if (cupos.Count == 0) mensaje = "No hay días con cupos disponibles en esta página.";
                if (elegido is not null)
                    foreach (var cupo in cupos.Take(Tamano))
                        Boton($"{Fecha(cupo.FechaCupo)} · {cupo.NumeroCuposDisponibles} cupos · Agenda #{cupo.Id}",
                            Nueva("reserva-servicios", elegido.Id, elegido.TutorId, cupo.Id));
                Paginar(cupos.Count, p => Nueva(solicitud.Accion, solicitud.PerroId, solicitud.TutorId, pagina: p));
                break;
            case "turnos":
                if (veterinario) throw new UnauthorizedAccessException();
                var listado = await turnos.ConsultarVisibles(usuario, false).OrderByDescending(x => x.CupoDiario.FechaCupo).ThenByDescending(x => x.Id)
                    .Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1)
                    .Select(x => new { x.Id, Perro = x.Perro.Nombre, Fecha = x.CupoDiario.FechaCupo, Estado = x.Estado.Descripcion }).ToListAsync();
                tablas.Add(new("Mis turnos", ["Turno", "Perro", "Día", "Estado actual"], listado.Take(Tamano)
                    .Select(x => new[] { x.Id.ToString(), x.Perro, Fecha(x.Fecha), x.Estado }).ToList()));
                mensaje = listado.Count == 0 ? "No hay turnos en esta página." : "Estos son los estados actuales de tus turnos.";
                Paginar(listado.Count, p => Nueva("turnos", pagina: p));
                break;
            case "clinica":
                if (veterinario) throw new UnauthorizedAccessException();
                var clinico = await PerroElegido();
                mensaje = $"Información registrada para {clinico.Nombre}.";
                Boton("Ver patologías y observaciones", Nueva("patologias", clinico.Id));
                Boton("Ver tratamientos y observaciones de las atenciones", Nueva("atenciones", clinico.Id));
                break;
            case "patologias":
                if (veterinario) throw new UnauthorizedAccessException();
                var paciente = await PerroElegido();
                var patologias = await db.PerrosPatologias.AsNoTracking().Where(x => x.PerroId == paciente.Id)
                    .OrderBy(x => x.PatologiaId).Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1)
                    .Select(x => new { x.Patologia.Descripcion, x.Controlado, x.Observacion }).ToListAsync();
                tablas.Add(new("Patologías de " + paciente.Nombre, ["Patología", "Controlado", "Observación"], patologias.Take(Tamano)
                    .Select(x => new[] { x.Descripcion, x.Controlado ? "Sí" : "No", x.Observacion ?? "Sin observaciones" }).ToList()));
                mensaje = patologias.Count == 0 ? "No hay patologías registradas en esta página." : "Patologías y observaciones registradas en la ficha del perro.";
                Paginar(patologias.Count, p => Nueva("patologias", paciente.Id, pagina: p));
                Boton("Ver atenciones", Nueva("atenciones", paciente.Id));
                break;
            case "atenciones":
                if (veterinario) throw new UnauthorizedAccessException();
                var atendido = await PerroElegido();
                var atenciones = await turnos.ConsultarVisibles(usuario, false).Where(x => x.PerroId == atendido.Id && x.EstadoId == EstadosTurno.Completado)
                    .OrderByDescending(x => x.CupoDiario.FechaCupo).ThenByDescending(x => x.Id)
                    .Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1)
                    .Select(x => new { x.Id, x.CupoDiario.FechaCupo, x.Observaciones,
                        Veterinario = x.Veterinario == null ? "No especificado" : x.Veterinario.Usuario.Nombre + " " + x.Veterinario.Usuario.Apellido,
                        Tratamientos = x.Tratamientos.OrderBy(t => t.TratamientoId).Select(t => new { t.Tratamiento.Descripcion, t.Razon }).ToList() }).ToListAsync();
                tablas.Add(new("Atenciones de " + atendido.Nombre, ["Turno", "Día del turno", "Veterinario", "Tratamientos y motivos", "Observaciones"],
                    atenciones.Take(Tamano).Select(x => new[] { x.Id.ToString(), Fecha(x.FechaCupo), x.Veterinario,
                        x.Tratamientos.Count == 0 ? "Sin tratamientos registrados" : string.Join("\n", x.Tratamientos.Select(t => t.Descripcion + (string.IsNullOrWhiteSpace(t.Razon) ? "" : ": " + t.Razon))),
                        x.Observaciones ?? "Sin observaciones" }).ToList()));
                mensaje = atenciones.Count == 0 ? "No hay atenciones completadas en esta página." : "Tratamientos y observaciones de las atenciones completadas, tal como los registró el veterinario.";
                Paginar(atenciones.Count, p => Nueva("atenciones", atendido.Id, pagina: p));
                Boton("Ver patologías", Nueva("patologias", atendido.Id));
                break;
            case "faltas":
                if (!veterinario) throw new UnauthorizedAccessException();
                var tutorElegido = await db.Tutores.AsNoTracking().Where(x => x.Id == solicitud.TutorId && x.Usuario.Activo)
                    .Select(x => new { x.Id, x.Usuario.Nombre, x.Usuario.Apellido }).SingleOrDefaultAsync() ?? throw new UnauthorizedAccessException();
                var faltas = await db.AuditoriaAcciones.AsNoTracking().Where(x => x.TutorId == tutorElegido.Id
                    && x.Entidad == nameof(Turno) && x.Accion == AuditoriaAccion.EstadoTurnoCambiado
                    && x.RolActivo == "Tutor" && x.EstadoNuevoId == EstadosTurno.CanceladoTutor)
                    .OrderByDescending(x => x.FechaUtc).ThenByDescending(x => x.Id)
                    .Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1)
                    .Select(x => new { x.EntidadId, x.FechaUtc }).ToListAsync();
                tablas.Add(new("Cancelaciones del tutor", ["Turno", "Fecha de cancelación (Argentina)"], faltas.Take(Tamano)
                    .Select(x => new[] { x.EntidadId.ToString(), FechaUtc(x.FechaUtc) }).ToList()));
                mensaje = $"Tutor: {tutorElegido.Nombre} {tutorElegido.Apellido} (#{tutorElegido.Id}). Se muestran cancelaciones registradas por el tutor; el sistema no registra inasistencias como un estado separado.";
                Paginar(faltas.Count, p => Nueva("faltas", tutor: tutorElegido.Id, pagina: p));
                Boton("Ver vetos registrados", Nueva("vetos", tutor: tutorElegido.Id));
                break;
            case "vetos":
                if (!veterinario) throw new UnauthorizedAccessException();
                var tutorVeto = await db.Tutores.AsNoTracking().Where(x => x.Id == solicitud.TutorId && x.Usuario.Activo)
                    .Select(x => new { x.Id, x.Usuario.Nombre, x.Usuario.Apellido }).SingleOrDefaultAsync() ?? throw new UnauthorizedAccessException();
                var ahora = DateTime.UtcNow;
                var vetosLista = await db.Vetos.AsNoTracking().Where(x => x.TutorId == tutorVeto.Id)
                    .OrderByDescending(x => x.FechaInicio).ThenByDescending(x => x.Id)
                    .Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1).ToListAsync();
                tablas.Add(new("Vetos registrados", ["Veto", "Inicio (Argentina)", "Fin (Argentina)", "Motivo", "Estado", "Motivo de finalización"], vetosLista.Take(Tamano)
                    .Select(x => new[] { x.Id.ToString(), FechaUtc(x.FechaInicio), x.FechaFin.HasValue ? FechaUtc(x.FechaFin.Value) : "Sin fecha de fin",
                        x.Motivo ?? "Sin motivo", x.FechaInicio <= ahora && (x.FechaFin == null || x.FechaFin > ahora) ? "Vigente" : x.FechaInicio > ahora ? "Programado" : "Finalizado",
                        x.MotivoFinVeto ?? "—" }).ToList()));
                mensaje = $"Vetos del tutor {tutorVeto.Nombre} {tutorVeto.Apellido} (#{tutorVeto.Id}).";
                Paginar(vetosLista.Count, p => Nueva("vetos", tutor: tutorVeto.Id, pagina: p));
                Boton("Ver cancelaciones", Nueva("faltas", tutor: tutorVeto.Id));
                break;
            case "reserva-servicios":
                var mascota = await PerroElegido();
                var servicios = await db.Servicios.AsNoTracking().OrderBy(x => x.Descripcion).ThenBy(x => x.Id)
                    .Skip((solicitud.Pagina - 1) * Tamano).Take(Tamano + 1).ToListAsync();
                var seleccion = await db.Servicios.AsNoTracking().Where(x => solicitud.ServicioIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync();
                if (seleccion.Count != solicitud.ServicioIds.Count) throw new InvalidOperationException("Algún servicio ya no está disponible.");
                mensaje = $"Servicios para {mascota.Nombre}. Tutor: {TutorNombre(mascota)}. Elegidos: "
                    + (seleccion.Count == 0 ? "ninguno." : string.Join(", ", seleccion.Select(x => x.Descripcion)) + ".");
                foreach (var servicio in servicios.Take(Tamano))
                {
                    var marcado = solicitud.ServicioIds.Contains(servicio.Id);
                    var ids = solicitud.ServicioIds.Where(x => x != servicio.Id).ToList();
                    if (!marcado && ids.Count < 20) ids.Add(servicio.Id);
                    else if (!marcado) continue;
                    Boton((marcado ? "Quitar: " : "Agregar: ") + servicio.Descripcion,
                        Nueva("reserva-servicios", mascota.Id, mascota.TutorId, solicitud.CupoId, solicitud.Pagina, ids));
                }
                Paginar(servicios.Count, p => Nueva("reserva-servicios", mascota.Id, mascota.TutorId, solicitud.CupoId, p, solicitud.ServicioIds));
                if (seleccion.Count > 0) Boton("Revisar reserva", Nueva("preparar", mascota.Id, mascota.TutorId, solicitud.CupoId, servicios: solicitud.ServicioIds));
                break;
            case "preparar":
                var reservado = await PerroElegido();
                var fechaReserva = await db.CuposDiarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == solicitud.CupoId
                    && x.FechaCupo >= Reloj.Hoy && x.NumeroCuposDisponibles > 0)
                    ?? throw new InvalidOperationException("El cupo ya no está disponible. Consultá los días nuevamente.");
                var serviciosReserva = await db.Servicios.AsNoTracking().Where(x => solicitud.ServicioIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync();
                if (serviciosReserva.Count == 0 || serviciosReserva.Count != solicitud.ServicioIds.Count)
                    throw new InvalidOperationException("Seleccioná al menos un servicio disponible.");
                var vetado = await vetos.Vigentes().AnyAsync(x => x.TutorId == reservado.TutorId);
                if (vetado && !veterinario) throw new InvalidOperationException("Tenés un veto vigente. Comunicate con la veterinaria para solicitar una reserva presencial.");
                int? profesionalId = veterinario ? await db.Veterinarios.Where(x => x.UserId == usuario && x.Usuario.Activo).Select(x => (int?)x.Id).SingleOrDefaultAsync() : null;
                if (veterinario && profesionalId is null) throw new UnauthorizedAccessException();
                var dto = new TurnoDto { PerroId = reservado.Id, CupoDiarioId = fechaReserva.Id, ServicioIds = serviciosReserva.Select(x => x.Id).ToList(), VeterinarioId = profesionalId };
                var token = pendientes.Preparar(usuario, veterinario, dto, veterinario && vetado);
                tablas.Add(new("Revisar solicitud", ["Perro", "Tutor", "Día", "Servicios", "Modalidad"],
                    [new[] { reservado.Nombre + $" (#{reservado.Id})", TutorNombre(reservado) + $" (#{reservado.TutorId})", Fecha(fechaReserva.FechaCupo),
                        string.Join(", ", serviciosReserva.Select(x => x.Descripcion)), veterinario && vetado ? "Reserva presencial por veto vigente" : veterinario ? "Agendada por veterinario" : "Solicitud del tutor" }]));
                mensaje = "Revisá los datos. Todavía no se guardó el turno ni se descontó un cupo. La propuesta vence en 5 minutos y el turno quedará en estado Solicitado.";
                Boton("Confirmar solicitud de turno", Nueva("confirmar", confirmacion: token));
                Boton("Descartar y volver", Nueva("descartar"));
                break;
            case "confirmar":
                var pendiente = pendientes.Consumir(usuario, veterinario, solicitud.Confirmacion);
                var id = pendiente.Presencial
                    ? await turnos.ReservarPresencialAsync(pendiente.Turno, usuario)
                    : await turnos.GuardarAsync(pendiente.Turno, usuario, veterinario);
                mensaje = $"Turno #{id} solicitado correctamente. Estado: Solicitado. Se descontó un cupo.";
                if (!veterinario) Boton("Ver mis turnos", Nueva("turnos"));
                break;
            default:
                throw new InvalidOperationException("La acción indicada no está disponible.");
        }
        if (solicitud.Accion is not ("inicio" or "descartar")) Boton("Volver al inicio del asistente", Nueva("inicio"));
        return new(mensaje, tablas, acciones);
    }
}
