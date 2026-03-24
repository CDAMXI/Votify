using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Votacion
    {
        public Votacion() { }
        public Votacion(DateTime fechaIni, DateTime fechaFin, bool estado)
        {
            //Id = id;
            FechaIni = fechaIni;
            FechaFin = fechaFin;
            Estado = estado;
        }
    }
}
