using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.shared
{
    public class VotacionDTO
    {
        public int Id { get;  set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaIni {  get; set; }
        public DateTime FechaFin { get; set; }
    }
}
