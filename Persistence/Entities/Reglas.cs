using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Votify.Entities
{
    public partial class Reglas
    {
        [Key]
        public int Id { get; set; }
        public string Descripcion { get; set; }
        public int ConfigPuntos { get; set; }
        public int MaxVotosPersona { get; set; }

        public virtual Evento evento { get; set; }
    }
}
