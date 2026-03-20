using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public abstract partial class EncargadoVotacion : Rol
    {
        public EncargadoVotacion() : base() { }
        public EncargadoVotacion(DateTime fechaAsignacion, double rawScore) : base(fechaAsignacion, rawScore) { }
    }
}
