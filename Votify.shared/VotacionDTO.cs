using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.shared
{
    public class VotacionDTO
    {
        public int Id { get;  set; }
        public int IdEvento { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaIni {  get; set; }
        public DateTime FechaFin { get; set; }
        public bool Estado { get; set; }
    }
}
