using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.BuisnessLogic.Service
{
    internal interface IVotifyService
    {
        void LogIn(String user, String password);
        void LogOut();
    }
}
