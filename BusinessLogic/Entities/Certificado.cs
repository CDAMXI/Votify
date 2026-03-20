using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Certificado
    {
        public Certificado() { }
        public Certificado (int idCertificado, string tipo, DateTime fechaEmision){
            IdCertificado = idCertificado;
            Tipo = tipo;
            FechaEmision = fechaEmision;
        }
    }
}
