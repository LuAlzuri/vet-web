# DER de VetWeb

Relaciones del dominio vigentes. Las tablas de roles, claims y tokens de Identity conservan el esquema estándar. Una cuenta puede tener perfiles de Tutor y Veterinario; la autorización usa únicamente el rol activo.

```mermaid
erDiagram
    ApplicationUser ||--o| Tutor : tiene
    ApplicationUser ||--o| Veterinario : tiene
    Tutor ||--o{ TelefonoTutor : tiene
    Tutor ||--o{ Perro : tiene
    GrupoEtario ||--o{ Perro : clasifica
    Tamanio ||--o{ Perro : clasifica
    Perro ||--o| Imagen : tiene
    Perro ||--o{ PerroPatologia : presenta
    Patologia ||--o{ PerroPatologia : identifica
    Perro ||--o{ Turno : recibe
    CupoDiario ||--o{ Turno : asigna
    Veterinario o|--o{ Turno : atiende
    Estado ||--o{ Turno : identifica
    Turno ||--o{ ServicioPorTurno : solicita
    Servicio ||--o{ ServicioPorTurno : identifica
    Turno ||--o{ TratamientoPorTurno : registra
    Tratamiento ||--o{ TratamientoPorTurno : identifica
    Tutor ||--o{ Veto : recibe
    Veterinario o|--o{ Veto : aplica
    Tutor ||--o{ AuditoriaAccion : afecta
    ApplicationUser ||--o{ AuditoriaAccion : dispara
    TratamientoPorTurno {
        int TurnoId PK,FK
        int TratamientoId PK,FK
        string Razon "opcional, hasta 1000 caracteres"
    }
    ServicioPorTurno {
        int TurnoId PK,FK
        int ServicioId PK,FK
        bool Completado
    }
```

`AgregarTratamientoPorTurno` reemplaza `Turnos.TratamientoId` por la tabla asociativa de clave compuesta. Cada tratamiento puede registrarse una sola vez por turno, con un motivo independiente. Las FK restringen el borrado de registros referenciados. La migración copia el tratamiento anterior sin inventar motivos. La reversión conserva el dato cuando aún es representable por el modelo anterior y falla explícitamente si existen motivos o varios tratamientos por turno.

El tratamiento previsto de un turno activo usa la misma relación y se revisa al registrar la atención. El cierre confirma el conjunto definitivo; los turnos finalizados y sus asociaciones son de consulta. Las bajas lógicas no eliminan el historial. Las fechas reales de solicitud y finalización permanecen en `AuditoriaAcciones`; no se agregan fechas ficticias a turnos anteriores.
