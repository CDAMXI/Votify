using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Jurado : Rol
    {
        public Jurado() : base() { }
        public Jurado(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore) { }

        public override string RolVotante() => "EXPERT";
        public override double NormalizedScore() => rawScore * 1.2;
    }
}
