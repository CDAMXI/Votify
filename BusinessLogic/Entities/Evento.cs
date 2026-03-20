using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Evento
    {
        public Evento() { }
        public Evento(int idEvento, string nombre, DateTime fechaIni, DateTime fechaFin, string descripcion)
        {
            IdEvento = idEvento;
            Nombre = nombre;
            FechaIni = fechaIni;
            FechaFin = fechaFin;
            Descripcion = descripcion;
        }
    }
}
