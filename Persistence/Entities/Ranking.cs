using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Ranking
    {
        //Atributos
        public int Posicion { get; set; }
        public double PuntajeTotal { get; set; }
        public bool EsManual { get; set; } // si fue intervenido manualmente por el jurado

        //Relaciones
        public Proyecto proyecto;
        public Votacion votacion;
    }
}
