using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Categoria
    {
        //Atributos
        public string Nombre { get; set; }
        public string Descripcion { get; set; }

        //Relaciones
        public Evento evento;
        public ICollection<Premios> premios;
    }
}
