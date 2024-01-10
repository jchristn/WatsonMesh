namespace WatsonMesh
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Commonly-used static methods.
    /// </summary>
    internal static class Common
    {
        internal static string Md5(byte[] data)
        {
            if (data == null) return null;
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(data);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
                string ret = sb.ToString();
                return ret;
            }
        }

        internal static string Md5(string data)
        {
            if (String.IsNullOrEmpty(data)) return null;
            using (MD5 md5 = MD5.Create())
            {
                byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(data);
                byte[] hash = md5.ComputeHash(dataBytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
                string ret = sb.ToString();
                return ret;
            }
        }

        internal static byte[] Sha1(byte[] data)
        {
            if (data == null || data.Length < 1) return null;
            using (SHA1 s = SHA1.Create())
            {
                return s.ComputeHash(data);
            }
        }

        internal static byte[] Sha256(byte[] data)
        {
            if (data == null || data.Length < 1) return null;
            using (SHA256 s = SHA256.Create())
            {
                return s.ComputeHash(data);
            }
        }

        internal static byte[] AppendBytes(byte[] head, byte[] tail)
        {
            byte[] ret;

            if (head == null) return tail;
            else if (tail == null) return head;
            else
            {
                ret = new byte[head.Length + tail.Length];
                Buffer.BlockCopy(head, 0, ret, 0, head.Length);
                Buffer.BlockCopy(tail, 0, ret, head.Length, tail.Length);
                return ret;
            }
        }

        internal static async Task<byte[]> ReadStream(long contentLength, Stream stream)
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
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
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

        internal static async Task<byte[]> StreamToBytes(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await ms.WriteAsync(buffer, 0, read);
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
