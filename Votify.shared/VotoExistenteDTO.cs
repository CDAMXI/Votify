using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.shared
{
    public class VotoExistenteDTO
    {
        public string Id { get; set; }
        public double Valor { get; set; }
        public string? Comentario { get; set; }
        public DateTime Fecha { get; set; }
    }
}
