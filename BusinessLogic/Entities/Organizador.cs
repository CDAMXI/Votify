using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public abstract partial class Organizador : Rol
    {
        public Organizador() : base() { }
        public Organizador(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore) { }
    }
}
