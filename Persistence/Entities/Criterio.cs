using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public enum TipoCriterio
    {
        NUMERICO,
        CHECKLIST,
        RUBRICA,
        COMENTARIO,
        AUDIO,
        VIDEO
    }

    public partial class Criterio
    {
        //Atributos
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public double Peso { get; set; }
        public TipoCriterio Tipo { get; set; }

        //Relaciones
        public Votacion votacion;
    }
}