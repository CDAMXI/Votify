using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.BusinessLogic
{
    internal abstract class Usuario
    {
        protected double rawScore; // campo compartido

        public Usuario(double rawScore)
        {
            this.rawScore = rawScore;
        }

        protected double RawScore() { return this.rawScore; }

        public abstract string RolVotante();
        public abstract double NormalizedScore();
    }
}