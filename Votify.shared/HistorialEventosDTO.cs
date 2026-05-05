using System;
using System.Collections.Generic;

namespace Votify.shared
{
    public class HistorialEventosDTO
    {
        public int EventosParticipados { get; set; }
        public int VotosEmitidos { get; set; }
        public List<EventoHistorialItemDTO> Eventos { get; set; } = new();
    }

    public class EventoHistorialItemDTO
    {
        public int IdEvento { get; set; }
        public string Nombre { get; set; } = "";
        public DateTime FechaIni { get; set; }
        public DateTime FechaFin { get; set; }
        public string? RolUsuario { get; set; }
        public bool Voto { get; set; }
        public string? ProyectoDestacado { get; set; }
        public int? PosicionProyecto { get; set; }
        public int? TotalProyectos { get; set; }
        public bool YaReclamado { get; set; }
        public string? EstadoReclamacion { get; set; }
    }
}
