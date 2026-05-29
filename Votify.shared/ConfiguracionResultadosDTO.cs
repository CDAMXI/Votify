namespace Votify.shared
{
    public class ConfiguracionResultadosDTO
    {
        public int PesoJurado { get; set; } = 70;
        public int PesoPublico { get; set; } = 30;
        public string EstrategiaCalculo { get; set; } = "ESTANDAR";
    }
}
