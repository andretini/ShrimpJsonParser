using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// Supports: objects, arrays, strings, numbers, booleans, and null.
public static class SimpleJson
{
    /// Entry point to parse a JSON string into C# objects.
    /// Returns:
    ///  - Dictionary<string,object> for JSON objects
    ///  - List<object> for JSON arrays
    ///  - string, long, decimal/double, bool, or null for primitive values
    public static object Parse(string json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));

        var p = new Parser(json);
        var value = p.ParseValue();

        // After parsing the value, we skip any trailing whitespace
        p.SkipWs();

        // If we didn’t reach the end, it means there is garbage after valid JSON
        if (!p.End) p.Error("Trailing characters after valid JSON value");

        return value;
    }

    // ---------------- INTERNAL PARSER CLASS ----------------
    private sealed class Parser
    {
        private readonly string _s; // JSON text
        private int _i;             // current position in the string

        public Parser(string s) { _s = s; _i = 0; }

        public bool End => _i >= _s.Length;

        
        /// Skip whitespace (spaces, tabs, newlines) between tokens.
            public void SkipWs()
        {
            while (!End)
            {
                char c = _s[_i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _i++;
                else break;
            }
        }

        
        /// Parse a JSON value (object, array, string, number, true, false, null).
            public object ParseValue()
        {
            SkipWs();
            if (End) Error("Unexpected end of input while expecting a value");

            char c = _s[_i];

            switch (c)
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return ParseString();
                case 't': return ParseLiteral("true", true);
                case 'f': return ParseLiteral("false", false);
                case 'n': return ParseLiteral("null", null);
                default:
                    if (c == '-' || char.IsDigit(c))
                        return ParseNumber();

                    Error($"Unexpected character '{c}' while parsing a value");
                    return null; // unreachable
            }
        }

        
        /// Match specific literal text like "true", "false", "null".
            private object ParseLiteral(string literal, object value)
        {
            Expect(literal);
            return value!;
        }

        
        /// Parse a JSON object: { "key": value, ... }
            private Dictionary<string, object> ParseObject()
        {
            var obj = new Dictionary<string, object>();

            Expect('{');
            SkipWs();

            // Empty object {}
            if (TryConsume('}')) return obj;

            // Loop over key:value pairs
            while (true)
            {
                SkipWs();
                if (!PeekIs('"')) Error("Object keys must be strings starting with '\"'");
                string key = ParseString();

                SkipWs();
                Expect(':'); // colon between key and value

                object val = ParseValue();
                obj[key] = val;

                SkipWs();
                if (TryConsume('}')) break; // end of object
                Expect(',');                 // otherwise expect comma for next pair
            }

            return obj;
        }

        
        /// Parse a JSON array: [ value, value, ... ]
            private List<object> ParseArray()
        {
            var arr = new List<object>();

            Expect('[');
            SkipWs();

            // Empty array []
            if (TryConsume(']')) return arr;

            // Loop over elements
            while (true)
            {
                arr.Add(ParseValue());

                SkipWs();
                if (TryConsume(']')) break; // end of array
                Expect(',');                 // otherwise expect comma for next value
            }

            return arr;
        }

        
        /// Parse a JSON string: "some text"
        /// Handles escape sequences like \" \n \uXXXX
            private string ParseString()
        {
            Expect('"');
            var sb = new StringBuilder();

            while (!End)
            {
                char c = _s[_i++];

                if (c == '"') return sb.ToString(); // end of string

                if (c == '\\')
                {
                    if (End) Error("Unterminated escape sequence in string");
                    char e = _s[_i++];

                    // Handle escape characters
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            sb.Append(ParseUnicodeEscape());
                            break;
                        default:
                            Error($"Invalid escape character '\\{e}'");
                            break;
                    }
                }
                else
                {
                    if (c <= 0x1F) Error("Unescaped control character in string");
                    sb.Append(c);
                }
            }

            Error("Unterminated string literal");
            return ""; // never reached
        }

        
        /// Handle \uXXXX unicode escapes.
        /// (Basic support, enough for most JSON)
            private char ParseUnicodeEscape()
        {
            int codeUnit = ParseHex4();

            // Basic Multilingual Plane char
            return (char)codeUnit;
        }

        
        /// Read 4 hex digits after \u
            private int ParseHex4()
        {
            if (_i + 4 > _s.Length) Error("Incomplete \\u escape");

            int val = 0;
            for (int k = 0; k < 4; k++)
            {
                char h = _s[_i++];
                int d =
                    (h >= '0' && h <= '9') ? (h - '0') :
                    (h >= 'a' && h <= 'f') ? (h - 'a' + 10) :
                    (h >= 'A' && h <= 'F') ? (h - 'A' + 10) : -1;

                if (d < 0) Error("Invalid hex digit in \\u escape");
                val = (val << 4) | d;
            }
            return val;
        }

        
        /// Parse a JSON number (int, decimal, scientific notation).
            private object ParseNumber()
        {
            int start = _i;

            if (TryConsume('-')) { } // optional minus

            // integer part
            if (TryConsume('0'))
            {
                // leading zero must not be followed by other digits
                if (!End && char.IsDigit(Peek())) Error("Numbers with leading zero are invalid");
            }
            else
            {
                if (!ConsumeDigits()) Error("Expected digits");
            }

            bool isFloat = false;

            // decimal part
            if (TryConsume('.'))
            {
                isFloat = true;
                if (!ConsumeDigits()) Error("Expected digits after decimal point");
            }

            // exponent part
            if (TryConsume('e') || TryConsume('E'))
            {
                isFloat = true;
                if (TryConsume('+') || TryConsume('-')) { }
                if (!ConsumeDigits()) Error("Expected digits in exponent");
            }

            string slice = _s.Substring(start, _i - start);

            // convert to numeric type
            if (!isFloat && long.TryParse(slice, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                return l;

            if (decimal.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec))
                return dec;

            return double.Parse(slice, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private bool ConsumeDigits()
        {
            int start = _i;
            while (!End && char.IsDigit(_s[_i])) _i++;
            return _i > start;
        }

        private bool PeekIs(char c) => !End && _s[_i] == c;
        private char Peek() => _s[_i];

        private bool TryConsume(char c)
        {
            if (!End && _s[_i] == c) { _i++; return true; }
            return false;
        }

        private void Expect(char c)
        {
            SkipWs();
            if (End || _s[_i] != c) Error($"Expected '{c}'");
            _i++;
        }

        private void Expect(string s)
        {
            SkipWs();
            for (int k = 0; k < s.Length; k++)
            {
                if (End || _s[_i++] != s[k]) Error($"Expected \"{s}\"");
            }
        }

        
        /// Throw a parsing error with line/column info.
            public void Error(string msg)
        {
            int line = 1, col = 1;
            for (int k = 0; k < _i; k++)
            {
                if (_s[k] == '\n') { line++; col = 1; }
                else col++;
            }
            throw new JsonParseException($"{msg} at line {line}, col {col}");
        }
    }
}


/// Custom exception for JSON parsing errors.
/// </summary>
public sealed class JsonParseException : Exception
{
    public JsonParseException(string message) : base(message) { }
}
