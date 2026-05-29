using System;
using System.Collections.Generic;
using System.Text;
using Votify.Entities.EstadosVotacion;

namespace Votify.Entities
{
    public partial class Votacion
    {
        public Votacion() { }
        public Votacion(DateTime fechaIni, DateTime fechaFin, string estado, EncargadoVotacion encargado)
        {
            //Id = id;
            Titulo = "Votacion";
            Descripcion = string.Empty;
            FechaIni = fechaIni;
            FechaFin = fechaFin;
            NombreEstado = estado ?? "Activa";
            Encargado = encargado;
            PesoJurado = 70;
            PesoPublico = 30;
        }

        public void Pausar() => EstadoActual.Pausar(this);
        public void Reanudar() => EstadoActual.Reanudar(this);
        public void Cerrar() => EstadoActual.Cerrar(this);
        public bool PuedeVotar() => EstadoActual.PuedeVotar();
    }
}
