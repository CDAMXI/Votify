using Votify.Persistence;
using System;
using System.Collections.Generic;
using System.Text;
using Votify.BusinessLogic;
using Votify.Entities;
using System.CodeDom;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection.Emit;
//using System.Runtime.Remoting.Contexts;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using ManteHos.Persistence;


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

        public void Commit()
        {
            dal.Commit();
        }
    }
}
