using Microsoft.Data.SqlClient;
using Pyxoom_Rabbit.Dtos;
using Pyxoom_Rabbit.Services;
using System.Data;

namespace Pyxoom_Rabbit.Database
{
    public class PyxoomDbService
    {
        private readonly string _connectionString;

        public PyxoomDbService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void EjecutarConsulta()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            var command = new SqlCommand("SELECT * FROM PyxoomUser.Persona", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                Console.WriteLine(reader[0]); // ejemplo
            }
        }

        public PersonaInfoDto ObtenerInfoPersona(int personaId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"SELECT 
                p.primer_nombre,
                p.apellido_paterno,
                p.correo_electronico,
                p.telefono_movil,
        
                -- Verificación de datos faltantes (TRUE = le falta el dato, FALSE = tiene el dato)
                CASE 
                    WHEN p.primer_nombre IS NULL OR LTRIM(RTRIM(p.primer_nombre)) = '' 
                    THEN 1 
                    ELSE 0 
                END AS falta_primer_nombre,
        
                CASE 
                    WHEN p.segundo_nombre IS NULL OR LTRIM(RTRIM(p.segundo_nombre)) = '' 
                    THEN 1 
                    ELSE 0 
                END AS falta_segundo_nombre,
        
                CASE 
                    WHEN p.apellido_paterno IS NULL OR LTRIM(RTRIM(p.apellido_paterno)) = '' 
                    THEN 1 
                    ELSE 0 
                END AS falta_apellido_paterno,
        
                CASE 
                    WHEN p.apellido_materno IS NULL OR LTRIM(RTRIM(p.apellido_materno)) = '' 
                    THEN 1 
                    ELSE 0 
                END AS falta_apellido_materno,
        
                -- Verificación de nombre completo (si falta primer_nombre O apellido_paterno)
                CASE 
                    WHEN (p.primer_nombre IS NULL OR LTRIM(RTRIM(p.primer_nombre)) = '') 
                      OR (p.apellido_paterno IS NULL OR LTRIM(RTRIM(p.apellido_paterno)) = '')
                    THEN 1 
                    ELSE 0 
                END AS falta_nombre_completo,
        
                CASE 
                    WHEN p.sexo IS NULL OR LTRIM(RTRIM(p.sexo)) = '' 
                    THEN 1 
                    ELSE 0 
                END AS falta_sexo,
        
                CASE 
                    WHEN p.fecha_nacimiento IS NULL 
                    THEN 1 
                    ELSE 0 
                END AS falta_fecha_nacimiento,
        
                CASE 
                    WHEN p.id_escolaridad IS NULL 
                    THEN 1 
                    ELSE 0 
                END AS falta_id_escolaridad,
        
                CASE 
                    WHEN p.id_estado_civil IS NULL 
                    THEN 1 
                    ELSE 0 
                END AS falta_id_estado_civil
            
            FROM PyxoomUser.Persona p
            WHERE p.id_persona = @persona_id", conn)
            {
                CommandType = CommandType.Text
            };

            cmd.Parameters.AddWithValue("@persona_id", personaId);

            var personaInfo = new PersonaInfoDto();

            try
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();

                if (reader.HasRows && reader.Read())
                {
                    string primerNombreEncriptado = DbUtils.GetNullableString(reader, "primer_nombre");
                    string apellidoPaternoEncriptado = DbUtils.GetNullableString(reader, "apellido_paterno");

                    personaInfo.PrimerNombre = !string.IsNullOrEmpty(primerNombreEncriptado)
                        ? Encryption.Decrypt(primerNombreEncriptado)
                        : primerNombreEncriptado;

                    personaInfo.ApellidoPaterno = !string.IsNullOrEmpty(apellidoPaternoEncriptado)
                        ? Encryption.Decrypt(apellidoPaternoEncriptado)
                        : apellidoPaternoEncriptado;

                    personaInfo.CorreoElectronico = DbUtils.GetNullableString(reader, "correo_electronico");
                    personaInfo.TelefonoMovil = DbUtils.GetNullableString(reader, "telefono_movil");
                    personaInfo.FaltaPrimerNombre = Convert.ToBoolean(reader["falta_primer_nombre"]);
                    personaInfo.FaltaSegundoNombre = Convert.ToBoolean(reader["falta_segundo_nombre"]);
                    personaInfo.FaltaApellidoPaterno = Convert.ToBoolean(reader["falta_apellido_paterno"]);
                    personaInfo.FaltaApellidoMaterno = Convert.ToBoolean(reader["falta_apellido_materno"]);
                    personaInfo.FaltaNombreCompleto = Convert.ToBoolean(reader["falta_nombre_completo"]);
                    personaInfo.FaltaSexo = Convert.ToBoolean(reader["falta_sexo"]);
                    personaInfo.FaltaFechaNacimiento = Convert.ToBoolean(reader["falta_fecha_nacimiento"]);
                    personaInfo.FaltaEscolaridad = Convert.ToBoolean(reader["falta_id_escolaridad"]);
                    personaInfo.FaltaEstadoCivil = Convert.ToBoolean(reader["falta_id_estado_civil"]);
                }

                return personaInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener información de persona: " + ex.Message);
                throw;
            }
        }

        public string ObtenerAvisoDePrivacidad(int idEmpresa)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"SELECT t.id_empresa, 
                avisoPrivacidad = taviso.texto, 
                avisoPrivacidadInterno = tAvisoInt.texto
            FROM PyxoomUser.EmpresaTextosInteractive t
            LEFT JOIN PyxoomUser.Texto_idioma taviso ON t.id_texto_avisoPrivacidad = taviso.id_texto AND taviso.id_idioma = 1
            LEFT JOIN PyxoomUser.Texto_idioma tAvisoInt ON t.id_texto_avisoInternoPrivacidad = tAvisoInt.id_texto AND tAvisoInt.id_idioma = 1
            WHERE t.id_empresa = @id_empresa", conn)
            {
                CommandType = CommandType.Text
            };

            cmd.Parameters.AddWithValue("@id_empresa", idEmpresa);

            try
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();

                if (reader.HasRows && reader.Read())
                {
                    // Obtener el aviso de privacidad encriptado
                    string avisoPrivacidadEncriptado = DbUtils.GetNullableString(reader, "avisoPrivacidad");

                    // Desencriptar el aviso de privacidad
                    return !string.IsNullOrEmpty(avisoPrivacidadEncriptado)
                        ? Encryption.Decrypt(avisoPrivacidadEncriptado)
                        : string.Empty;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener aviso de privacidad: " + ex.Message);
                throw;
            }
        }

        public List<PreguntaDto> ObtenerPreguntasVacante(int vacanteId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SP_ObtenerPreguntasRespuestasKit", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@vacante_id", vacanteId);

            var preguntaDict = new Dictionary<int, PreguntaDto>();

            try
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    int idPregunta = DbUtils.GetNullableInt(reader, "id_pregunta") ?? 0;

                    // Crear o obtener la pregunta usando id_pregunta como clave
                    if (!preguntaDict.ContainsKey(idPregunta))
                    {
                        var pregunta = new PreguntaDto
                        {
                            IdPregunta = idPregunta,
                            IdTipo = DbUtils.GetNullableInt(reader, "id_tipo") ?? 0,
                            Tipo = DbUtils.GetNullableString(reader, "texto_tipo_pregunta"),
                            EsClave = reader["es_clave"] != DBNull.Value && Convert.ToBoolean(reader["es_clave"]),
                            EsDescarte = reader["es_descarte"] != DBNull.Value && Convert.ToBoolean(reader["es_descarte"]),
                            EsPreguntaValidacion = reader["es_pregunta_validacion"] != DBNull.Value && Convert.ToBoolean(reader["es_pregunta_validacion"]),
                            IdCategoria = DbUtils.GetNullableInt(reader, "id_categoria") ?? 0,
                            Orden = DbUtils.GetNullableInt(reader, "orden") ?? 0,
                            TextoPregunta = DbUtils.GetNullableString(reader, "texto_pregunta")
                        };

                        preguntaDict[idPregunta] = pregunta;
                    }

                    // Agregar respuesta si existe (verificar que id_respuesta no sea null)
                    var idRespuesta = DbUtils.GetNullableInt(reader, "id_respuesta");
                    if (idRespuesta.HasValue)
                    {
                        var respuesta = new RespuestaDto
                        {
                            IdPregunta = idPregunta,
                            IdRespuesta = (int)idRespuesta,
                            OrdenRespuesta = DbUtils.GetNullableInt(reader, "orden_respuesta") ?? 0,
                            RespuestaEsClave = reader["respuesta_es_clave"] != DBNull.Value && Convert.ToBoolean(reader["respuesta_es_clave"]),
                            MinValor = DbUtils.GetNullableInt(reader, "min_valor"),
                            MaxValor = DbUtils.GetNullableInt(reader, "max_valor"),
                            TextoRespuesta = DbUtils.GetNullableString(reader, "texto_respuesta")
                        };

                        preguntaDict[idPregunta].Respuestas.Add(respuesta);
                    }
                }

                // Convertir el diccionario a lista y ordenar
                var preguntas = preguntaDict.Values.OrderBy(p => p.Orden).ToList();

                // Ordenar respuestas dentro de cada pregunta
                foreach (var pregunta in preguntas)
                {
                    pregunta.Respuestas = pregunta.Respuestas.OrderBy(r => r.OrdenRespuesta).ToList();
                }

                return preguntas;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener preguntas de vacante: " + ex.Message);
                throw;
            }
        }


        public void InsertarRespuestasRegistro(List<RespuestasRegistroDto> respuestas, int idPersonaProceso)
        {
            using var conn = new SqlConnection(_connectionString);

            try
            {
                conn.Open();

                foreach (var respuesta in respuestas)
                {
                    using var cmd = new SqlCommand(@"INSERT INTO Pyxoom.KitRespuestaRegistro 
                (id_respuesta, id_pregunta, respuesta_texto, id_persona_proceso)
                VALUES (@id_respuesta, @id_pregunta, @respuesta_texto, @id_persona_proceso)", conn)
                    {
                        CommandType = CommandType.Text
                    };

                    // Agregar parámetros
                    cmd.Parameters.AddWithValue("@id_respuesta", (object)respuesta.id_respuesta ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id_pregunta", respuesta.id_pregunta);
                    cmd.Parameters.AddWithValue("@respuesta_texto", (object)respuesta.respuestaTexto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id_persona_proceso", idPersonaProceso);

                    cmd.ExecuteNonQuery();
                }

                Console.WriteLine($"Se insertaron {respuestas.Count} respuestas correctamente para el proceso {idPersonaProceso}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al insertar respuestas: {ex.Message}");
                throw;
            }
        }
        public void EjecutarSP_LeerVariables(string folio)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SP_TRAER_TABLERO_POR_FOLIO", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@folio", folio);

            try
            {
                conn.Open();
                using var reader = cmd.ExecuteReader();

                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        int? id = DbUtils.GetNullableInt(reader, "Id");
                        string correo = DbUtils.GetNullableString(reader, "Correo");
                        string cliente = DbUtils.GetNullableString(reader, "Cliente");
                        string estatus = DbUtils.GetNullableString(reader, "Estatus");

                        Console.WriteLine($"ID: {id}, Correo: {correo}, Cliente: {cliente}, Estatus: {estatus}");
                    }
                }
                else
                {
                    Console.WriteLine("No se encontraron resultados.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al ejecutar SP: " + ex.Message);
            }
        }

        public void ActualizarDatosFaltantesPersona(List<RespuestasRegistroDto> respuestasFaltantes, int idPersona)
        {
            using var conn = new SqlConnection(_connectionString);

            try
            {
                conn.Open();

                foreach (var respuesta in respuestasFaltantes)
                {
                    // Determinar qué campo actualizar según el ID de la pregunta
                    string campoActualizar = DeterminarCampoPersona(respuesta.id_pregunta);

                    if (!string.IsNullOrEmpty(campoActualizar))
                    {
                        string valorActualizar = ObtenerValorParaActualizar(respuesta, campoActualizar);

                        if (!string.IsNullOrEmpty(valorActualizar))
                        {
                            string query = $@"UPDATE PyxoomUser.Persona 
                                    SET {campoActualizar} = @valor 
                                    WHERE id_persona = @id_persona";

                            using var cmd = new SqlCommand(query, conn)
                            {
                                CommandType = CommandType.Text
                            };

                            cmd.Parameters.AddWithValue("@valor", valorActualizar);
                            cmd.Parameters.AddWithValue("@id_persona", idPersona);

                            int rowsAffected = cmd.ExecuteNonQuery();

                            if (rowsAffected > 0)
                            {
                                Console.WriteLine($"Campo {campoActualizar} actualizado correctamente para persona {idPersona}");
                            }
                        }
                    }
                }

                Console.WriteLine($"Datos faltantes actualizados correctamente para la persona {idPersona}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar datos faltantes: {ex.Message}");
                throw;
            }
        }

        private string DeterminarCampoPersona(int idPregunta)
        {
            return idPregunta switch
            {
                1000 => "primer_nombre", // Nombre completo (tomamos solo primer nombre)
                1001 => "sexo",          // Género
                1002 => "fecha_nacimiento", // Fecha de nacimiento
                1003 => "id_escolaridad",   // Escolaridad
                1004 => "id_estado_civil",  // Estado civil
                _ => string.Empty
            };
        }

        private string ObtenerValorParaActualizar(RespuestasRegistroDto respuesta, string campo)
        {
            switch (campo)
            {
                case "primer_nombre":
                    return !string.IsNullOrEmpty(respuesta.respuestaTexto)
                        ? Encryption.Encrypt(respuesta.respuestaTexto)
                        : string.Empty;

                case "sexo":
                    return respuesta.id_respuesta switch
                    {
                        1 => "M", // Masculino
                        2 => "F", // Femenino
                        _ => string.Empty
                    };

                case "fecha_nacimiento":
                    return !string.IsNullOrEmpty(respuesta.respuestaTexto)
                        ? Encryption.Encrypt(respuesta.respuestaTexto)
                        : string.Empty;

                case "id_escolaridad":
                    return respuesta.id_respuesta.ToString() ?? string.Empty;

                case "id_estado_civil":
                    return respuesta.id_respuesta.ToString() ?? string.Empty;

                default:
                    return string.Empty;
            }
        }

    }

}
