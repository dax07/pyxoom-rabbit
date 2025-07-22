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
using Microsoft.Extensions.Configuration;

namespace Pyxoom_Rabbit.Services
{
    public class EmailService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;

        public EmailService(string connectionString, IConfiguration configuration)
        {
            _connectionString = connectionString;
            _configuration = configuration;
        }

        public EmailConfigurationDto ObtenerConfiguracionBrevo()
        {
            try
            {
                var config = new EmailConfigurationDto
                {
                    Host = _configuration["Brevo:BrevoHost"],
                    Port = int.Parse(Encryption.Decrypt(_configuration["Brevo:BrevoPort"])),
                    UserName = _configuration["Brevo:BrevoAcc"],
                    Password = _configuration["Brevo:BrevoSec"],
                    ApiKey = _configuration["Brevo:BrevoKey"], // Esta NO está encriptada
                    UseSSL = bool.Parse(Encryption.Decrypt(_configuration["Brevo:BrevoSsl"])),
                    Sender = _configuration["Brevo:BrevoSender"]
                };

                // Desencriptar valores que SÍ están encriptados
                if (!string.IsNullOrEmpty(config.Host))
                    config.Host = Encryption.Decrypt(config.Host);

                if (!string.IsNullOrEmpty(config.UserName))
                    config.UserName = Encryption.Decrypt(config.UserName);

                if (!string.IsNullOrEmpty(config.Password))
                    config.Password = Encryption.Decrypt(config.Password);

                if (!string.IsNullOrEmpty(config.Sender))
                    config.Sender = Encryption.Decrypt(config.Sender);

                // ApiKey NO se desencripta porque ya está en texto plano

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener configuración de Brevo: " + ex.Message);
                return null;
            }
        }

        public List<PersonInteractiveDto> ObtenerAccesoPersonaInteractive(string[] personProcessIds, int companyId, int languageId)
        {
            using var conn = new SqlConnection(_connectionString);

            try
            {
                conn.Open();

                // Crear DataTable para los IDs (similar a tu lógica original)
                DataTable idsPersonaProceso = new DataTable();
                idsPersonaProceso.Columns.Add("id", typeof(Int64));

                var ppIds = personProcessIds
                    .Where(f => !string.IsNullOrEmpty(f))
                    .Select(f => int.Parse(f))
                    .Distinct()
                    .ToArray();

                foreach (var id in ppIds)
                {
                    idsPersonaProceso.Rows.Add(id);
                }

                // Ejecutar el mismo stored procedure que usas en Unity
                using var cmd = new SqlCommand("Pyxoom.ObtenerInfoMail", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                SqlParameter ids = new SqlParameter("@pPersonaProcesoIds", SqlDbType.Structured)
                {
                    Value = idsPersonaProceso,
                    TypeName = "Pyxoom.IdBig"
                };
                cmd.Parameters.Add(ids);

                var resultados = new List<PersonInteractiveDto>();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var persona = new PersonInteractiveDto
                    {
                        login = !string.IsNullOrEmpty(DbUtils.GetNullableString(reader, "login"))
                            ? Encryption.Decrypt(DbUtils.GetNullableString(reader, "login"))
                            : string.Empty,
                        password = !string.IsNullOrEmpty(DbUtils.GetNullableString(reader, "password"))
                            ? Encryption.Decrypt(DbUtils.GetNullableString(reader, "password"))
                            : string.Empty,
                        NameCandidateEmail = ObtenerNombreCompleto(reader),
                        puesto = DbUtils.GetNullableString(reader, "puesto"),
                        responsable = !string.IsNullOrEmpty(DbUtils.GetNullableString(reader, "responsable"))
                            ? Encryption.DecryptFullName(DbUtils.GetNullableString(reader, "responsable"))
                            : "Administrador",
                        correo_electronico = DbUtils.GetNullableString(reader, "correo_electronico"),
                        dias_expiracion = ObtenerDiasExpiracion(companyId),
                        mainMessage = ObtenerMensajePrincipal(Convert.ToInt32(reader["personProcessId"]), companyId, languageId),
                        hasInteractive = VerificarHasInteractive(Convert.ToInt32(reader["personProcessId"])),
                        isEnglish = languageId == 2
                    };

                    resultados.Add(persona);
                }

                // Actualizar usuarios y procesos (como en tu lógica original)
                ActualizarUsuariosYProcesos(ppIds, companyId);

                return resultados;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener acceso persona interactive: " + ex.Message);
                return new List<PersonInteractiveDto>();
            }
        }

