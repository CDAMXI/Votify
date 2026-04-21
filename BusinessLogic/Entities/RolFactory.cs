using System;
using System.Collections.Generic;
using System.Text;
using Votify.BusinessLogic.Service;
using Votify.Entities;

namespace Votify.Entities
{
    public static class RolFactory
    {
        public static Rol Create(string tipoRol, DateTime fechaAsignacion, double rawScore)
        {
            return tipoRol switch
            {
                "JURADO" => new Jurado(fechaAsignacion, rawScore),
                "COMPETIDOR" => new Competidor(fechaAsignacion, rawScore),
                "ORGANIZADOR" => new Organizador(fechaAsignacion, rawScore),
                "ENCARGADO" => new EncargadoVotacion(fechaAsignacion, rawScore),
                "PUBLICO" => new Publico(fechaAsignacion, rawScore),
                _ => throw new ServiceException($"Tipo de rol desconocido: {tipoRol}")
            };
        }
    }
}
