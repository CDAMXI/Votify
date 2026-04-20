using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class HojaRuta
    {
        //Atributos
        public int Id { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaGeneracion { get; set; }

        //Relaciones
        public virtual Competidor competidor { get; set; }
    }
}
