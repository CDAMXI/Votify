using System;
using System.Collections.Generic;
using Votify.Entities;
using Votify.Persistence;

namespace Votify.BusinessLogic.Service
{
    public sealed class VotifyRepositories
    {
        public VotifyRepositories(
            IDAL<Usuario> usuarios,
            IDAL<Voto> votos,
            IDAL<Votacion> votaciones,
            IDAL<Evento> eventos,
            IDAL<Rol> roles,
            IDAL<Proyecto> proyectos,
            IDAL<Jurado> jurados,
            IDAL<Publico> publicos,
            IDAL<Competidor> competidores,
            IDAL<Organizador> organizadores,
            IDAL<EncargadoVotacion> encargados,
            IDAL<Reclamacion> reclamaciones,
            IDAL<Notificacion>? notificaciones = null)
        {
            Usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
            Votos = votos ?? throw new ArgumentNullException(nameof(votos));
            Votaciones = votaciones ?? throw new ArgumentNullException(nameof(votaciones));
            Eventos = eventos ?? throw new ArgumentNullException(nameof(eventos));
            Roles = roles ?? throw new ArgumentNullException(nameof(roles));
            Proyectos = proyectos ?? throw new ArgumentNullException(nameof(proyectos));
            Jurados = jurados ?? throw new ArgumentNullException(nameof(jurados));
            Publicos = publicos ?? throw new ArgumentNullException(nameof(publicos));
            Competidores = competidores ?? throw new ArgumentNullException(nameof(competidores));
            Organizadores = organizadores ?? throw new ArgumentNullException(nameof(organizadores));
            Encargados = encargados ?? throw new ArgumentNullException(nameof(encargados));
            Reclamaciones = reclamaciones ?? throw new ArgumentNullException(nameof(reclamaciones));
            Notificaciones = notificaciones; // opcional — puede ser null
        }

        public IDAL<Usuario> Usuarios { get; }

        public IDAL<Voto> Votos { get; }

        public IDAL<Votacion> Votaciones { get; }

        public IDAL<Evento> Eventos { get; }

        public IDAL<Rol> Roles { get; }

        public IDAL<Proyecto> Proyectos { get; }

        public IDAL<Jurado> Jurados { get; }

        public IDAL<Publico> Publicos { get; }

        public IDAL<Competidor> Competidores { get; }

        public IDAL<Organizador> Organizadores { get; }

        public IDAL<EncargadoVotacion> Encargados { get; }

        public IDAL<Reclamacion> Reclamaciones { get; }

        public IDAL<Notificacion>? Notificaciones { get; }
    }
}