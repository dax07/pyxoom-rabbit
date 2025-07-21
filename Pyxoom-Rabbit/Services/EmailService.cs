using Microsoft.Data.SqlClient;
using Microsoft.Web.Administration;
using Pyxoom_Rabbit.Database;
using Pyxoom_Rabbit.Dtos;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using sib_api_v3_sdk.Api;
using sib_api_v3_sdk.Client;
using sib_api_v3_sdk.Model;

namespace Pyxoom_Rabbit.Services
{
    public class EmailService
    {
        private readonly string _connectionString;

        public EmailService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public EmailConfigurationDto ObtenerConfiguracionBrevo()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"SELECT 
                host, puerto, ssl, usuario, 
                password, sender
                FROM PyxoomUser.Configuracion_Correo 
                WHERE ssl = 1", conn)
            {
                CommandType = CommandType.Text
            };

            try
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();

                if (reader.HasRows && reader.Read())
                {
                    var config = new EmailConfigurationDto
                    {
                        Host = DbUtils.GetNullableString(reader, "host"),
                        Port = Convert.ToInt32(reader["puerto"]),
                        UserName = DbUtils.GetNullableString(reader, "usuario"),
                        Password = DbUtils.GetNullableString(reader, "password"),
                        
                        UseSSL = Convert.ToBoolean(reader["ssl"]),
                        Sender = DbUtils.GetNullableString(reader, "sender")
                    };

                    // Desencriptar valores sensibles
                    if (!string.IsNullOrEmpty(config.Host))
                        config.Host = Encryption.Decrypt(config.Host);

                    if (!string.IsNullOrEmpty(config.UserName))
                        config.UserName = Encryption.Decrypt(config.UserName);

                    if (!string.IsNullOrEmpty(config.Password))
                        config.Password = Encryption.Decrypt(config.Password);

                    if (!string.IsNullOrEmpty(config.Sender))
                        config.Sender = Encryption.Decrypt(config.Sender);

                    return config;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener configuración de Brevo: " + ex.Message);
                throw;
            }
        }

