using Votify.Persistence;
using Votify.Entities;


namespace Votify.BuisnessLogic.Service
{
    internal class VotifyService : IVotifyService
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
            }
            else throw new ServiceException("Usuario o contraseña no válidos");
        }
        public void LogOut()
        {
            if (usuario != null)
            {
                usuario = null;
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
            throw new NotImplementedException();
        }

        public Usuario GetUsuarioActual()
        {
            return usuario;
        }

        public void GuardarVoto(int idVotacion, int idCompetidor, double puntuacion)
        {
            throw new NotImplementedException();
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
    }
}
