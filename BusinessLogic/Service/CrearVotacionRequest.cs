using System;
using System.Collections.Generic;
using System.Text;
using System;
using System.Collections.Generic;

namespace Votify.BusinessLogic.Service
{
    public sealed class CrearVotacionRequest
    {
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public DateTime FechaFin { get; set; }
        public bool Activa { get; set; }
        public bool PermiteCompetidoresVotar { get; set; }
        public int PesoJurado { get; set; } = 70;
        public int PesoPublico { get; set; } = 30;
        public string? CodigoEncargado { get; set; }
        public string? CodigoJurado { get; set; }
        public List<string> CorreosEncargados { get; set; } = new();
        public List<string> CorreosJurados { get; set; } = new();
        public List<string> Categorias { get; set; } = new();
    }
}
