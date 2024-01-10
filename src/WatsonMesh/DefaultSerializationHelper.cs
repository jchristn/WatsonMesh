﻿namespace WatsonMesh
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Default serialization helper.
    /// </summary>
    public class DefaultSerializationHelper : ISerializationHelper
    {
        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DefaultSerializationHelper()
        {
            InstantiateConverter();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Deserialize JSON to an instance.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Instance.</returns>
        public T DeserializeJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json);
        }

        /// <summary>
        /// Serialize object to JSON.
        /// </summary>
        /// <param name="obj">Object.</param>
        /// <param name="pretty">Pretty print.</param>
        /// <returns>JSON.</returns>
        public string SerializeJson(object obj, bool pretty = true)
        {
            if (obj == null) return null;

            if (!pretty)
            {
                return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            }
            else
            {
                return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            }
        }

        /// <summary>
        /// Instantiation method to support fixups for various environments, e.g. Unity.
        /// </summary>
        public void InstantiateConverter()
        {
            try
            {
                Activator.CreateInstance<JsonStringEnumConverter>();
            }
            catch (Exception)
            {

            }
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}