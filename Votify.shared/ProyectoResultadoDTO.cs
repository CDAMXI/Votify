namespace Votify.shared
{
    public class ProyectoResultadoDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Competidor { get; set; } = "";
        public List<string> Participantes { get; set; } = new();
        public double Media { get; set; }
        public int NumVotos { get; set; }
        public int Rank { get; set; }
    }
}
