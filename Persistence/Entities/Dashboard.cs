using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Dashboard
    {
        //Atributos
        public int Id { get; set; }
        public double PuntuacionGlobal { get; set; }
        public int PuntuacionPorDimencion { get; set; }
        public string Descripcion { get; set; }

        //Relaciones
        public virtual Competidor competidor { get; set; }
    }
}
