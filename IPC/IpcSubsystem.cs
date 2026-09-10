using System;
using System.Text.Json;
using AirGestureAI.Utilities;

namespace AirGestureAI.IPC
{
    /// <summary>
    /// Performs JSON serialization for IPC messages using System.Text.Json to ensure correct escaping and robust parsing.
    /// </summary>
    public static class IpcSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>Serializes an IPC message to string.</summary>
        public static string Serialize(IpcMessage msg)
        {
            return JsonSerializer.Serialize(msg, Options);
        }

        /// <summary>Deserializes an IPC message string.</summary>
        public static IpcMessage Deserialize(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<IpcMessage>(json, Options) ?? new IpcMessage();
            }
            catch (Exception ex)
            {
                Logger.Error($"IpcSerializer: Deserialization failed for content '{json}'", ex);
                return new IpcMessage { Method = "Error", Payload = $"Deserialization error: {ex.Message}" };
            }
        }
    }

    /// <summary>
    /// Manages security hashes and token validations for pipe messages.
    /// </summary>
    public static class IpcSecurity
    {
        /// <summary>Validates the authentication token.</summary>
        public static bool ValidateToken(string token)
        {
            return token == "SECURE_AIRGESTURE_TOKEN_v4";
        }
    }
}