        private string ObtenerNombreCompleto(SqlDataReader reader)
        {
            string firstName = DbUtils.GetNullableString(reader, "FirstName");
            string lastName = DbUtils.GetNullableString(reader, "FirstLastName");

            if (!string.IsNullOrEmpty(firstName))
                firstName = Encryption.Decrypt(firstName);
            if (!string.IsNullOrEmpty(lastName))
                lastName = Encryption.Decrypt(lastName);

            return $"{firstName} {lastName}".Trim();
        }

        private int ObtenerDiasExpiracion(int companyId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"SELECT epe.valor 
                FROM PyxoomUser.Parametros_Empresa pe
                INNER JOIN PyxoomUser.Empresa_Parametros_Empresa epe ON pe.id_parametro = epe.id_parametro
                WHERE epe.id_empresa = @companyId AND pe.nombre_parametro = @paramName", conn)
            {
                CommandType = CommandType.Text
            };

            var daysKey = Encryption.Encrypt("expiracion_interactive");
            cmd.Parameters.AddWithValue("@companyId", companyId);
            cmd.Parameters.AddWithValue("@paramName", daysKey);

            try
            {
                conn.Open();
                var result = cmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(result))
                {
                    var decryptedValue = Encryption.Decrypt(result);
                    return Convert.ToInt32(decryptedValue);
                }
                return 30; // Valor por defecto
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error obteniendo días de expiración: " + ex.Message);
                return 30; // Valor por defecto
            }
        }

        private string ObtenerMensajePrincipal(int personProcessId, int companyId, int languageId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"SELECT HTML_EmailMessage 
                FROM PyxoomUser.TemplateEmail 
                WHERE id_empresa = @companyId AND tipo_email = 'candidato_nuevo' AND id_idioma = @languageId", conn)
            {
                CommandType = CommandType.Text
            };

            cmd.Parameters.AddWithValue("@companyId", companyId);
            cmd.Parameters.AddWithValue("@languageId", languageId);

            try
            {
                conn.Open();
                return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error obteniendo mensaje principal: " + ex.Message);
                return string.Empty;
            }
        }

        private bool VerificarHasInteractive(int personProcessId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"SELECT CASE WHEN activo = 1 THEN 1 ELSE 0 END 
                FROM PyxoomUser.PersonaProceso 
                WHERE id_persona_proceso = @personProcessId", conn)
            {
                CommandType = CommandType.Text
            };

            cmd.Parameters.AddWithValue("@personProcessId", personProcessId);

            try
            {
                conn.Open();
                var result = cmd.ExecuteScalar();
                return Convert.ToBoolean(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error verificando hasInteractive: " + ex.Message);
                return false;
            }
        }

        private void ActualizarUsuariosYProcesos(int[] ppIds, int companyId)
        {
            using var conn = new SqlConnection(_connectionString);

            try
            {
                conn.Open();
                using var transaction = conn.BeginTransaction();

                // Obtener días de expiración
                int days = ObtenerDiasExpiracion(companyId);
                var dateExpire = DateTime.Now.AddDays(days);

                // Actualizar usuarios
                using (var cmdUsuarios = new SqlCommand(@"UPDATE u SET 
                    u.bit_activo = 1, 
                    u.fecha_actualizacion = @fechaActual, 
                    u.fecha_expiracion = @fechaExpiracion
                    FROM PyxoomUser.Usuario u
                    INNER JOIN PyxoomUser.Persona_Proceso pp ON u.id_persona = pp.id_persona
                    WHERE pp.id_persona_proceso IN (" + string.Join(",", ppIds) + ")", conn, transaction))
                {
                    cmdUsuarios.Parameters.AddWithValue("@fechaActual", DateTime.Now);
                    cmdUsuarios.Parameters.AddWithValue("@fechaExpiracion", dateExpire);
                    cmdUsuarios.ExecuteNonQuery();
                }

                // Actualizar procesos
                using (var cmdProcesos = new SqlCommand(@"UPDATE PyxoomUser.Persona_Proceso 
                    SET envio_datos = envio_datos + 1 
                    WHERE id_persona_proceso IN (" + string.Join(",", ppIds) + ")", conn, transaction))
                {
                    cmdProcesos.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error actualizando usuarios y procesos: " + ex.Message);
                throw;
            }
        }

        public string ObtenerUbicacionHelper(int companyId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"SELECT ubicacion 
                FROM PyxoomUser.Configuracion_Correo 
                WHERE id_empresa = @companyId", conn)
            {
                CommandType = CommandType.Text
            };

            cmd.Parameters.AddWithValue("@companyId", companyId);

            try
            {
                conn.Open();
                var result = cmd.ExecuteScalar()?.ToString();
                return !string.IsNullOrEmpty(result) ? Encryption.Decrypt(result) : string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener ubicación helper: " + ex.Message);
                return string.Empty;
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

                // Obtener ubicación usando el método correcto
                string ubicacion = ObtenerUbicacionHelper(companyId);

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
                            TemplateParameters = new Dictionary<string, string>()
                        };

                        // Procesar mensaje principal con reemplazos
                        string mainMessage = item.mainMessage ?? "";

                        // Si no hay mensaje en BD, usar uno por defecto
                        if (string.IsNullOrEmpty(mainMessage))
                        {
                            mainMessage = item.isEnglish
                                ? "Dear @nombre,\n\nYour access credentials are:\nUser: @usuario\nPassword: @contraseña\n\nYou can access here: @liga\n\nThis access expires in @dias days.\n\nBest regards,\n@administrador"
                                : "Estimado(a) @nombre,\n\nTus credenciales de acceso son:\nUsuario: @usuario\nContraseña: @contraseña\n\nPuedes ingresar aquí: @liga\n\nEste acceso expira en @dias días.\n\nSaludos,\n@administrador";
                        }

                        mainMessage = mainMessage.Replace("@nombre", item.NameCandidateEmail);
                        mainMessage = mainMessage.Replace("@usuario", item.login);
                        mainMessage = mainMessage.Replace("@contraseña", item.password);
                        mainMessage = mainMessage.Replace("@name", item.NameCandidateEmail);
                        mainMessage = mainMessage.Replace("@jobposition", item.puesto ?? "");
                        mainMessage = mainMessage.Replace("@administrador", item.responsable);
                        mainMessage = mainMessage.Replace("@liga", ubicacion + "?lang=" + (item.isEnglish ? "en" : "es"));
                        mainMessage = mainMessage.Replace("@dias", item.dias_expiracion.ToString());

                        emailRequest.TemplateParameters.Add("mainMessage", mainMessage);

                        // Agregar imagen
                        var urlPyxoom = ObtenerUbicacionHelper(companyId);
                        emailRequest.TemplateParameters.Add("cid:uniqueId", urlPyxoom.Replace("/Home/Login", "") + "/Content/images/pyxoom-psw.png");

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
                    Message = emailsEnviados > 0 ? "Se enviaron los emails exitosamente" : "No se pudieron enviar emails"
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
                else
                {
                    Console.WriteLine($"Template no encontrado: {templatePath}, usando template por defecto");
                    content = ObtenerTemplatePorDefecto();
                }
            }
            else if (!string.IsNullOrEmpty(emailRequest.HtmlContent))
            {
                content = emailRequest.HtmlContent;
            }
            else
            {
                // Si no hay template ni HTML, usar el por defecto
                content = ObtenerTemplatePorDefecto();
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

        private string ObtenerTemplatePorDefecto()
        {
            return @"<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <title></title>
    <style type=""text/css"">
        body {
            font-family: Arial, Verdana, Sans-Serif;
        }
        table tr td {
            text-align: left;
        }
        .regular-text {
            font-size: 12px;
            text-align: justify;
        }
        .container-data-title {
            width: 90px;
            font-size: 13px;
            font-weight: bold;
            padding: 5px;
            border: solid 1px #EAF282;
        }
        .container-data-text {
            font-size: 13px;
            font-weight: bold;
            padding: 5px;
            background: #EAF282;
        }
        .style3 {
            height: 83px;
            width: 211px;
        }
    </style>
</head>
<body>
    <div style=""width: 100%; margin: auto; text-align: center; padding: 3px;"">
        <div>
            <table cellpadding=""0"" cellspacing=""0"" width=""80%"">
                <tr>
                    <td style=""width: 80%; background: url(../../Resources/Images/title-bg.png) repeat-x"">
                        <h3 style=""margin: 0px 0px 5px 0px"" align=""center"">
                            <b>Cuenta de usuario </b>
                        </h3>
                    </td>
                </tr>
                <tr>
                    <td colspan=""2"" class=""regular-text"" style=""padding: 10px 10px 10px 10px"" align=""left"">
                        <br />
                        @mainMessage
                    </td>
                </tr>
                <tr>
                    <td colspan=""2"" style=""height: 55px; padding-right: 10px; text-align: right; font-size: 11px; background: url(../../Resources/Images/title-footer-bg.png) repeat-x"">
                        <table cellpadding=""0"" cellspacing=""0"" align=""right"">
                            <tr>
                                <td style=""background: url(../../Resources/Images/pyxoom-psw.png) repeat-x"" class=""style3"">
                                    <img align=""right"" alt=""PYXOOM"" border=""0"" hspace=""0"" src=""cid:uniqueId"" />
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </div>
    </div>
</body>
</html>";
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
