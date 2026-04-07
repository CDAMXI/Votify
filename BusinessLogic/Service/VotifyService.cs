using Votify.Persistence;
using Votify.Entities;


namespace Votify.BuisnessLogic.Service
{
    public class VotifyService : IVotifyService
    {
        public Usuario usuario;
        public Rol rol;
        private readonly IDAL dal;
        public VotifyService(IDAL dal)
        {
            this.dal = dal;
        }
        
        public void LogIn(String user, String password)
        {
            Usuario User = dal.GetById<Usuario>(user);
            if(User != null && password == User.Password)
            {
                usuario = User;
                rol = User.roles?.FirstOrDefault();
            }
            else throw new ServiceException("Usuario o contraseña no válidos");
        }
        public void LogOut()
        {
            if (usuario != null)
            {
                usuario = null;
                rol = null;
            }
            else throw new ServiceException("No hay ningún usuario logueado");
        }

        /**
         * Este método a lo mejor no es necesario
         * public String getUser(){
         *      if(usuario is Competidor){
         *          return "Competidor";
         *      }
         *      if(usuario is Jurado){
         *          return "Jurado";
         *      }
         *      if(usuario is Publico){
         *          return "Público";
         *      }
         *      if(usuario is Organizador){
         *          return "Organizador";
         *      }
         *      if(usuario is EncargadoVotacion){
         *          return "Encargado de votación";
         *      }
         *      throw new ServiceException("No se ha encontrado al usuario");
         * }
         */

        public void Registrar(string username, string email, string password)
        {
            Usuario existingUser = dal.GetById<Usuario>(username);
            if (existingUser != null)
                throw new ServiceException("El usuario ya existe");

            Usuario newUser = new Usuario(username, email, password, 0);
            dal.Insert<Usuario>(newUser);
            dal.Commit();
        }

        public Usuario GetUsuarioActual()
        {
            return usuario;
        }

        public void GuardarVoto(int idVotacion, int idCompetidor, double puntuacion, string? comentario)
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");

            if (rol == null)
                throw new ServiceException("No hay ningún rol activo");

            if (rol is Organizador || rol is EncargadoVotacion)
                throw new ServiceException("El rol actual no puede votar");

            if (comentario != null && comentario.Length > 500)
                throw new ServiceException("El comentario no puede superar los 500 caracteres");

            Votacion votacion = dal.GetById<Votacion>(idVotacion);
            if (votacion == null)
                throw new ServiceException("La votación no existe");

            Proyecto proyecto = dal.GetById<Proyecto>(idCompetidor);
            if (proyecto == null)
                throw new ServiceException("El proyecto no existe");

            Evento evento = votacion.evento;
            if (evento == null)
                throw new ServiceException("La votación no está asociada a ningún evento");


            if (rol is Competidor && !evento.PermiteCompetidoresVotar)
                throw new ServiceException("Los competidores no pueden votar en este evento");

            Voto voto = new Voto(puntuacion, comentario ?? string.Empty, DateTime.Now);
            voto.votacion = votacion;
            voto.proyecto = proyecto;
            voto.votante = rol;

            dal.Insert<Voto>(voto);
            dal.Commit();
        }

        public void Commit()
        {
            dal.Commit();
        }

        public void crearVotoación(DateTime end, bool status)
        {
            if (rol is EncargadoVotacion)
            {
                EncargadoVotacion encargado = rol as EncargadoVotacion;
                Votacion votacion = new Votacion(DateTime.Now, end, status, encargado);
                dal.Insert<Votacion>(votacion);
                dal.Commit();
                encargado.votaciones.Add(votacion);
                dal.Insert<EncargadoVotacion>(encargado);
            }

        }
        public void borrarVotacion(int idVotacion)
        {
            Votacion votacion = dal.GetById<Votacion>(idVotacion);
            EncargadoVotacion encargado = rol as EncargadoVotacion;
            if (votacion.Encargado == rol)
            {
                dal.Delete<Votacion>(votacion);
                dal.Commit();
            }
            else throw new ServiceException("No es Encargado de la votacion");
        }

        public void modificarFecha(int votacionId, DateTime newEnd)
        {
            Votacion votacion = dal.GetById<Votacion>(votacionId);
            EncargadoVotacion encargado = rol as EncargadoVotacion;
            if (votacion.Encargado == rol)
            {
                votacion.FechaFin = newEnd;
                dal.Insert<Votacion>(votacion);
                dal.Commit();
            }
            else throw new ServiceException("No es Encargado de votación");
        }
        public Rol GetRolEnEvento(int idEvento)
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");

            return usuario.roles?.FirstOrDefault(r => r.evento?.IdEvento == idEvento);
        }
    }
}
