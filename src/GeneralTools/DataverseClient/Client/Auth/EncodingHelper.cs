using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth
{
    /// <summary>
    /// The encoding helper.
    /// </summary>
    internal static class EncodingHelper
    {
        private const int MaxUrlEncodingSize = 2000;

        /// <summary>
        /// URL encode the given string.
        /// </summary>
        /// <param name="message">String to URL encode</param>
        /// <returns>URL encoded string</returns>
        /// <remarks>This method encodes the space ' ' character as "+" rather than "%20".</remarks>
        public static string UrlEncode(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            if (message.Length < MaxUrlEncodingSize)
            {
                // This optimization is done for most common scenarios where the message length will not exceed MaxUrlEncodingSize
                message = Uri.EscapeDataString(message);
                message = message.Replace("%20", "+");
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                int loops = message.Length / MaxUrlEncodingSize;

                for (int i = 0; i <= loops; i++)
                {
                    if (i < loops)
                    {
                        sb.Append(Uri.EscapeDataString(message.Substring(MaxUrlEncodingSize * i, MaxUrlEncodingSize)));
                    }
                    else
                    {
                        sb.Append(Uri.EscapeDataString(message.Substring(MaxUrlEncodingSize * i)));
                    }
                }

                message = sb.ToString();
                message = message.Replace("%20", "+");
            }

            return message;
        }

        /// <summary>
        /// Decode the given URL encoded string.
        /// </summary>
        /// <param name="message">URL encoded string to decode</param>
        /// <returns>Decoded string</returns>
        /// <remarks>This method decodes "+" (as well as "%20") into the space character ' '.</remarks>
        public static string UrlDecode(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            message = message.Replace("+", "%20");
            message = Uri.UnescapeDataString(message);

            return message;
        }

        public static void AddKeyValueString(StringBuilder messageBuilder, string key, string value)
        {
            AddKeyValueString(messageBuilder, key, value.ToCharArray());
        }

        /// <summary>
        /// Parse a delimited string of key-value pairs in to a dictionary.
        /// </summary>
        /// <param name="input">Delimited string of key-value pairs</param>
        /// <param name="delimiter">Character used as a delimiter between key-value pairs</param>
        /// <param name="urlDecode">True to perform URL decoding of both the keys and values</param>
        /// <param name="lowercaseKeys">True to make all resulting keys lower-case</param>
        /// <returns>Dictionary of string key-value pairs</returns>
        public static Dictionary<string, string> ParseKeyValueList(string input, char delimiter, bool urlDecode, bool lowercaseKeys)
        {
            return ParseKeyValueList(input, delimiter, urlDecode, lowercaseKeys, strict: false);
        }

        /// <summary>
        /// Parse a delimited string of key-value pairs in to a dictionary.
        /// </summary>
        /// <param name="input">Delimited string of key-value pairs</param>
        /// <param name="delimiter">Character used as a delimiter between key-value pairs</param>
        /// <param name="urlDecode">True to perform URL decoding of both the keys and values</param>
        /// <param name="lowercaseKeys">True to make all resulting keys lower-case</param>
        /// <exception cref="ArgumentException">Thrown if a malformed key-value pair is present in <paramref name="input"/></exception>
        /// <returns>Dictionary of string key-value pairs</returns>
        public static Dictionary<string, string> ParseKeyValueListStrict(string input, char delimiter, bool urlDecode, bool lowercaseKeys)
        {
            return ParseKeyValueList(input, delimiter, urlDecode, lowercaseKeys, strict: true);
        }

        /// <summary>
        /// Parse a delimited string of key-value pairs in to a dictionary.
        /// </summary>
        /// <param name="input">Delimited string of key-value pairs</param>
        /// <param name="delimiter">Character used as a delimiter between key-value pairs</param>
        /// <param name="urlDecode">True to perform URL decoding of both the keys and values</param>
        /// <param name="lowercaseKeys">True to make all resulting keys lower-case</param>
        /// <param name="strict">Throw <see cref="ArgumentException"/> when the input string contains a malformed key-value pair</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="strict"/> is true and a malformed key-value pair is present in <paramref name="input"/></exception>
        /// <returns>Dictionary of string key-value pairs</returns>
        private static Dictionary<string, string> ParseKeyValueList(string input, char delimiter, bool urlDecode, bool lowercaseKeys,
            bool strict)
        {
            var response = new Dictionary<string, string>();

            List<string> queryPairs = SplitWithQuotes(input, delimiter);

            foreach (string queryPair in queryPairs)
            {
                List<string> pair = SplitWithQuotes(queryPair, '=');

                if (pair.Count == 2 && !string.IsNullOrWhiteSpace(pair[0]) && !string.IsNullOrWhiteSpace(pair[1]))
                {
                    string key = pair[0];
                    string value = pair[1];

                    // Url decoding is needed for parsing OAuth response, but not for parsing WWW-Authenticate header in 401 challenge
                    if (urlDecode)
                    {
                        key = UrlDecode(key);
                        value = UrlDecode(value);
                    }

                    if (lowercaseKeys)
                    {
                        key = key.Trim().ToLowerInvariant();
                    }

                    value = value.Trim().Trim(new[] { '\"' }).Trim();

                    response[key] = value;
                }
                else if (strict && pair.Count > 2)
                {
                    throw new ArgumentException(nameof(input));
                }
            }

            return response;
        }

        /// <summary>
        /// Parse a delimited string of key-value pairs in to a dictionary.
        /// </summary>
        /// <param name="input">Delimited string of key-value pairs</param>
        /// <param name="delimiter">Character used as a delimiter between key-value pairs</param>
        /// <param name="urlDecode">True to perform URL decoding of both the keys and values</param>
        /// <remarks>Keys are forced to lower-cased</remarks>
        /// <returns>Dictionary of string key-value pairs</returns>
        public static Dictionary<string, string> ParseKeyValueList(string input, char delimiter, bool urlDecode)
        {
            return ParseKeyValueList(input, delimiter, urlDecode, true, strict: false);
        }

        /// <summary>
        /// Create a <see cref="Stream"/> from the given string.
        /// </summary>
        /// <param name="s">String to create a <see cref="Stream"/> from</param>
        /// <returns><see cref="Stream"/> from a string</returns>
        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Deserialize the given JSON string in to the specified type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">Type to deserialize the JSON as</typeparam>
        /// <param name="response">JSON string</param>
        /// <returns>Deserialized type <typeparamref name="T"/></returns>
        public static T DeserializeResponse<T>(string response)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));

            if (response == null)
            {
                return default(T);
            }

            using (Stream stream = GenerateStreamFromString(response))
            {
                return ((T)serializer.ReadObject(stream));
            }
        }

        /// <summary>
        /// Base64 encode the given string.
        /// </summary>
        /// <param name="input">String to base64 encode</param>
        /// <returns>Base64 encoded string</returns>
        internal static string Base64Encode(string input)
        {
            string encodedString = String.Empty;
            if (!String.IsNullOrEmpty(input))
            {
                encodedString = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
            }

            return encodedString;
        }

        /// <summary>
        /// Decode the given base64 encoded string.
        /// </summary>
        /// <param name="encodedString">Base64 encoded string</param>
        /// <returns>Decoded string</returns>
        internal static string Base64Decode(string encodedString)
        {
            string output = null;
            if (!String.IsNullOrEmpty(encodedString))
            {
                byte[] outputBytes = Convert.FromBase64String(encodedString);
                output = Encoding.UTF8.GetString(outputBytes, 0, outputBytes.Length);
            }

            return output;
        }

        internal static char[] UrlEncode(char[] message)
        {
            if (message == null)
            {
                return null;
            }

            var encodedMessage = new char[message.Length * 2];
            int length = 0;
            var singleChar = new char[1];
            foreach (char ch in message)
            {
                singleChar[0] = ch;
                var str = new string(singleChar);
                string encodedStr = UrlEncode(str);
                char[] encodedSingleChar = encodedStr.ToCharArray();
                if (length + encodedSingleChar.Length > encodedMessage.Length)
                {
                    Array.Resize(ref encodedMessage, encodedMessage.Length + message.Length * 2);
                }

                encodedSingleChar.CopyTo(encodedMessage, length);
                length += encodedSingleChar.Length;
            }

            Array.Resize(ref encodedMessage, length);
            return encodedMessage;
        }

        /// <summary>
        /// Split a string into individual elements by the specified delimiter, where
        /// a delimiter enclosed within double-quotes '"' is considered to be part of the same
        /// single element.
        /// </summary>
        /// <param name="input">Delimited string</param>
        /// <param name="delimiter">Element delimiter</param>
        /// <returns>List of elements</returns>
        internal static List<string> SplitWithQuotes(string input, char delimiter)
        {
            List<string> items = new List<string>();

            if (string.IsNullOrWhiteSpace(input))
            {
                return items;
            }

            int startIndex = 0;
            bool insideString = false;
            string item;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == delimiter && !insideString)
                {
                    item = input.Substring(startIndex, i - startIndex);
                    if (!string.IsNullOrWhiteSpace(item.Trim()))
                    {
                        items.Add(item);
                    }

                    startIndex = i + 1;
                }
                else if (input[i] == '"')
                {
                    insideString = !insideString;
                }
            }

            item = input.Substring(startIndex);
            if (!string.IsNullOrWhiteSpace(item.Trim()))
            {
                items.Add(item);
            }

            return items;
        }

        internal static void AddKeyValueString(StringBuilder messageBuilder, string key, char[] value)
        {
            string delimiter = (messageBuilder.Length == 0) ? string.Empty : "&";
            messageBuilder.AppendFormat(CultureInfo.CurrentCulture, "{0}{1}=", delimiter, key);
            messageBuilder.Append(value);
        }

        internal static string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }
    }
}
