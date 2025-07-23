using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pyxoom_Rabbit.Dtos
{
    public class PersonaInfoDto
    {
        public string? PrimerNombre { get; set; }
        public string? ApellidoPaterno { get; set; }
        public string? CorreoElectronico { get; set; }
        public string? TelefonoMovil { get; set; }
        public bool FaltaPrimerNombre { get; set; }
        public bool FaltaSegundoNombre { get; set; }
        public bool FaltaApellidoPaterno { get; set; }
        public bool FaltaApellidoMaterno { get; set; }
        public bool FaltaNombreCompleto { get; set; }
        public bool FaltaSexo { get; set; }
        public bool FaltaFechaNacimiento { get; set; }
        public bool FaltaEscolaridad { get; set; }
        public bool FaltaEstadoCivil { get; set; }
    }

    public class PreguntaDto
    {
        public int IdPregunta { get; set; }
        public int IdTipo { get; set; }
        public string Tipo { get; set; }
        public bool EsClave { get; set; }
        public bool EsDescarte { get; set; }
        public bool EsPreguntaValidacion { get; set; }
        public int IdCategoria { get; set; }
        public int Orden { get; set; }
        public string TextoPregunta { get; set; }
        public List<RespuestaDto> Respuestas { get; set; } = new List<RespuestaDto>();
    }

    public class RespuestaDto
    {
        public int IdPregunta { get; set; }
        public int IdRespuesta { get; set; }
        public int OrdenRespuesta { get; set; }
        public bool RespuestaEsClave { get; set; }
        public int? MinValor { get; set; }
        public int? MaxValor { get; set; }
        public string TextoRespuesta { get; set; }
    }

    public class MessageDto
    {
        public int? CompanyId { get; set; }
        public int? PersonId { get; set; }
        public int? PersonProcessId { get; set; }
        public int? VacancyId { get; set; }
        public string Type { get; set; }
        public string Actions { get; set; }
    }

    public class MensajeRespuestasDto
    {
        public List<RespuestasRegistroDto> respuestas { get; set; }
    }

    public class RespuestasRegistroDto
    { 
        public int? id_respuesta { get; set; }
        public int id_pregunta { get; set; }
        public string pregunta { get; set; }
        public string respuesta { get; set; }
    }
    public class ConfiguracionKitDto
    {
        public bool AltaCv { get; set; } = false;
        public bool FiltradoInteligente { get; set; } = false;
    }
    
}
