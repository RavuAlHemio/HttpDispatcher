using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RavuAlHemio.HttpDispatcher
{
    public static class HttpDispatcherUtil
    {
        /// <summary>
        /// The <c>ERROR_OPERATION_ABORTED</c> Win32 error code, which is raised if a
        /// <see cref="System.Net.HttpListener"/> is closed while another thread is
        /// waiting for a request.
        /// </summary>
        public const int ErrorOperationAborted = 995;

        /// <summary>
        /// A UTF-8 encoding which doesn't emit Byte Order Marks and throws exceptions on
        /// encoding/decoding failure.
        /// </summary>
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);

        public static byte? DecodeHexDigit(byte b)
        {
            if (b >= '0' && b <= '9')
            {
                return (byte)(b - '0');
            }
            if (b >= 'a' && b <= 'f')
            {
                return (byte)(b - 'a' + 10);
            }
            if (b >= 'A' && b <= 'F')
            {
                return (byte)(b - 'A' + 10);
            }
            return null;
        }

        /// <summary>
        /// URL-decodes the given character sequence, decoding percent-escapes using the
        /// given encoding.
        /// </summary>
        /// <returns>The decoded string.</returns>
        /// <param name="chars">The character sequence to decode.</param>
        /// <param name="encoding">The encoding to use for handling percent-escapes.</param>
        public static string UrlDecode(IEnumerable<char> chars, Encoding encoding)
        {
            var inBytes = encoding.GetBytes(chars.ToArray());
            var bytes = new List<byte>();
            var numer = inBytes.AsEnumerable().GetEnumerator();
            while (numer.MoveNext())
            {
                if (numer.Current == '%')
                {
                    if (!numer.MoveNext())
                    {
                        // "...%"
                        bytes.Add((byte)'%');
                        break;
                    }
                    var topByte = numer.Current;
                    var top = DecodeHexDigit(topByte);
                    if (!top.HasValue)
                    {
                        // "...%z..."
                        bytes.Add((byte)'%');
                        bytes.Add(numer.Current);
                        continue;
                    }
                    if (!numer.MoveNext())
                    {
                        // "...%A"
                        bytes.Add((byte)'%');
                        bytes.Add(topByte);
                        break;
                    }
                    var bottomByte = numer.Current;
                    var bottom = DecodeHexDigit(bottomByte);
                    if (!bottom.HasValue)
                    {
                        // "...%Aw..."
                        bytes.Add((byte)'%');
                        bytes.Add(topByte);
                        bytes.Add(bottomByte);
                        continue;
                    }

                    var unescapedByte = (byte)((top.Value << 4) | bottom.Value);
                    bytes.Add(unescapedByte);
                }
                else
                {
                    bytes.Add(numer.Current);
                }
            }

            return encoding.GetString(bytes.ToArray());
        }

        /// <summary>
        /// URL-decodes the given character sequence, decoding percent-escapes
        /// using UTF-8.
        /// </summary>
        /// <returns>The decoded string.</returns>
        /// <param name="chars">The character sequence to decode.</param>
        public static string UrlDecodeUtf8(IEnumerable<char> chars)
        {
            return UrlDecode(chars, Utf8NoBom);
        }

        /// <summary>
        /// Parses a string containing an <see cref="System.Int32"/>; returns <c>null</c> on failure.
        /// </summary>
        /// <returns>The <see cref="System.Int32"/> on success, or <c>null</c> on failure.</returns>
        /// <param name="text">The string to parse.</param>
        public static int? ParseIntOrNull(string text)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }
            return value;
        }

        /// <summary>
        /// Parses a string containing an <see cref="System.Int64"/>; returns <c>null</c> on failure.
        /// </summary>
        /// <returns>The <see cref="System.Int64"/> on success, or <c>null</c> on failure.</returns>
        /// <param name="text">The string to parse.</param>
        public static long? ParseLongOrNull(string text)
        {
            long value;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }
            return value;
        }

        /// <summary>
        /// Parses a string containing a <see cref="System.Single"/>; returns <c>null</c> on failure.
        /// </summary>
        /// <returns>The <see cref="System.Single"/> on success, or <c>null</c> on failure.</returns>
        /// <param name="text">The string to parse.</param>
        public static float? ParseFloatOrNull(string text)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }
            return value;
        }

        /// <summary>
        /// Parses a string containing a <see cref="System.Double"/>; returns <c>null</c> on failure.
        /// </summary>
        /// <returns>The <see cref="System.Double"/> on success, or <c>null</c> on failure.</returns>
        /// <param name="text">The string to parse.</param>
        public static double? ParseDoubleOrNull(string text)
        {
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }
            return value;
        }

        /// <summary>
        /// Parses a string containing an <see cref="System.Decimal"/>; returns <c>null</c> on failure.
        /// </summary>
        /// <returns>The <see cref="System.Decimal"/> on success, or <c>null</c> on failure.</returns>
        /// <param name="text">The string to parse.</param>
        public static decimal? ParseDecimalOrNull(string text)
        {
            decimal value;
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }
            return value;
        }
    }
}

