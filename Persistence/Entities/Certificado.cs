using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Certificado
    {
        //Atributos
        public int IdCertificado { get; set; }
        public string Tipo { get; set; }
        public DateTime FechaEmision { get; set; }

        //Relaciones
        ICollection<Competidor> competidores;
    }
}
