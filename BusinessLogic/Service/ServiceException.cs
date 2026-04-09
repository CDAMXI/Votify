using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.BusinessLogic.Service
{
    public class ServiceException : Exception
    {
        public ServiceException() { }
        public ServiceException(string message) : base(message) { }
        public ServiceException(String message, Exception inner) : base(message, inner) { }
    }
}
