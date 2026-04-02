namespace Votify.shared
{
    public class VotoDTO
    {
        //lo que se me ocurrió¿
        public int VotacionId { get; set; }
        public int PropuestaId { get; set; }
        public required string UsuarioId { get; set; }
        public DateTime FechaVoto { get; set; }
        public double Puntuacion { get; set; }
        public string? Comentario { get; set; }
    }
    }

