using Microsoft.Extensions.Configuration;
using Pyxoom_Rabbit.Dtos;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pyxoom_Rabbit.Services
{
    public class ExternalApiService
    {
        //private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;
        private static HttpClient _httpClient = new HttpClient();

        public ExternalApiService(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _configuration = configuration;
            _baseUrl = _configuration["ExternalServices:NodeAppUrl"];
        }

        private static async Task CallCandidatosApiAsync(PersonaInfoDto personaInfo, int vacancyId, int clientId, int personaProcesoId, IConfiguration config)
        {
            try
            {
                var nodeAppUrl = config["ExternalServices:NodeAppUrl"];
                var apiKey = config["ExternalServices:ApiKey"];

                var candidatosEndpoint = $"{nodeAppUrl}api/candidatos";
                var candidatoData = new
                {
                    token = "ABC123",
                    id_lista = "",
                    id_vacante = vacancyId,
                    id_cliente = clientId,
                    id_person_process = personaProcesoId,
                    telefono = personaInfo.TelefonoMovil,
                    nombre = personaInfo.PrimerNombre,
                    correo = personaInfo.CorreoElectronico,
                    procesoActivo = true,
                    recordatoriosActivos = true,
                    solicitarCV = true,
                    urlPyxoom = ""
                };

                var jsonCandidatos = JsonSerializer.Serialize(candidatoData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                Log.Logger.Information($"JSON enviado a /api/candidatos: {jsonCandidatos}");

                var contentCandidatos = new StringContent(jsonCandidatos, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                }

                var candidatosResponse = await _httpClient.PostAsync(candidatosEndpoint, contentCandidatos);

                if (candidatosResponse.IsSuccessStatusCode)
                {
                    var candidatosResponseContent = await candidatosResponse.Content.ReadAsStringAsync();
                    Log.Logger.Information($"API candidatos respondió exitosamente: {candidatosResponseContent}");
                }
                else
                {
                    var errorContent = await candidatosResponse.Content.ReadAsStringAsync();
                    Log.Logger.Error($"Error en API candidatos: {candidatosResponse.StatusCode} - {candidatosResponse.ReasonPhrase}");
                    Log.Logger.Error($"Contenido del error: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error al llamar a la API de candidatos");
            }
        }

        private static async Task CallPreguntasApiAsync(List<PreguntaDto> preguntas, int vacanteId, IConfiguration config)
        {
            try
            {
                var nodeAppUrl = config["ExternalServices:NodeAppUrl"];
                var apiKey = config["ExternalServices:ApiKey"];

                var preguntasEndpoint = $"{nodeAppUrl}api/preguntas";
                var preguntasData = CrearDatosPreguntasApi(preguntas, vacanteId);

                var jsonPreguntas = JsonSerializer.Serialize(preguntasData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                Log.Logger.Information($"JSON enviado a /api/preguntas: {jsonPreguntas}");

                var contentPreguntas = new StringContent(jsonPreguntas, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                }

                var preguntasResponse = await _httpClient.PostAsync(preguntasEndpoint, contentPreguntas);

                if (preguntasResponse.IsSuccessStatusCode)
                {
                    var preguntasResponseContent = await preguntasResponse.Content.ReadAsStringAsync();
                    Log.Logger.Information($"API preguntas respondió exitosamente: {preguntasResponseContent}");
                }
                else
                {
                    var errorContent = await preguntasResponse.Content.ReadAsStringAsync();
                    Log.Logger.Error($"Error en API preguntas: {preguntasResponse.StatusCode} - {preguntasResponse.ReasonPhrase}");
                    Log.Logger.Error($"Contenido del error: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error al llamar a la API de preguntas");
            }
        }

        private static object CrearDatosPreguntasApi(List<PreguntaDto> preguntas, int vacanteId)
        {
            // Preguntas faltantes dummy (hardcodeadas)
            var preguntasFaltantes = new List<object>
    {
        new
        {
            id = 1,
            tipo = "abierta",
            pregunta = "¿Cuál ha sido tu mayor logro profesional hasta ahora?"
        },
        new
        {
            id = 2,
            tipo = "opcion_multiple",
            pregunta = "¿Con qué nivel de experiencia te sientes más cómodo trabajando?",
            opciones = new[]
            {
                new { id = 1, text = "Trabajo individual" },
                new { id = 2, text = "Trabajo en equipo" },
                new { id = 3, text = "Ambos por igual" }
            },
            descarte = true,
            respuestaCorrectaId = 1,
            contentSid = (string)null,
            templateCreado = false,
            estado = "pendiente"
        }
    };

            // Preguntas de validación de la lista recibida
            var preguntasValidacion = preguntas
                .Select(p => new
                {
                    id = p.IdPregunta,
                    tipo = p.Tipo?.ToLower() ?? "opcion_multiple",
                    pregunta = p.TextoPregunta,
                    opciones = p.Respuestas?.Select(r => new
                    {
                        id = r.IdRespuesta,
                        text = r.TextoRespuesta
                    }).ToList(),
                    descarte = p.EsDescarte,
                    respuestaCorrectaId = p.EsDescarte ?
                        p.Respuestas?.FirstOrDefault(r => r.RespuestaEsClave)?.IdRespuesta :
                        (int?)null,
                    contentSid = (string)null,
                    templateCreado = false,
                    estado = "pendiente"
                })
                .ToList();

            return new
            {
                id_lista = "lp_001",
                id_cliente = 1,
                id_vacante = vacanteId,
                estado = "activa",
                creadoEn = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"),
                creadoPor = "userVacante",
                preguntasValidacion = preguntasValidacion,
                preguntasFaltantes = preguntasFaltantes
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
