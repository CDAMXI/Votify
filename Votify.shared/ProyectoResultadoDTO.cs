namespace Votify.shared
{
    public class ProyectoResultadoDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Competidor { get; set; } = "";
        public string Categoria { get; set; } = "";
        public List<string> Participantes { get; set; } = new();
        public double Media { get; set; }
        public double MediaBruta { get; set; }
        public double MediaJurado { get; set; }
        public double MediaPopular { get; set; }
        public int NumVotos { get; set; }
        public int NumVotosJurado { get; set; }
        public int NumVotosPopular { get; set; }
        public int PesoJuradoAplicado { get; set; }
        public int PesoPublicoAplicado { get; set; }
        public int Rank { get; set; }
    }
}
