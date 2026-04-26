namespace Votify.shared
{
    public class MonitorVotacionDTO
    {
        public int VotosEmitidos { get; set; }
        public int TotalVotantes { get; set; }
        public double PromedioGeneral { get; set; }
        public List<ProyectoMonitorDTO> Proyectos { get; set; } = new();
        public List<VotanteEstadoDTO> Votantes { get; set; } = new();
        public List<VotoHistorialDTO> Historial { get; set; } = new();
    }

    public class ProyectoMonitorDTO
    {
        public string Nombre { get; set; } = "";
        public double Media { get; set; }
        public int NumVotos { get; set; }
    }

    public class VotanteEstadoDTO
    {
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "";
        public bool HaVotado { get; set; }
    }

    public class VotoHistorialDTO
    {
        public string Votante { get; set; } = "";
        public string Proyecto { get; set; } = "";
        public double Valor { get; set; }
        public string? Comentario { get; set; }
        public DateTime Fecha { get; set; }
    }
}
