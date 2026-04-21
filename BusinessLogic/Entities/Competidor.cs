using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Competidor : Rol
    {
        public Competidor() : base()
        {
            TipoRol = "COMPETIDOR";
        }
        public Competidor(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore)
        {
            TipoRol = "COMPETIDOR";
        }

        public override double NormalizedScore()
        {
            return rawScore;
        }

        //Identificador de tipo (usado por RolFactory)
        public override string RolVotante() => "COMPETIDOR";
    }
}
