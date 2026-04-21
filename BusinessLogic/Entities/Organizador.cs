using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Organizador : Rol
    {
        public Organizador() : base()
        {
            TipoRol = "ORGANIZADOR";
        }
        public Organizador(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore)
        {
            TipoRol = "ORGANIZADOR";
        }

        //Identificador de tipo (usado por RolFactory)
        public override string RolVotante() => "ORGANIZADOR";
        public override double NormalizedScore() => rawScore;
    }
}
