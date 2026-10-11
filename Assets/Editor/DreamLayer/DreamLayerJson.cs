using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DreamLayer.UnityTools
{
    /// <summary>
    /// A small, dependency-free JSON reader. DreamLayer's atlas.json stores <c>frames</c> as an object
    /// keyed by frame name, which Unity's JsonUtility can't read, and a drop-in importer shouldn't
    /// require Newtonsoft. Objects keep their key order (frame order matters).
    /// Values: JsonObject, List&lt;object&gt;, string, double, bool, null.
    /// </summary>
    public static class DreamLayerJson
    {
        public static object Parse(string json)
        {
            int i = 0;
            object value = ParseValue(json, ref i);
            SkipWhitespace(json, ref i);
            if (i != json.Length) throw new FormatException($"Unexpected data after JSON value at {i}.");
            return value;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON.");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') return Expect(s, ref i, "true", true);
            if (c == 'f') return Expect(s, ref i, "false", false);
            if (c == 'n') return Expect(s, ref i, "null", null);
            return ParseNumber(s, ref i);
        }

        static JsonObject ParseObject(string s, ref int i)
        {
            var obj = new JsonObject();
            i++; // {
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException($"Expected ':' at {i}.");
                i++;
                obj.Add(key, ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return obj; }
                throw new FormatException($"Expected ',' or '}}' at {i}.");
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw new FormatException($"Expected ',' or ']' at {i}.");
            }
        }

        static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"Expected string at {i}.");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
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
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new FormatException($"Bad escape '\\{e}' at {i}.");
                }
            }
            throw new FormatException("Unterminated string.");
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw new FormatException($"Unexpected character '{s[i]}' at {i}.");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static object Expect(string s, ref int i, string word, object value)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException($"Expected '{word}' at {i}.");
            i += word.Length;
            return value;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }

    /// <summary>A JSON object that keeps key order, with typed getters.</summary>
    public class JsonObject : List<KeyValuePair<string, object>>
    {
        public void Add(string key, object value) => Add(new KeyValuePair<string, object>(key, value));

        public object Get(string key)
        {
            foreach (var kv in this) if (kv.Key == key) return kv.Value;
            return null;
        }

        public bool Has(string key) => Get(key) != null;
        public string GetString(string key, string fallback = null) => Get(key) as string ?? fallback;
        public double GetNumber(string key, double fallback = 0) => Get(key) is double d ? d : fallback;
        public int GetInt(string key, int fallback = 0) => Get(key) is double d ? (int)Math.Round(d) : fallback;
        public bool GetBool(string key, bool fallback = false) => Get(key) is bool b ? b : fallback;
        public JsonObject GetObject(string key) => Get(key) as JsonObject;
        public List<object> GetArray(string key) => Get(key) as List<object>;
    }
}
