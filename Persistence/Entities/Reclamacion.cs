using System;
using System.ComponentModel.DataAnnotations;

namespace Votify.Entities
{
    public partial class Reclamacion
    {
        public const string EstadoPendiente = "PENDIENTE";
        public const string EstadoResuelta = "RESUELTA";
        public const string EstadoRechazada = "RECHAZADA";

        [Key]
        public int Id { get; set; }
        public int EventoId { get; set; }
        public int UsuarioId { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string Estado { get; set; }
        public string? RespuestaOrganizador { get; set; }
        public DateTime? FechaRespuesta { get; set; }

        public virtual Evento evento { get; set; }
        public virtual Usuario usuario { get; set; }
    }
}
