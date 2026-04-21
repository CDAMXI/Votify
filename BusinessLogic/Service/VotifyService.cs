using Votify.Persistence;
using Votify.Entities;


namespace Votify.BusinessLogic.Service
{
    public class VotifyService : IVotifyService
    {
        public Usuario? usuario;
        public Rol? rol;
        private readonly IDAL dal;
        public VotifyService(IDAL dal)
        {
            this.dal = dal;
        }
        // Usamos el método fábrica
        public void LogIn(String user, String password)
        {
            Usuario User = dal.GetWhere<Usuario>(u => u.Username == user).FirstOrDefault();
            if (User != null && password == User.Password)
            {
                usuario = User;
                Rol rolRecuperado = User.roles?.FirstOrDefault();
                if (rolRecuperado != null)
                    rol = RolFactory.Create(rolRecuperado.RolVotante(), rolRecuperado.FechaAsignacion, rolRecuperado.RawScore());
                else
                    rol = null;
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

        public (string Username, string Email, string? FotoPerfil) GetPerfil()
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");
            return (usuario.Username, usuario.Email, usuario.FotoPerfil);
        }

        public void UpdateEmail(string nuevoEmail)
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");
            usuario.Email = nuevoEmail;
            dal.Commit();
        }

        public void UpdatePassword(string passwordActual, string nuevaPassword)
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");
            if (usuario.Password != passwordActual)
                throw new ServiceException("La contraseña actual no es correcta");
            usuario.Password = nuevaPassword;
            dal.Commit();
        }

        public void UpdateFotoPerfil(string base64Foto)
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");
            usuario.FotoPerfil = base64Foto;
            dal.Commit();
        }

        public string GeneratePasswordResetToken(string email)
        {
            Usuario user = dal.GetWhere<Usuario>(u => u.Email == email).FirstOrDefault();
            if (user == null)
                throw new ServiceException("No existe ninguna cuenta con ese correo");

            string token = Guid.NewGuid().ToString("N");
            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(10);
            dal.Commit();
            return token;
        }

        public void ResetPassword(string token, string nuevaPassword)
        {
            Usuario user = dal.GetWhere<Usuario>(u => u.ResetToken == token).FirstOrDefault();
            if (user == null)
                throw new ServiceException("El enlace no es válido");

            if (user.ResetTokenExpiry < DateTime.UtcNow)
                throw new ServiceException("El enlace ha expirado");

            user.Password = nuevaPassword;
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            dal.Commit();
        }

        public void RestoreSession(string username)
        {
            Usuario user = dal.GetWhere<Usuario>(u => u.Username == username).FirstOrDefault();
            if (user == null)
                throw new ServiceException("Usuario no encontrado");

            usuario = user;
            Rol rolRecuperado = user.roles?.FirstOrDefault();
            if (rolRecuperado != null)
                rol = RolFactory.Create(rolRecuperado.RolVotante(), rolRecuperado.FechaAsignacion, rolRecuperado.RawScore());
            else
                rol = null;
        }

        public void Registrar(string username, string email, string password)
        {
            Usuario existingUser = dal.GetWhere<Usuario>(u => u.Username == username).FirstOrDefault();
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

            bool yaVotó = dal.GetWhere<Voto>(v => v.votante == rol && v.votacion == votacion && v.proyecto == proyecto).Any();
            if (yaVotó)
                throw new ServiceException("Ya has votado en este proyecto para esta votación");

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

        public int CrearVotacion(DateTime end, bool status, string titulo, string descripcion)
        {
            if (rol is EncargadoVotacion)
            {
                EncargadoVotacion encargado = rol as EncargadoVotacion;
                Votacion votacion = new Votacion(DateTime.Now, end, status, encargado);
                votacion.Titulo = titulo ?? string.Empty;
                votacion.Descripcion = descripcion ?? string.Empty;
                dal.Insert<Votacion>(votacion);
                dal.Commit();
                encargado.votaciones.Add(votacion);
                dal.Insert<EncargadoVotacion>(encargado);
                // After commit the votacion should have its Id generated by the DAL/EF
                return votacion.Id;
            }
            else
            {
                throw new ServiceException("No es Encargado de votación");
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

        public bool HasVotadoEnEvento(int idEvento)
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");

            return dal.GetWhere<Voto>(v =>
                v.votante.usuario.Id == usuario.Id &&
                v.votacion.evento.IdEvento == idEvento
            ).Any();
        }

        // Usamos el método fábrica
        public void AsignarRolEnEvento(string tipoRol, int idEvento)
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");

            Evento evento = dal.GetById<Evento>(idEvento);
            if (evento == null)
                throw new ServiceException("El evento no existe");

            Rol nuevoRol = RolFactory.Create(tipoRol, DateTime.Now, 0);
            nuevoRol.evento = evento;
            usuario.roles ??= new List<Rol>();
            usuario.roles.Add(nuevoRol);
            dal.Insert<Rol>(nuevoRol);
            dal.Commit();
            rol = nuevoRol;
        }

        public void AsignarRolSinEvento(string tipoRol)
        {
            if (usuario == null)
                throw new ServiceException("No hay ningún usuario logueado");
            Rol nuevoRol = RolFactory.Create(tipoRol, DateTime.Now, 0);
            nuevoRol.evento = null;
            usuario.roles ??= new List<Rol>();
            usuario.roles.Add(nuevoRol);
            dal.Insert<Rol>(nuevoRol);
            dal.Commit();
            rol = nuevoRol;
        }
    }
}