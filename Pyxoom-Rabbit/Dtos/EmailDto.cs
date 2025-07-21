using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pyxoom_Rabbit.Dtos
{
    public class EmailConfigurationDto
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool UseSSL { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ApiKey { get; set; }
        public string Sender { get; set; }
    }

    public class EmailRequestDto
    {
        public string PersonProcessId { get; set; }
        public int CompanyId { get; set; }
        public string To { get; set; }
        public string Subject { get; set; }
        public string HtmlContent { get; set; }
        public string TemplatePath { get; set; }
        public Dictionary<string, string> TemplateParameters { get; set; }
        public byte[] Attachment { get; set; }
        public string AttachmentName { get; set; }
        public bool IsEnglish { get; set; }
    }

    public class PersonInteractiveDto
    {
        public string login { get; set; }
        public string password { get; set; }
        public string NameCandidateEmail { get; set; }
        public string puesto { get; set; }
        public string responsable { get; set; }
        public string correo_electronico { get; set; }
        public int dias_expiracion { get; set; }
        public string mainMessage { get; set; }
        public bool hasInteractive { get; set; }
        public bool isEnglish { get; set; }
    }

    public class EmailResultDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string ErrorDetails { get; set; }
    }
}
