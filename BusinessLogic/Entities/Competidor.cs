using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Competidor : Rol
    {
        public Competidor() : base() { }
        public Competidor(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore)
        {
        }

        public override double NormalizedScore()
        {
            throw new NotImplementedException();
        }

        public override string RolVotante() => "COMPETITOR";
    }
}
