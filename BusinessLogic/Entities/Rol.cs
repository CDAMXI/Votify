using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public abstract partial class Rol
    {
        public double rawScore; // campo compartido
        public Rol() { }

        public Rol(DateTime fechaAsignacion, double rawScore)
        {
            FechaAsignacion = fechaAsignacion;
            this.rawScore = rawScore;
        }
        protected double RawScore() { return this.rawScore; }

        public abstract string RolVotante();
        public abstract double NormalizedScore();
    }
}
