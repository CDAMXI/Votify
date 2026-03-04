using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Votify.BuisnessLogic;

namespace Votify.BusinessLogic
{
    internal abstract class Votante : Usuario
    {
        protected double rawScore;  // campo compartido

        public Votante(double rawScore)
        {
            this.rawScore = rawScore;
        }

        protected double RawScore() { return this.rawScore; }

        public abstract string RolVotante();
        public abstract double NormalizedScore();
    }
}
