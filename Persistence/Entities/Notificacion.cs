using System;
using System.ComponentModel.DataAnnotations;

namespace Votify.Entities
{
    public partial class Notificacion
    {
        [Key]
        public int Id { get; set; }
        public int RemitenteId { get; set; }
        public int DestinatarioId { get; set; }
        public string Asunto { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public bool Leida { get; set; }

        public virtual Usuario remitente { get; set; }
        public virtual Usuario destinatario { get; set; }
    }
}
