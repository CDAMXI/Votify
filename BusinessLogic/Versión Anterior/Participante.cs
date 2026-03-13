using System;
using System.Collections.Generic;
using System.Text;
using Votify.BusinessLogic;
using Votify.BusinessLogic.Entities;

namespace Votify.BuisnessLogic
{
    internal class Participante : Usuario
    {
        public Participante(double rawScore) : base(rawScore)
        {
        }

        public override double NormalizedScore()
        {
            throw new NotImplementedException();
        }

        public override string RolVotante()
        {
            throw new NotImplementedException();
        }
    }
}
