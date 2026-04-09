using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Votify.Entities
{
    public partial class Sugerencias
    {
        [Key]
        public int Id { get; set; }
        public string Contenido { get; set; }
        public DateTime Fecha { get; set; }

        public virtual Evento evento { get; set; }
    }
}
