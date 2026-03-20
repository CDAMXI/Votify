using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Usuario
    {
        public Usuario() { }
        public Usuario(string username, string email, string password, int id)
        {
            Username = username;
            Email = email;
            Password = password;
            Id = id;
        }
    }
}
