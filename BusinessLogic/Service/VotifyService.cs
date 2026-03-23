using Votify.Persistence;
using Votify.Entities;


namespace Votify.BuisnessLogic.Service
{
    internal class VotifyService : IVotifyService
    {
        public Usuario usuario;
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
           Votacion votacion = new Votacion(DateTime.Now, end, status);
           dal.Insert<Votacion>(votacion);
        }
        public void borrarVotacion(int idVotacion)
        {
            Votacion vot = dal.GetById<Votacion>(idVotacion);
            if (usuario is EncargadoVotacion)
            {
                dal.Delete<Votacion>(vot);
            }
            else throw new ServiceException("No es Encargado de la votacion");
        }
    }
}
