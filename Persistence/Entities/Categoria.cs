using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Votify.Entities
{
    public partial class Categoria
    {
        [Key]
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }

        public virtual Evento evento { get; set; }
        public virtual ICollection<Premios> premios { get; set; }
    }
}
