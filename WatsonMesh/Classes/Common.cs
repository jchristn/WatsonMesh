using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Watson
{
    /// <summary>
    /// Commonly-used static methods.
    /// </summary>
    internal static class Common
    {
        public static string SerializeJson(object obj, bool pretty)
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

        public static T DeserializeJson<T>(string json)
        {
            if (String.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));

            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception e)
            {
                Console.WriteLine("");
                Console.WriteLine("Exception while deserializing:");
                Console.WriteLine(json);
                Console.WriteLine("");
                Console.WriteLine(SerializeJson(e, true));
                Console.WriteLine("");
                throw e;
            }
        }

        public static T DeserializeJson<T>(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return DeserializeJson<T>(Encoding.UTF8.GetString(data));
        }

        public static bool InputBoolean(string question, bool yesDefault)
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

        public static string InputString(string question, string defaultAnswer, bool allowNull)
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

        public static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
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

        public static string Md5(byte[] data)
        {
            if (data == null) return null;
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(data);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
            string ret = sb.ToString();
            return ret;
        }

        public static string Md5(string data)
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

        public static byte[] Sha1(byte[] data)
        {
            if (data == null || data.Length < 1) return null;
            SHA1Managed s = new SHA1Managed();
            return s.ComputeHash(data);
        }

        public static byte[] Sha256(byte[] data)
        {
            if (data == null || data.Length < 1) return null;
            SHA256Managed s = new SHA256Managed();
            return s.ComputeHash(data);
        }

        public static byte[] AppendBytes(byte[] head, byte[] tail)
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

        public static byte[] ReadStream(long contentLength, Stream stream)
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

        public static byte[] ReadStream(Stream stream)
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

        public static void LogException(Exception e)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine(" = Exception Type: " + e.GetType().ToString());
            Console.WriteLine(" = Exception Data: " + e.Data);
            Console.WriteLine(" = Inner Exception: " + e.InnerException);
            Console.WriteLine(" = Exception Message: " + e.Message);
            Console.WriteLine(" = Exception Source: " + e.Source);
            Console.WriteLine(" = Exception StackTrace: " + e.StackTrace);
            Console.WriteLine("================================================================================");
        }
    }
}
