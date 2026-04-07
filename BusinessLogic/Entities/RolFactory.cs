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
                "EXPERT" => new Jurado(fechaAsignacion, rawScore),
                "COMPETITOR" => new Competidor(fechaAsignacion, rawScore),
                "ORGANIZER" => new Organizador(fechaAsignacion, rawScore),
                "VOTING_MANAGER" => new EncargadoVotacion(fechaAsignacion, rawScore),
                "PUBLIC" => new Publico(fechaAsignacion, rawScore),
                _ => throw new ServiceException($"Tipo de rol desconocido: {tipoRol}")
            };
        }
    }
}
