using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Reglas
    {
        public Reglas() { }
        public Reglas(string descripcion, int configPuntos, int maxVotosPersona)
        {
            Descripcion = descripcion;
            ConfigPuntos = configPuntos;
            MaxVotosPersona = maxVotosPersona;
        }
    }
}
