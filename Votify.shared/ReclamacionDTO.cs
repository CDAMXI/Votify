using System;

namespace Votify.shared
{
    public class ReclamacionDTO
    {
        public int Id { get; set; }
        public int EventoId { get; set; }
        public string EventoNombre { get; set; } = "";
        public string Solicitante { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
        public string Estado { get; set; } = "";
        public string? RespuestaOrganizador { get; set; }
        public DateTime? FechaRespuesta { get; set; }
    }

    public class CrearReclamacionRequest
    {
        public int EventoId { get; set; }
        public string Descripcion { get; set; } = "";
    }

    public class ResponderReclamacionRequest
    {
        public string Estado { get; set; } = "";
        public string? Respuesta { get; set; }
    }
}
