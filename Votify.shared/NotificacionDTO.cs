using System;

namespace Votify.shared
{
    public class NotificacionDTO
    {
        public int Id { get; set; }
        public string Asunto { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public bool Leida { get; set; }
        public string RemitenteUsername { get; set; } = string.Empty;
        public string DestinatarioUsername { get; set; } = string.Empty;
    }
}
