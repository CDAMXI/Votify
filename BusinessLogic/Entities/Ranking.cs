using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Ranking
    {
        public Ranking() { }
        public Ranking(int posicion, double puntajeTotal, bool esManual)
        {
            Posicion = posicion;
            PuntajeTotal = puntajeTotal;
            EsManual = esManual;
        }
    }
}
