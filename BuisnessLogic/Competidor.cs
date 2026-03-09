using System;
using System.Collections.Generic;
using System.Text;
using Votify.BusinessLogic;

namespace Votify.BuisnessLogic
{
    internal class Competidor : Usuario
    {
        public Competidor(double rawScore) : base(rawScore)
        {
        }

        public override double NormalizedScore()
        {
            throw new NotImplementedException();
        }

        public override string RolVotante() => "COMPETIDOR";
    }
}
