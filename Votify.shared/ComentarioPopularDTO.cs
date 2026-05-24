namespace Votify.shared
{
    public class ComentarioPopularDTO
    {
        public int Id { get; set; }
        public string Autor { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string TipoRol { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
    }
}
