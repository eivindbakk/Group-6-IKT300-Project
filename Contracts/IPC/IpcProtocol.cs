using System;
using System.IO;
using System.Text;
using System.Text. Json;

namespace Contracts.IPC
{
    /// <summary>
    /// Protocol for serializing/deserializing IPC messages over named pipes.
    /// Uses length-prefixed JSON for reliable message framing.
    /// </summary>
    public static class IpcProtocol
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy. CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        /// <summary>
        /// Serialize message to bytes with length prefix.
        /// </summary>
        public static byte[] Serialize(IpcMessage message)
        {
            string json = JsonSerializer. Serialize(message, JsonOptions);
            byte[] jsonBytes = Encoding. UTF8.GetBytes(json);
            byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

            byte[] result = new byte[4 + jsonBytes. Length];
            Array.Copy(lengthBytes, 0, result, 0, 4);
            Array. Copy(jsonBytes, 0, result, 4, jsonBytes.Length);

            return result;
        }

        /// <summary>
        /// Deserialize message from bytes.
        /// </summary>
        public static IpcMessage Deserialize(byte[] data)
        {
            if (data == null || data.Length < 4) return null;

            int length = BitConverter. ToInt32(data, 0);
            if (data.Length < 4 + length) return null;

            string json = Encoding. UTF8.GetString(data, 4, length);
            return JsonSerializer.Deserialize<IpcMessage>(json, JsonOptions);
        }

        /// <summary>
        /// Write message to stream. 
        /// </summary>
        public static void WriteMessage(Stream stream, IpcMessage message)
        {
            byte[] data = Serialize(message);
            stream. Write(data, 0, data.Length);
            stream. Flush();
        }

        /// <summary>
        /// Read message from stream (blocking).
        /// </summary>
        public static IpcMessage ReadMessage(Stream stream)
        {
            // Read length prefix (4 bytes)
            byte[] lengthBytes = new byte[4];
            int bytesRead = 0;
            while (bytesRead < 4)
            {
                int read = stream.Read(lengthBytes, bytesRead, 4 - bytesRead);
                if (read == 0) return null; // Connection closed
                bytesRead += read;
            }

            int length = BitConverter. ToInt32(lengthBytes, 0);
            if (length <= 0 || length > 1024 * 1024) return null; // Max 1MB

            // Read JSON data
            byte[] jsonBytes = new byte[length];
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = stream.Read(jsonBytes, totalRead, length - totalRead);
                if (read == 0) return null; // Connection closed
                totalRead += read;
            }

            string json = Encoding. UTF8.GetString(jsonBytes);
            return JsonSerializer. Deserialize<IpcMessage>(json, JsonOptions);
        }
    }
}