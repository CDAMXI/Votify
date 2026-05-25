using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Votify.BusinessLogic.Service
{
    public sealed class GeminiComentarioPopularClassifier : IComentarioPopularClassifier
    {
        private const string DefaultModel = "gemini-2.5-flash-lite";
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiComentarioPopularClassifier(HttpClient httpClient, string apiKey, string? model = null)
        {
            _httpClient = httpClient;
            _apiKey = apiKey?.Trim() ?? string.Empty;
            _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        }

        public async Task<ResultadoClasificacionComentariosPopulares> ClasificarAsync(
            IReadOnlyList<ComentarioPopularEntrada> comentarios,
            CancellationToken cancellationToken = default)
        {
            if (comentarios.Count == 0)
                return new ResultadoClasificacionComentariosPopulares();

            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new ServiceException("API key de Gemini no configurada");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var prompt = ConstruirPrompt(comentarios);
            var body = new
            {
                system_instruction = new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = "Eres un clasificador de comentarios populares de Votify. Devuelve solo JSON valido, sin markdown."
                        }
                    }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = prompt } }
                    }
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(url, body, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ServiceException("No se pudo clasificar comentarios con Gemini");

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var text = json.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            return ParsearClasificacion(text);
        }

        private static string ConstruirPrompt(IReadOnlyList<ComentarioPopularEntrada> comentarios)
        {
            var comentariosJson = JsonSerializer.Serialize(comentarios.Select(c => new
            {
                id = c.Id,
                texto = c.Texto
            }));

            return
                "Agrupa estos comentarios populares de un proyecto en categorias breves y utiles para filtrar en una interfaz. " +
                "Usa entre 1 y 6 categorias. Prioriza categorias como Positivo, Mejora, Tecnico, Diseno, Impacto o Presentacion cuando encajen, " +
                "pero puedes crear otras si describen mejor los comentarios. " +
                "Cada comentario debe aparecer exactamente una vez. " +
                "Devuelve exclusivamente JSON con esta forma: " +
                "{\"categorias\":[{\"nombre\":\"Positivo\"}],\"comentarios\":[{\"idComentario\":1,\"categoria\":\"Positivo\"}]}." +
                "\nComentarios:\n" + comentariosJson;
        }

        private static ResultadoClasificacionComentariosPopulares ParsearClasificacion(string text)
        {
            var jsonText = ExtraerJson(text);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var resultado = JsonSerializer.Deserialize<ResultadoClasificacionComentariosPopulares>(jsonText, options);
            return resultado ?? new ResultadoClasificacionComentariosPopulares();
        }

        private static string ExtraerJson(string text)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.StartsWith("```", StringComparison.Ordinal))
            {
                int firstNewLine = value.IndexOf('\n');
                int lastFence = value.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewLine >= 0 && lastFence > firstNewLine)
                    value = value[(firstNewLine + 1)..lastFence].Trim();
            }

            int firstBrace = value.IndexOf('{');
            int lastBrace = value.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace >= firstBrace)
                value = value[firstBrace..(lastBrace + 1)];

            return value;
        }
    }
}
