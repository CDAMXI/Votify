using System;
using System.Collections.Generic;
using System.Text;
using Votify.BusinessLogic;

namespace Votify.BuisnessLogic
{
    internal class Publico : Participante
    {
        public Publico(double rawScore) : base(rawScore)
        {
        }

        public override string RolVotante() => "PUBLIC";
        public override double NormalizedScore() => rawScore * 0.8;
    }
}
