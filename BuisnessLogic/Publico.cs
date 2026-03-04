using System;
using System.Collections.Generic;
using System.Text;
using Votify.BusinessLogic;

namespace Votify.BusinessLogic
{
    internal class Publico : Votante
    {
        public Publico(double rawScore) : base(rawScore) { }

        public override string RolVotante() => "PUBLIC";
        public override double NormalizedScore() => rawScore * 0.85;
    }
}
