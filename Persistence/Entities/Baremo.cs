using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

    public partial class Baremo
    {
        [Key]
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public double Peso { get; set; }
        public TipoCriterio Tipo { get; set; }

        public virtual Votacion votacion { get; set; }
    }
}
