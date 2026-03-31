using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Reglas
    {
        //Atributos
        public string Descripcion { get; set; }
        public int ConfigPuntos { get; set; }
        public int MaxVotosPersona { get; set; }

        //Relaciones
        public Evento evento;
    }
}
