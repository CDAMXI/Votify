using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Categoria
    {
        public Categoria()
        {
        }

        public Categoria(string username, string email, string password, int id) : this()
        {
            Username = username;
            Email = email;
            Password = password;
            Id = id;
        }
    }
}
