using System.Collections.Generic;

namespace Votify.shared
{
    public class CategoriaBaremoDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public List<CriterioDTO> Criterios { get; set; } = new();
    }
}
