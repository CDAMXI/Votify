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
            Usuario User = null;
            //Restricción: El usuario no debe contener '@' para no confundir con mail
            if (user.Contains('@'))
            {
                //User = dal.getWhere<Uusario>(x => x.email == user)
            }
            else
            {
               //User = dal.GetById<Usuario>(user);
            }
            //if(User != null && password == User.password)
            if (User != null)
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
    }
}
