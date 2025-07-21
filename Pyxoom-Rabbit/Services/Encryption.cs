using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Pyxoom_Rabbit.Services
{
    public static class Encryption
    {
        #region Decrypt(string sTexto)
        /// <summary>
        /// Desencripta una cadena de texto intentando múltiples métodos
        /// </summary>
        /// <param name="sTexto">Texto a desencriptar</param>
        /// <returns>string</returns>
        public static string Decrypt(string sTexto)
        {
            if (string.IsNullOrEmpty(sTexto))
                return sTexto;

            try
            {
                // Intentar primero con AES
                return AES.Decrypt(sTexto);
            }
            catch
            {
                try
                {
                    // Si AES falla, intentar con DES clásico
                    return ClassicEncryption.Decrypt(sTexto);
                }
                catch
                {
                    // Si ambos fallan, devolver el texto original
                    return sTexto;
                }
            }
        }
        #endregion

        #region DecryptFullName(string EncryptedName)
        /// <summary>
        /// Desencripta un nombre concatenado por espacios
        /// </summary>
        /// <param name="EncryptedName">Nombre concatenado por espacios</param>
        /// <returns>string</returns>
        public static string DecryptFullName(string EncryptedName)
        {
            if (EncryptedName == null)
                return string.Empty;

            string Name = string.Empty;
            Char delimiter = ' ';
            String[] nombres = EncryptedName.Split(delimiter);
            for (int i = 0; i < nombres.Length; i++)
            {
                if (i == 0)
                    Name = Decrypt(nombres[i]);
                else
                    Name += " " + Decrypt(nombres[i]);
            }
            return Name;
        }
        #endregion

        #region Encrypt(string sTexto)
        /// <summary>
        /// Encripta una cadena de texto
        /// </summary>
        /// <param name="sTexto">Texto a encriptar</param>
        /// <returns>string</returns>
        public static string Encrypt(string sTexto)
        {
            if (String.IsNullOrEmpty(sTexto))
            {
                return sTexto;
            }

            return AES.Encrypt(sTexto);
        }
        #endregion

        #region DecryptInteger(string sTexto)
        /// <summary>
        /// Desencripta un texto e intenta convertirlo a entero
        /// </summary>
        /// <param name="sTexto">Texto a desencriptar</param>
        /// <returns>int</returns>
        public static int DecryptInteger(string sTexto)
        {
            int result = 0;
            string tDecrypt = Decrypt(sTexto);
            tDecrypt = tDecrypt == null ? "0" : tDecrypt;
            if (int.TryParse(tDecrypt, out result))
                return result;
            else return 0;
        }
        #endregion



    }

    public static class ClassicEncryption
    {
        private const string ENCRYPT_KEY_CODE = "pfo3d9Ps";

        public static string Decrypt(string sTexto)
        {
            try
            {
                return ASCIIDecrypt(sTexto);
            }
            catch
            {
                return sTexto;
            }
        }

        public static string Encrypt(string sTexto)
        {
            if (String.IsNullOrEmpty(sTexto))
            {
                return sTexto;
            }

            return ASCIIEncrypt(sTexto);
        }

        public static int DecryptInteger(string sTexto)
        {
            int result = 0;
            string tDecrypt = Decrypt(sTexto);
            tDecrypt = tDecrypt == null ? "0" : tDecrypt;
            if (int.TryParse(tDecrypt, out result))
                return result;
            else return 0;
        }

        static string ASCIIDecrypt(string sTexto)
        {
            if (String.IsNullOrEmpty(sTexto))
            {
                return sTexto;
            }

            try
            {
                var text64 = Convert.FromBase64String(sTexto);
                using (MemoryStream memoryStream = new MemoryStream(text64))
                {
                    memoryStream.Position = 0;
                    using (DESCryptoServiceProvider deCryptoProvider = new DESCryptoServiceProvider())
                    {
                        var k = ASCIIEncoding.ASCII.GetBytes(ENCRYPT_KEY_CODE);
                        var decriptor = deCryptoProvider.CreateDecryptor(k, k);

                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decriptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader reader = new StreamReader(cryptoStream))
                            {
                                return reader.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                return sTexto;
            }
        }

        static string ASCIIEncrypt(string sTexto)
        {
            if (String.IsNullOrEmpty(sTexto))
            {
                return sTexto;
            }

            byte[] btKey = ASCIIEncoding.ASCII.GetBytes(ENCRYPT_KEY_CODE);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider())
                {
                    var encriptor = cryptoProvider.CreateEncryptor(btKey, btKey);
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encriptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter writer = new StreamWriter(cryptoStream))
                        {
                            writer.Write(sTexto);
                            writer.Flush();
                            cryptoStream.FlushFinalBlock();
                        }
                    }
                }
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }
    }

    public static class AES
    {
        private const string ENCRYPT_KEY_CODE = "9r3Spfo3d9Ps";
        private const string ENCRYPT_NONCE = "9r3Spfo3d9Ps";

        public static byte[] AES_Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes)
        {
            byte[] decryptedBytes = null;
            // Set your salt here to meet your flavor:
            byte[] saltBytes = passwordBytes;

            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged AES = new RijndaelManaged())
                {
                    AES.KeySize = 256;
                    AES.BlockSize = 128;

                    var key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, 1000);
                    AES.Key = key.GetBytes(AES.KeySize / 8);
                    AES.IV = key.GetBytes(AES.BlockSize / 8);

                    AES.Mode = CipherMode.CBC;

                    using (CryptoStream cs = new CryptoStream(ms, AES.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);
                        cs.Close();
                    }
                    decryptedBytes = ms.ToArray();
                }
            }

            return decryptedBytes;
        }

        public static string Decrypt(string decryptedText)
        {
            byte[] bytesToBeDecrypted = Convert.FromBase64String(decryptedText);
            byte[] ba = null;
            byte[] nonce = GetNonceBytes();
            ba = GetPasswordBytes();
            byte[] passwordBytes = System.Security.Cryptography.SHA256.Create().ComputeHash(ba);
            // Hash the password with SHA256
            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);

            //byte[] decryptedBytes = AES_Decrypt(bytesToBeDecrypted, passwordBytes, nonce);
            byte[] decryptedBytes = AES_Decrypt(bytesToBeDecrypted, passwordBytes);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        private static byte[] GetPasswordBytes()
        {
            byte[] ba = null;
            Encoding encU8 = Encoding.UTF8;
            ba = encU8.GetBytes(ENCRYPT_KEY_CODE);

            return System.Security.Cryptography.SHA256.Create().ComputeHash(ba);
        }

        private static byte[] GetNonceBytes()
        {
            byte[] ba = null;
            Encoding encU8 = Encoding.UTF8;
            ba = encU8.GetBytes(ENCRYPT_NONCE);

            return System.Security.Cryptography.SHA256.Create().ComputeHash(ba);
        }

        static string ASCIIEncrypt(string sTexto)
        {
            if (String.IsNullOrEmpty(sTexto))
            {
                return sTexto;
            }

            byte[] btKey = ASCIIEncoding.ASCII.GetBytes(ENCRYPT_KEY_CODE);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider())
                {
                    var encriptor = cryptoProvider.CreateEncryptor(btKey, btKey);
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encriptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter writer = new StreamWriter(cryptoStream))
                        {
                            writer.Write(sTexto);
                            writer.Flush();
                            cryptoStream.FlushFinalBlock();
                        }
                    }
                }
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        // 3. Agregar método Encrypt a la clase AES
        public static string Encrypt(string text)
        {
            byte[] ba = null;
            ba = GetPasswordBytes();
            byte[] passwordBytes = System.Security.Cryptography.SHA256.Create().ComputeHash(ba);
            byte[] nonce = GetNonceBytes();

            byte[] originalBytes = Encoding.UTF8.GetBytes(text);
            byte[] encryptedBytes = null;

            // Hash the password with SHA256
            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);

            encryptedBytes = AES_Encrypt(originalBytes, passwordBytes);

            return Convert.ToBase64String(encryptedBytes);
        }

        public static byte[] AES_Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
        {
            byte[] encryptedBytes = null;
            byte[] saltBytes = passwordBytes;

            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged AES = new RijndaelManaged())
                {
                    AES.KeySize = 256;
                    AES.BlockSize = 128;

                    var key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, 1000);
                    AES.Key = key.GetBytes(AES.KeySize / 8);
                    AES.IV = key.GetBytes(AES.BlockSize / 8);

                    AES.Mode = CipherMode.CBC;

                    using (CryptoStream cs = new CryptoStream(ms, AES.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
                        cs.Close();
                    }
                    encryptedBytes = ms.ToArray();
                }
            }

            return encryptedBytes;
        }
    }
}
