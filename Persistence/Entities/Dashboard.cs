using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Dashboard
    {
        //Atributos
        public int PuntuacionGlobal { get; set; }
        public int PuntuacionPorDimencion { get; set; }

        //Relaciones
        public HojaRuta hojaRuta;
        public Competidor competidor;
    }
}
