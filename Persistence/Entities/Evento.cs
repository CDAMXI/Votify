using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Evento
    {
        //Atributos
        public int IdEvento { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaIni { get; set; }
        public DateTime FechaFin { get; set; }
        public string Descripcion { get; set; }

        //Relaciones
    }
}
