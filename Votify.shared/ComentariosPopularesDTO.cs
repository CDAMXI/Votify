namespace Votify.shared
{
    public class ComentariosPopularesDTO
    {
        public List<string> Categorias { get; set; } = new();
        public List<ComentarioPopularDTO> Comentarios { get; set; } = new();
    }
}
