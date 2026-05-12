using System;
using System.Collections.Generic;
using System.Text;
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
            IDAL<Reclamacion> reclamaciones)
        {
            Usuarios = usuarios;
            Votos = votos;
            Votaciones = votaciones;
            Eventos = eventos;
            Roles = roles;
            Proyectos = proyectos;
            Jurados = jurados;
            Publicos = publicos;
            Competidores = competidores;
            Organizadores = organizadores;
            Encargados = encargados;
            Reclamaciones = reclamaciones;
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
    }
}
