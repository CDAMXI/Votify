namespace Votify.shared
{
    public class VotoDTO
    {
        //lo que se me ocurrió¿
            public int PropuestaId { get; set; }
            public required string UsuarioId { get; set; }
            public DateTime FechaVoto { get; set; }
        }
    }

