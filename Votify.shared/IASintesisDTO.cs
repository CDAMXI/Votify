using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Votify.shared
{
    public interface IASintesisDTO
    {
       public int Id { get; set; }
        public string Propuesta { get; set; }
        public DateOnly FechaSintesis { get; set; }
        public string Description { get; set; }
       
    }
}
