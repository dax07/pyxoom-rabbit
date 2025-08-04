using Microsoft.Extensions.Configuration;
using Pyxoom_Rabbit;
using Pyxoom_Rabbit.Database;
using Pyxoom_Rabbit.Dtos;
using Pyxoom_Rabbit.Services;
using Serilog;
using System.Buffers.Text;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PSW.Pyxoom.Analytix.Queue
{
    internal class Program
    {
        private static HttpClient _httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var logFile = config["serilog:write-to:File.path"]!;
            var templateFile = config["serilog:write-to:File.outputTemplate"]!;

            //var logger = new LoggerConfiguration()
            //                .Enrich.FromLogContext()
            //                .MinimumLevel.Information()
            //                .WriteTo.Console(outputTemplate: templateFile)
            //                .WriteTo.File(logFile, rollingInterval: RollingInterval.Day)
            //                .CreateLogger();
            ////.ReadFrom.AppSettings().CreateLogger();
            //logger.Information("Starting Analytix Worker Services");
            //Log.Logger = logger;
            var rabbitHelper = new RabbitMQHelper(config);

            RabbitMQHelper.ProcessResult ConsumeFunction(string body, string messageId = "")
            {
                var dbManager = new DbManager(config);
                var pr = new RabbitMQHelper.ProcessResult { };
                Log.Logger.Information($"Mensaje recibido: {messageId}");
                var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                var messageData = JsonSerializer.Deserialize<MessageDto>(body);

                switch (messageData.Type?.ToLower())
                {
                    case "pyxoom":
                        HandlePyxoomActions(messageData, dbManager, config);
                        break;

                    case "interactive":
                        HandleInteractiveActions(messageData, dbManager, config);
                        break;

                    case "chatbot":
                        HandleChatBotActions(messageData, body, dbManager, config);
                        break;

                    default:
                        Log.Logger.Warning($"Tipo de mensaje no reconocido: {messageData.Type}");
                        break;
                }

                var method = jsonData["method"].ToString();
                return pr;
            }

            rabbitHelper.Consume(ConsumeFunction, (string body, string messageId, string _queueName, string errorMessage) =>
            {
                //var service = new ErrorService();
                //service.InsertError(new EF.ErrorQueue
                //{
                //    Error = errorMessage,
                //    FechaHora = DateTime.Now,
                //    Mensaje = body,
                //    MessageId = messageId,
                //    TipoError = ErrorService.ERROR_CONSUME_MESSAGE,
                //    Queue = _queueName
                //});
            });

            Environment.Exit(0);

            var dbManager = new DbManager(config);

            // Ejecutar SQL
            dbManager.SqlService.EjecutarConsulta();
        }

        private static void HandlePyxoomActions(MessageDto messageData, DbManager dbManager, IConfiguration config)
        {
            switch (messageData.Actions?.ToLower())
            {
                case "crear_vacante":
                    List<PreguntaDto> preguntas = dbManager.PyxoomService.ObtenerPreguntasVacante((int)messageData.VacancyId);
                    string nombre_empresa = dbManager.PyxoomService.ObtenerNombreEmpresa((int)messageData.CompanyId);
                    string avisoPrivacida = dbManager.PyxoomService.ObtenerAvisoDePrivacidad((int)messageData.CompanyId);
                    CallPreguntasApiAsync(preguntas, (int)messageData.VacancyId, avisoPrivacida, nombre_empresa, config).Wait();
                    break;

                case "crear_candidato":
                    PersonaInfoDto persona = dbManager.PyxoomService.ObtenerInfoPersona((int)messageData.PersonId);
                    string nombreEmpresa = dbManager.PyxoomService.ObtenerNombreEmpresa((int)messageData.CompanyId);
                    bool checkProfesional = dbManager.PyxoomService.TieneCheckProfesional((int)messageData.PersonProcessId);
                    ConfiguracionKitDto configuracionKit = dbManager.PyxoomService.ObtenerConfiguracionKit((int)messageData.VacancyId);
                    CallCandidatosApiAsync(persona, configuracionKit, (int)messageData.VacancyId, (int)messageData.PersonProcessId, nombreEmpresa, checkProfesional, config).Wait();
                    break;

                case "finalizacion_vacante":
                    CallFinalizacionApiAsync((int)messageData.VacancyId, (int)messageData.CompanyId, config).Wait();
                    break;

                default:
                    Log.Logger.Warning($"Acción Pyxoom no reconocida: {messageData.Actions}");
                    break;
            }
        }

        private static void HandleInteractiveActions(MessageDto messageData, DbManager dbManager, IConfiguration config)
        {
            switch (messageData.Actions?.ToLower())
            {
                case "curriculumn_subido":
                    Log.Logger.Information($"Procesando acción Interactive: Curriculumn subido - Pendiente de implementación");
                    // TODO: Implementar lógica para curriculumn subido
                    break;

                default:
                    Log.Logger.Warning($"Acción Interactive no reconocida: {messageData.Actions}");
                    break;
            }
        }

        private static void HandleChatBotActions(MessageDto messageData, string body, DbManager dbManager, IConfiguration config)
        {
            Log.Logger.Information($"Procesando tipo ChatBot - Actions: {messageData.Actions ?? "Sin acción definida"}");

            switch (messageData.Actions?.ToLower())
            {
                case "pregunta_validacion_respondidas":
                    try
                    {
                        var respuestasValidacion = JsonSerializer.Deserialize<List<RespuestasRegistroDto>>(body);

                        if (respuestasValidacion != null && respuestasValidacion.Any() && messageData.PersonProcessId.HasValue)
                        {
                            dbManager.PyxoomService.InsertarRespuestasRegistro(respuestasValidacion, (int)messageData.PersonProcessId);
                            Log.Logger.Information($"Respuestas de validación insertadas correctamente para PersonProcessId: {messageData.PersonProcessId}");
                        }
                        else
                        {
                            Log.Logger.Warning("No se pudieron procesar las respuestas de validación: datos faltantes o inválidos");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error al procesar respuestas de validación");
                    }
                    break;

                case "RespuestasDePreguntasFaltantes":
                    try
                    {
                        var mensaje = JsonSerializer.Deserialize<MensajeRespuestasDto>(body);
                        if (mensaje?.respuestas != null && mensaje.respuestas.Any() && messageData.PersonId.HasValue)
                        {
                            dbManager.PyxoomService.ActualizarDatosFaltantesPersona(mensaje.respuestas, (int)messageData.PersonId);
                        }
                        else
                        {
                            Log.Logger.Warning("No se pudieron procesar las respuestas faltantes: datos faltantes o inválidos");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error al procesar respuestas faltantes");
                    }
                    break;
                case "cambiar_estatus_procesoactivo_false":
                    
                    break;

                case "enviar_correo_con_accesos":
                    try
                    {
                        var emailService = new EmailService(config.GetConnectionString("Pyxoom42"), config); // <- Agregar config aquí

                        if (messageData.PersonProcessId.HasValue && messageData.CompanyId.HasValue)
                        {
                            var resultado = emailService.EnviarEmailInteractiveShortUrl(
                                messageData.PersonProcessId.ToString(),
                                (int)messageData.CompanyId
                            );

                            if (resultado.IsSuccess)
                            {
                                Log.Logger.Information($"Email de accesos enviado exitosamente: {resultado.Message}");
                            }
                            else
                            {
                                Log.Logger.Error($"Error enviando email de accesos: {resultado.Message}");
                            }
                        }
                        else
                        {
                            Log.Logger.Warning("Faltan datos requeridos para enviar email de accesos");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error al procesar envío de correo con accesos");
                    }
                    break;

                case "crear_persona":
                    try
                    {
                        if (messageData.VacancyId.HasValue && !string.IsNullOrEmpty(messageData.TelefonoMovil))
                        {

                            int idPersonaProceso = dbManager.PyxoomService.CrearPersonaYProceso(
                                messageData.TelefonoMovil,
                                (int)messageData.VacancyId
                            );

                            var personaCreada = dbManager.PyxoomService.ObtenerPersonaDePersonaProceso(idPersonaProceso);

                            if (personaCreada != null)
                            {

                                PersonaInfoDto persona = dbManager.PyxoomService.ObtenerInfoPersona(personaCreada.PersonId);
                                string nombreEmpresa = dbManager.PyxoomService.ObtenerNombreEmpresa((int)messageData.CompanyId);
                                bool checkProfesional = dbManager.PyxoomService.TieneCheckProfesional(idPersonaProceso);
                                ConfiguracionKitDto configuracionKit = dbManager.PyxoomService.ObtenerConfiguracionKit((int)messageData.VacancyId);

                                CallCandidatosApiAsync(persona, configuracionKit, (int)messageData.VacancyId, idPersonaProceso, nombreEmpresa, checkProfesional, config).Wait();

                            }
                        }
                        else
                        {
                            Log.Logger.Warning("Faltan datos requeridos para crear persona: VacancyId, CompanyId y TelefonoMovil son obligatorios");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error al procesar creación de persona");
                    }
                    break;
                default:
                    Log.Logger.Information($"ChatBot - Acción pendiente de implementación: {messageData.Actions}");
                    break;
            }
        }

        private static string LimpiarHtml(string htmlText)
        {
            if (string.IsNullOrEmpty(htmlText))
                return string.Empty;

            string textoSinEtiquetas = Regex.Replace(htmlText, "<.*?>", string.Empty);
            string textoDecodificado = HttpUtility.HtmlDecode(textoSinEtiquetas);
            string textoLimpio = Regex.Replace(textoDecodificado, @"\s+", " ");
            return textoLimpio.Trim();
        }

        private static async Task CallCandidatosApiAsync(PersonaInfoDto personaInfo, ConfiguracionKitDto configuracionKit, int vacancyId,int personaProcesoId, string npmbreEmpresa,bool check, IConfiguration config)
        {
            try
            {
                var nodeAppUrl = config["ExternalServices:NodeAppUrl"];
                var apiKey = config["ExternalServices:ApiKey"];
                var pyxoomInteractiveUrl = config["ExternalServices:PyxoomInteractiveUrl"];
                var secretKey = config["ExternalServices:SecretKeyEncryption"];
                var timezoneOffsetHours = 0.0;
                if (double.TryParse(config["ExternalServices:TimezoneOffsetHours"], out var offsetValue))
                {
                    timezoneOffsetHours = offsetValue;
                }

                var adjustedDateTime = DateTime.Now.AddHours(timezoneOffsetHours);
                var timestamp = adjustedDateTime.ToString("ddMMyyyyHHmmss");
                var dataToEncrypt = $"{personaProcesoId}_{timestamp}";
                var encryptedData = EncryptText(dataToEncrypt, secretKey);
                var urlPyxoom = $"{pyxoomInteractiveUrl}?token={encryptedData}";

                int clientId = int.Parse(config["ExternalServices:ClientId"]);
                var preguntasFaltantes = CrearPreguntasFaltantes(personaInfo);

                var candidatosEndpoint = $"{nodeAppUrl}api/candidatos";
                var candidatoData = new
                {
                    token = "ABC123",
                    id_vacante = vacancyId,
                    id_cliente = clientId,
                    id_person_process = personaProcesoId,
                    telefono = personaInfo.TelefonoMovil,
                    nombre = personaInfo.PrimerNombre,
                    correo = personaInfo.CorreoElectronico,
                    procesoActivo = true,
                    recordatoriosActivos = true,
                    solicitarCV = configuracionKit.AltaCv,
                    filtradoInteligente = configuracionKit.FiltradoInteligente,
                    nombreEmpresa = npmbreEmpresa,
                    urlPyxoom = urlPyxoom,
                    urlCurriculumKey = urlPyxoom,
                    urlCustom = urlPyxoom,
                    chekProfesional = check,
                    preguntasFaltantes = preguntasFaltantes
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

        private static async Task CallPreguntasApiAsync(List<PreguntaDto> preguntas, int vacanteId,string avisoPrivacidad,string nombre_empresa, IConfiguration config)
        {
            try
            {
                var nodeAppUrl = config["ExternalServices:NodeAppUrl"];
                var apiKey = config["ExternalServices:ApiKey"];

                var preguntasEndpoint = $"{nodeAppUrl}api/preguntas";
                var preguntasData = CrearDatosPreguntasApi(preguntas, vacanteId, avisoPrivacidad, nombre_empresa, config);

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
        private static async Task CallFinalizacionApiAsync(int vacancyId, int clientId, IConfiguration config)
        {
            try
            {
                var nodeAppUrl = config["ExternalServices:NodeAppUrl"];
                var apiKey = config["ExternalServices:ApiKey"];

                var finalizacionEndpoint = $"{nodeAppUrl}api/preguntas";
                var finalizacionData = new
                {
                    id_cliente = clientId.ToString(),
                    id_vacante = vacancyId.ToString()
                };

                var jsonFinalizacion = JsonSerializer.Serialize(finalizacionData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var contentFinalizacion = new StringContent(jsonFinalizacion, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                }

                var finalizacionResponse = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, finalizacionEndpoint)
                {
                    Content = contentFinalizacion
                });

                if (finalizacionResponse.IsSuccessStatusCode)
                {
                    var finalizacionResponseContent = await finalizacionResponse.Content.ReadAsStringAsync();
                    Log.Logger.Information($"API finalizacion respondió exitosamente: {finalizacionResponseContent}");
                }
                else
                {
                    var errorContent = await finalizacionResponse.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error al llamar a la API de finalizacion");
            }
        }

        private static object CrearDatosPreguntasApi(List<PreguntaDto> preguntas, int vacanteId,string avisoPrivacidad,string nombre_empresa, IConfiguration config)
        {
            int clientId = int.Parse(config["ExternalServices:ClientId"]);
            var avisoPrivacidadLimpio = LimpiarHtml(avisoPrivacidad);

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
                id_cliente = clientId,
                id_vacante = vacanteId,
                estado = "activa",
                creadoEn = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"),
                creadoPor = "userVacante",
                preguntasValidacion = preguntasValidacion,
                avisoPrivacidad = avisoPrivacidadLimpio,
                nombreEmpresa = nombre_empresa
            };
        }

        private static List<object> CrearPreguntasFaltantes(PersonaInfoDto personaInfo)
{
    var preguntasFaltantes = new List<object>();
    int preguntaId = 1000; // ID base para preguntas faltantes

    // Pregunta 1000: Nombre completo (si falta nombre completo)
    if (personaInfo.FaltaPrimerNombre || personaInfo.FaltaSegundoNombre || personaInfo.FaltaApellidoPaterno || personaInfo.FaltaApellidoMaterno)
    {
        preguntasFaltantes.Add(new
        {
            id = preguntaId, // 1000
            tipo = "abierta",
            pregunta = "¿Cuál es tu nombre completo?",
        });
    }
    preguntaId++; // Siempre incrementar para mantener consistencia

    // Pregunta 1001: Género (si falta sexo)
    if (personaInfo.FaltaSexo)
    {
        preguntasFaltantes.Add(new
        {
            id = preguntaId, // 1001
            tipo = "opcion_multiple",
            pregunta = "¿Cuál es tu género?",
            opciones = new[]
            {
                new { id = 1, text = "Masculino" },
                new { id = 2, text = "Femenino" }
            }
        });
    }
    preguntaId++; // Siempre incrementar

    // Pregunta 1002: Fecha de nacimiento
    if (personaInfo.FaltaFechaNacimiento)
    {
        preguntasFaltantes.Add(new
        {
            id = preguntaId, // 1002
            tipo = "abierta",
            pregunta = "Ingresa tu fecha de nacimiento con el siguiente formato: DD/MM/AAAA",
        });
    }
    preguntaId++; // Siempre incrementar

    // Pregunta 1003: Escolaridad (si falta escolaridad)
    if (personaInfo.FaltaEscolaridad)
    {
        preguntasFaltantes.Add(new
        {
            id = preguntaId, // 1003
            tipo = "opcion_multiple",
            pregunta = "Selecciona tu Último Grado de Estudios",
            opciones = new[]
            {
                new { id = 1, text = "Primaria Inconclusa" },
                new { id = 2, text = "Primaria" },
                new { id = 3, text = "Secundaria" },
                new { id = 4, text = "Preparatoria" },
                new { id = 5, text = "Técnico" },
                new { id = 6, text = "Técnico Superior" },
                new { id = 7, text = "Profesional" },
                new { id = 8, text = "Maestría" },
                new { id = 9, text = "Doctorado" }
            }
        });
    }
    preguntaId++; // Siempre incrementar

    // Pregunta 1004: Estado civil (si falta estado civil)
    if (personaInfo.FaltaEstadoCivil)
    {
        preguntasFaltantes.Add(new
        {
            id = preguntaId, // 1004
            tipo = "opcion_multiple",
            pregunta = "Tu Estado Civil es:",
            opciones = new[]
            {
                new { id = 1, text = "Soltero(a)" },
                new { id = 2, text = "Casado(a)" },
                new { id = 3, text = "Viudo(a)" },
                new { id = 4, text = "Divorciado(a)" },
                new { id = 5, text = "Otro" }
            }
        });
    }

    return preguntasFaltantes;
}


        private static string EncryptText(string plainText, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.GenerateIV();

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // Agregar IV al inicio del stream
                    msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    string base64 =  Convert.ToBase64String(msEncrypt.ToArray());
                    string urlSafeBase64 = base64.Replace('+', '-').Replace('/', '_').Replace("=", "");

                    return urlSafeBase64;

                }
            }
        }
    }
}