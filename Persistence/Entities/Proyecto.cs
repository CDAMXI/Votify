using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Proyecto
    {
        //Atributos
        public int IdProyecto { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public ICollection<string> Materiales { get; set; }

        //Relaciones
    }
}