        public List<PersonInteractiveDto> ObtenerAccesoPersonaInteractive(string[] personProcessIds, int companyId, int languageId)
        {
            using var conn = new SqlConnection(_connectionString);

            // Crear la lista de parámetros para el IN
            var parameters = string.Join(",", personProcessIds.Select((id, index) => $"@personId{index}"));

            using var cmd = new SqlCommand($@"SELECT 
                pp.login,
                pp.password,
                CONCAT(p.primer_nombre, ' ', p.apellido_paterno) as NameCandidateEmail,
                v.nombre as puesto,
                r.nombre as responsable,
                p.correo_electronico,
                pp.dias_expiracion,
                tm.texto as mainMessage,
                CASE WHEN pp.activo = 1 THEN 1 ELSE 0 END as hasInteractive,
                CASE WHEN @languageId = 2 THEN 1 ELSE 0 END as isEnglish
                FROM PyxoomUser.PersonaProceso pp
                INNER JOIN PyxoomUser.Persona p ON pp.id_persona = p.id_persona
                INNER JOIN PyxoomUser.Vacante v ON pp.id_vacante = v.id_vacante
                LEFT JOIN PyxoomUser.Responsable r ON v.id_responsable = r.id_responsable
                LEFT JOIN PyxoomUser.TextoEmail tm ON tm.id_empresa = @companyId AND tm.tipo_email = 'candidato_nuevo'
                WHERE pp.id_persona_proceso IN ({parameters})
                AND v.id_empresa = @companyId", conn)
            {
                CommandType = CommandType.Text
            };

            // Agregar parámetros dinámicamente
            for (int i = 0; i < personProcessIds.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@personId{i}", personProcessIds[i]);
            }
            cmd.Parameters.AddWithValue("@companyId", companyId);
            cmd.Parameters.AddWithValue("@languageId", languageId);

            var resultados = new List<PersonInteractiveDto>();

            try
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var persona = new PersonInteractiveDto
                    {
                        login = DbUtils.GetNullableString(reader, "login"),
                        password = DbUtils.GetNullableString(reader, "password"),
                        NameCandidateEmail = DbUtils.GetNullableString(reader, "NameCandidateEmail"),
                        puesto = DbUtils.GetNullableString(reader, "puesto"),
                        responsable = DbUtils.GetNullableString(reader, "responsable") ?? "Administrador",
                        correo_electronico = DbUtils.GetNullableString(reader, "correo_electronico"),
                        dias_expiracion = Convert.ToInt32(reader["dias_expiracion"]),
                        mainMessage = DbUtils.GetNullableString(reader, "mainMessage"),
                        hasInteractive = Convert.ToBoolean(reader["hasInteractive"]),
                        isEnglish = Convert.ToBoolean(reader["isEnglish"])
                    };

                    // Desencriptar datos sensibles
                    if (!string.IsNullOrEmpty(persona.NameCandidateEmail))
                        persona.NameCandidateEmail = Encryption.Decrypt(persona.NameCandidateEmail);

                    resultados.Add(persona);
                }

                return resultados;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener acceso persona interactive: " + ex.Message);
                throw;
            }
        }

        public string ObtenerUbicacionInteractive(int companyId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"SELECT url_interactive 
                FROM PyxoomUser.EmpresaConfiguracion 
                WHERE id_empresa = @companyId", conn)
            {
                CommandType = CommandType.Text
            };

            cmd.Parameters.AddWithValue("@companyId", companyId);

            try
            {
                conn.Open();
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener ubicación interactive: " + ex.Message);
                throw;
            }
        }

        public EmailResultDto EnviarEmailInteractiveShortUrl(string personProcessId, int companyId)
        {
            try
            {
                // Obtener configuración
                var mailConfig = ObtenerConfiguracionBrevo();
                if (mailConfig == null)
                {
                    return new EmailResultDto
                    {
                        IsSuccess = false,
                        Message = "No se pudo obtener la configuración de email"
                    };
                }

                // Obtener ubicación
                string ubicacion = ObtenerUbicacionInteractive(companyId);

                // Obtener datos de personas
                var objData = ObtenerAccesoPersonaInteractive(personProcessId.Split('|'), companyId, 1);

                if (objData.Count == 0)
                {
                    return new EmailResultDto
                    {
                        IsSuccess = false,
                        Message = "No se encontraron datos para enviar"
                    };
                }

                int emailsEnviados = 0;

                foreach (var item in objData)
                {
                    if (item.hasInteractive && !string.IsNullOrEmpty(item.correo_electronico) && !string.IsNullOrEmpty(item.login))
                    {
                        var emailRequest = new EmailRequestDto
                        {
                            To = item.correo_electronico,
                            Subject = item.isEnglish ? "Access Account" : "Acceso a Cuenta",
                            TemplatePath = item.isEnglish
                                ? "Content/MailTemplates/CandidateAccountMailTemplateEnglish.htm"
                                : "Content/MailTemplates/CandidateAccountMailTemplate.htm",
                            TemplateParameters = new Dictionary<string, string>
                            {
                                ["nombre"] = item.NameCandidateEmail,
                                ["usuario"] = item.login,
                                ["contraseña"] = item.password,
                                ["name"] = item.NameCandidateEmail,
                                ["jobposition"] = item.puesto ?? "",
                                ["administrador"] = item.responsable,
                                ["liga"] = ubicacion + "?lang=" + (item.isEnglish ? "en" : "es"),
                                ["dias"] = item.dias_expiracion.ToString(),
                                ["cid:uniqueId"] = ubicacion.Replace("/Home/Login", "") + "/Content/images/pyxoom-psw.png"
                            }
                        };

                        // Procesar mensaje principal
                        string mainMessage = item.mainMessage ?? "";
                        foreach (var param in emailRequest.TemplateParameters)
                        {
                            if (param.Key != "cid:uniqueId")
                            {
                                mainMessage = mainMessage.Replace("@" + param.Key, param.Value);
                            }
                        }
                        emailRequest.TemplateParameters["mainMessage"] = mainMessage;

                        var resultado = EnviarEmailPorBrevo(emailRequest, mailConfig);
                        if (resultado.IsSuccess)
                        {
                            emailsEnviados++;
                        }
                    }
                }

                return new EmailResultDto
                {
                    IsSuccess = emailsEnviados > 0,
                    Message = $"Se enviaron {emailsEnviados} emails exitosamente"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en EnviarEmailInteractiveShortUrl: " + ex.Message);
                return new EmailResultDto
                {
                    IsSuccess = false,
                    Message = "Error interno del servidor",
                    ErrorDetails = ex.Message
                };
            }
        }

        public EmailResultDto EnviarEmailPorBrevo(EmailRequestDto emailRequest, EmailConfigurationDto config)
        {
            try
            {
                // Configurar API Key
                if (!sib_api_v3_sdk.Client.Configuration.Default.ApiKey.ContainsKey("api-key"))
                {
                    sib_api_v3_sdk.Client.Configuration.Default.AddApiKey("api-key", config.ApiKey);
                }

                // Procesar contenido del template
                string htmlContent = ProcesarTemplate(emailRequest);

                // Configurar email
                var sender = new SendSmtpEmailSender("PSW GLOBAL", config.Sender);
                var recipients = ObtenerDestinatarios(emailRequest.To);

                var email = new SendSmtpEmail
                {
                    Sender = sender,
                    To = recipients,
                    HtmlContent = htmlContent,
                    Subject = emailRequest.Subject
                };

                // Agregar adjunto si existe
                if (emailRequest.Attachment != null)
                {
                    email.Attachment = new List<SendSmtpEmailAttachment>
                    {
                        new SendSmtpEmailAttachment
                        {
                            Content = emailRequest.Attachment,
                            Name = emailRequest.AttachmentName ?? "attachment.ics"
                        }
                    };
                }

                // Enviar email
                var apiInstance = new TransactionalEmailsApi();
                var result = apiInstance.SendTransacEmail(email);

                Console.WriteLine($"Email enviado exitosamente a {emailRequest.To}");

                return new EmailResultDto
                {
                    IsSuccess = true,
                    Message = "Email enviado exitosamente"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando email a {emailRequest.To}: {ex.Message}");

                return new EmailResultDto
                {
                    IsSuccess = false,
                    Message = "Error enviando email",
                    ErrorDetails = ex.Message
                };
            }
        }

        private string ProcesarTemplate(EmailRequestDto emailRequest)
        {
            string content = string.Empty;

            // Cargar template si existe
            if (!string.IsNullOrEmpty(emailRequest.TemplatePath))
            {
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), emailRequest.TemplatePath);
                if (File.Exists(templatePath))
                {
                    content = File.ReadAllText(templatePath);
                }
            }
            else if (!string.IsNullOrEmpty(emailRequest.HtmlContent))
            {
                content = emailRequest.HtmlContent;
            }

            // Procesar parámetros de reemplazo
            if (emailRequest.TemplateParameters != null)
            {
                foreach (var param in emailRequest.TemplateParameters)
                {
                    if (param.Key == "cid:uniqueId")
                    {
                        content = content.Replace(param.Key, param.Value);
                    }
                    else
                    {
                        content = content.Replace("@" + param.Key, param.Value);
                    }
                }
            }

            return content;
        }

        private List<SendSmtpEmailTo> ObtenerDestinatarios(string emailAddresses)
        {
            var recipients = new List<SendSmtpEmailTo>();

            if (!string.IsNullOrEmpty(emailAddresses))
            {
                var addresses = emailAddresses.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var address in addresses)
                {
                    recipients.Add(new SendSmtpEmailTo(address.Trim()));
                }
            }

            return recipients;
        }

        public EmailResultDto EnviarEmailGenerico(EmailRequestDto emailRequest)
        {
            try
            {
                var config = ObtenerConfiguracionBrevo();
                if (config == null)
                {
                    return new EmailResultDto
                    {
                        IsSuccess = false,
                        Message = "No se pudo obtener la configuración de email"
                    };
                }

                return EnviarEmailPorBrevo(emailRequest, config);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en EnviarEmailGenerico: " + ex.Message);
                return new EmailResultDto
                {
                    IsSuccess = false,
                    Message = "Error interno del servidor",
                    ErrorDetails = ex.Message
                };
            }
        }
    }
}
