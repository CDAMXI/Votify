using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.BusinessLogic
{
    internal class Jurado : Usuario
    {
        public Jurado(double rawScore) : base(rawScore) { }

        public override string RolVotante() => "EXPERT";
        public override double NormalizedScore() => rawScore * 1.2;
    }
}
