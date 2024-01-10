namespace WatsonMesh
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Xml;
    using System.Xml.Serialization;

    /// <summary>
    /// Default serialization helper.
    /// </summary>
    public static class SerializationHelper
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static ExceptionConverter<Exception> _ExceptionConverter = new ExceptionConverter<Exception>();
        private static NameValueCollectionConverter _NameValueCollectionConverter = new NameValueCollectionConverter();
        private static JsonStringEnumConverter _StringEnumConverter = new JsonStringEnumConverter();
        private static DateTimeConverter _DateTimeConverter = new DateTimeConverter();
        private static IPAddressConverter _IPAddressConverter = new IPAddressConverter();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Instantiation method to support fixups for various environments, e.g. Unity.
        /// </summary>
        public static void InstantiateConverter()
        {
            try
            {
                Activator.CreateInstance<ExceptionConverter<Exception>>();
                Activator.CreateInstance<NameValueCollectionConverter>();
                Activator.CreateInstance<JsonStringEnumConverter>();
                Activator.CreateInstance<DateTimeConverter>();
                Activator.CreateInstance<IPAddressConverter>();
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// Deserialize JSON to an instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Instance.</returns>
        public static T DeserializeJson<T>(string json)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.AllowTrailingCommas = true;
            options.ReadCommentHandling = JsonCommentHandling.Skip;
            options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

            options.Converters.Add(_ExceptionConverter);
            options.Converters.Add(_NameValueCollectionConverter);
            options.Converters.Add(_StringEnumConverter);
            options.Converters.Add(_DateTimeConverter);
            options.Converters.Add(_IPAddressConverter);

            return JsonSerializer.Deserialize<T>(json, options);
        }

        /// <summary>
        /// Serialize object to JSON.
        /// </summary>
        /// <param name="obj">Object.</param>
        /// <param name="pretty">Pretty print.</param>
        /// <returns>JSON.</returns>
        public static string SerializeJson(object obj, bool pretty = true)
        {
            if (obj == null) return null;

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            // see https://github.com/dotnet/runtime/issues/43026
            options.Converters.Add(_ExceptionConverter);
            options.Converters.Add(_NameValueCollectionConverter);
            options.Converters.Add(_StringEnumConverter);
            options.Converters.Add(_DateTimeConverter);
            options.Converters.Add(_IPAddressConverter);

            if (!pretty)
            {
                options.WriteIndented = false;
                return JsonSerializer.Serialize(obj, options);
            }
            else
            {
                options.WriteIndented = true;
                return JsonSerializer.Serialize(obj, options);
            }
        }

        /// <summary>
        /// Copy an object.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="o">Object.</param>
        /// <returns>Instance.</returns>
        public static T CopyObject<T>(object o)
        {
            if (o == null) return default(T);
            string json = SerializeJson(o, false);
            T ret = DeserializeJson<T>(json);
            return ret;
        }

        #endregion

        #region Private-Methods

        #endregion

        #region Private-Classes

        private class ExceptionConverter<TExceptionType> : JsonConverter<TExceptionType>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(Exception).IsAssignableFrom(typeToConvert);
            }

            public override TExceptionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException("Deserializing exceptions is not allowed");
            }

            public override void Write(Utf8JsonWriter writer, TExceptionType value, JsonSerializerOptions options)
            {
                var serializableProperties = value.GetType()
                    .GetProperties()
                    .Select(uu => new { uu.Name, Value = uu.GetValue(value) })
                    .Where(uu => uu.Name != nameof(Exception.TargetSite));

                if (options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
                {
                    serializableProperties = serializableProperties.Where(uu => uu.Value != null);
                }

                var propList = serializableProperties.ToList();

                if (propList.Count == 0)
                {
                    // Nothing to write
                    return;
                }

                writer.WriteStartObject();

                foreach (var prop in propList)
                {
                    writer.WritePropertyName(prop.Name);
                    JsonSerializer.Serialize(writer, prop.Value, options);
                }

                writer.WriteEndObject();
            }
        }

        private class NameValueCollectionConverter : JsonConverter<NameValueCollection>
        {
            public override NameValueCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, NameValueCollection value, JsonSerializerOptions options)
            {
                var val = value.Keys.Cast<string>()
                    .ToDictionary(k => k, k => string.Join(", ", value.GetValues(k)));
                System.Text.Json.JsonSerializer.Serialize(writer, val);
            }
        }

        private class DateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(
                        ref Utf8JsonReader reader,
                        Type typeToConvert,
                        JsonSerializerOptions options)
            {
                string str = reader.GetString();

                DateTime val;
                if (DateTime.TryParse(str, out val)) return val;

                throw new FormatException("The JSON value '" + str + "' could not be converted to System.DateTime.");
            }

            public override void Write(
                Utf8JsonWriter writer,
                DateTime dateTimeValue,
                JsonSerializerOptions options)
            {
                writer.WriteStringValue(dateTimeValue.ToString(
                    "yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture));
            }

            private List<string> _AcceptedFormats = new List<string>
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssK",
                "yyyy-MM-dd HH:mm:ss.ffffff",
                "yyyy-MM-ddTHH:mm:ss.ffffff",
                "yyyy-MM-ddTHH:mm:ss.fffffffK",
                "yyyy-MM-dd",
                "MM/dd/yyyy HH:mm",
                "MM/dd/yyyy hh:mm tt",
                "MM/dd/yyyy H:mm",
                "MM/dd/yyyy h:mm tt",
                "MM/dd/yyyy HH:mm:ss"
            };
        }

        private class IPAddressConverter : JsonConverter<IPAddress>
        {
            public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string str = reader.GetString();
                return IPAddress.Parse(str);
            }

            public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }

        private class XmlWriterExtended : XmlWriter
        {
            private XmlWriter baseWriter;

            public XmlWriterExtended(XmlWriter w)
            {
                baseWriter = w;
            }

            // Force WriteEndElement to use WriteFullEndElement
            public override void WriteEndElement() { baseWriter.WriteFullEndElement(); }

            public override void WriteFullEndElement()
            {
                baseWriter.WriteFullEndElement();
            }

            public override void Close()
            {
                baseWriter.Close();
            }

            public override void Flush()
            {
                baseWriter.Flush();
            }

            public override string LookupPrefix(string ns)
            {
                return (baseWriter.LookupPrefix(ns));
            }

            public override void WriteBase64(byte[] buffer, int index, int count)
            {
                baseWriter.WriteBase64(buffer, index, count);
            }

            public override void WriteCData(string text)
            {
                baseWriter.WriteCData(text);
            }

            public override void WriteCharEntity(char ch)
            {
                baseWriter.WriteCharEntity(ch);
            }

            public override void WriteChars(char[] buffer, int index, int count)
            {
                baseWriter.WriteChars(buffer, index, count);
            }

            public override void WriteComment(string text)
            {
                baseWriter.WriteComment(text);
            }

            public override void WriteDocType(string name, string pubid, string sysid, string subset)
            {
                baseWriter.WriteDocType(name, pubid, sysid, subset);
            }

            public override void WriteEndAttribute()
            {
                baseWriter.WriteEndAttribute();
            }

            public override void WriteEndDocument()
            {
                baseWriter.WriteEndDocument();
            }

            public override void WriteEntityRef(string name)
            {
                baseWriter.WriteEntityRef(name);
            }

            public override void WriteProcessingInstruction(string name, string text)
            {
                baseWriter.WriteProcessingInstruction(name, text);
            }

            public override void WriteRaw(string data)
            {
                baseWriter.WriteRaw(data);
            }

            public override void WriteRaw(char[] buffer, int index, int count)
            {
                baseWriter.WriteRaw(buffer, index, count);
            }

            public override void WriteStartAttribute(string prefix, string localName, string ns)
            {
                baseWriter.WriteStartAttribute(prefix, localName, ns);
            }

            public override void WriteStartDocument(bool standalone)
            {
                baseWriter.WriteStartDocument(standalone);
            }

            public override void WriteStartDocument()
            {
                baseWriter.WriteStartDocument();
            }

            public override void WriteStartElement(string prefix, string localName, string ns)
            {
                baseWriter.WriteStartElement(prefix, localName, ns);
            }

            public override WriteState WriteState
            {
                get { return baseWriter.WriteState; }
            }

            public override void WriteString(string text)
            {
                baseWriter.WriteString(text);
            }

            public override void WriteSurrogateCharEntity(char lowChar, char highChar)
            {
                baseWriter.WriteSurrogateCharEntity(lowChar, highChar);
            }

            public override void WriteWhitespace(string ws)
            {
                baseWriter.WriteWhitespace(ws);
            }
        }
    }

    #endregion
}