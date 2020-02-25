using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WatsonMesh
{
    /// <summary>
    /// Commonly-used static methods.
    /// </summary>
    internal static class Common
    {
        internal static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
            }
        }

        internal static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        internal static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                int ret = 0;
                if (!Int32.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        }

        internal static string Md5(byte[] data)
        {
            if (data == null) return null;
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(data);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
            string ret = sb.ToString();
            return ret;
        }

        internal static string Md5(string data)
        {
            if (String.IsNullOrEmpty(data)) return null;
            MD5 md5 = MD5.Create();
            byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(data);
            byte[] hash = md5.ComputeHash(dataBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
            string ret = sb.ToString();
            return ret;
        }

        internal static byte[] Sha1(byte[] data)
        {
            if (data == null || data.Length < 1) return null;
            SHA1Managed s = new SHA1Managed();
            return s.ComputeHash(data);
        }

        internal static byte[] Sha256(byte[] data)
        {
            if (data == null || data.Length < 1) return null;
            SHA256Managed s = new SHA256Managed();
            return s.ComputeHash(data);
        }

        internal static byte[] AppendBytes(byte[] head, byte[] tail)
        {
            byte[] ret;

            if (head == null || head.Length == 0)
            {
                if (tail == null || tail.Length == 0) return null;

                ret = new byte[tail.Length];
                Buffer.BlockCopy(tail, 0, ret, 0, tail.Length);
                return ret;
            }
            else
            {
                if (tail == null || tail.Length == 0) return head;

                ret = new byte[head.Length + tail.Length];
                Buffer.BlockCopy(head, 0, ret, 0, head.Length);
                Buffer.BlockCopy(tail, 0, ret, head.Length, tail.Length);
                return ret;
            }
        }

        internal static byte[] ReadStream(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            int bytesRead = 0;
            long bytesRemaining = contentLength;
            byte[] buffer = new byte[65536];
            byte[] ret = null;

            while (bytesRemaining > 0)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    if (bytesRead == buffer.Length)
                    {
                        ret = AppendBytes(ret, buffer);
                    }
                    else
                    {
                        byte[] temp = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                        ret = AppendBytes(ret, temp);
                    } 
                    bytesRemaining -= bytesRead;
                }
            }

            return ret;
        }

        internal static byte[] ReadStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            int bytesRead = 0;
            byte[] buffer = new byte[65536];
            byte[] ret = null;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (bytesRead == buffer.Length)
                {
                    ret = AppendBytes(ret, buffer);
                }
                else
                {
                    byte[] temp = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                    ret = AppendBytes(ret, buffer);
                }
            }

            return ret;
        }

        internal static string BytesToHex(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }

        internal static byte[] HexToBytes(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        internal static string SerializeJson(object obj, bool pretty)
        {
            if (obj == null) return null;
            string json;

            if (pretty)
            {
                json = JsonConvert.SerializeObject(
                  obj,
                  Newtonsoft.Json.Formatting.Indented,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                  });
            }
            else
            {
                json = JsonConvert.SerializeObject(obj,
                  new JsonSerializerSettings
                  {
                      NullValueHandling = NullValueHandling.Ignore,
                      DateTimeZoneHandling = DateTimeZoneHandling.Utc
                  });
            }

            return json;
        }

        internal static T DeserializeJson<T>(string json)
        {
            if (String.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));
            return JsonConvert.DeserializeObject<T>(json);
        }

        internal static T DeserializeJson<T>(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return DeserializeJson<T>(Encoding.UTF8.GetString(data));
        }

        internal static T CopyObject<T>(object o)
        {
            if (o == null) return default(T);
            string json = SerializeJson(o, false);
            T ret = DeserializeJson<T>(json);
            return ret;
        }

        internal static byte[] StreamToBytes(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        internal static void ParseIpPort(string ipPort, out string ip, out int port)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ip = null;
            port = -1;

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                ip = ipPort.Substring(0, colonIndex);
                port = Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }
        }
    }
}
