using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.shared
{
    public class VotacionDTO
    {
        public int Id { get;  set; }
        public int IdEvento { get; set; }
        public string NombreEvento { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaIni {  get; set; }
        public DateTime FechaFin { get; set; }
        public string NombreEstado { get; set; }
        public string? RolActual { get; set; }

        // Campos de configuración del evento
        public bool PermiteCompetidoresVotar { get; set; } = false;
        public string CodigoEncargado { get; set; } = string.Empty;
        public string CodigoJurado { get; set; } = string.Empty;
        public int PesoJurado { get; set; } = 70;
        public int PesoPublico { get; set; } = 30;
        public List<string> CorreosEncargados { get; set; } = new();
        public List<string> CorreosJurados { get; set; } = new();
        public List<CategoriaBaremoDTO> Categorias { get; set; } = new();
    }
}
