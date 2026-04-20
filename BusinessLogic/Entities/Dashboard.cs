using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Dashboard
    {
        public Dashboard() { }

        public Dashboard(int puntuacionGlobal, int puntuacionPorDimencion)
        {
            PuntuacionGlobal = puntuacionGlobal;
            PuntuacionPorDimencion = puntuacionPorDimencion;
            Descripcion = string.Empty;
        }
    }
}
