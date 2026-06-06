// Minimal SimpleJSON subset for Luban cs-simple-json generated code.
// Full library: https://github.com/Bunny83/SimpleJSON

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SimpleJSON
{
    public enum JSONNodeType
    {
        None = 0,
        Null = 1,
        String = 2,
        Number = 3,
        Object = 4,
        Array = 5,
        Bool = 6,
        NullValue = 7,
    }

    public abstract class JSONNode
    {
        public abstract JSONNodeType Tag { get; }

        public virtual JSONNode this[string key]
        {
            get { return JSONNull.Instance; }
            set { }
        }

        public virtual JSONNode this[int index]
        {
            get { return JSONNull.Instance; }
            set { }
        }

        public virtual int Count => 0;

        public virtual IEnumerable<JSONNode> Children
        {
            get { yield break; }
        }

        public virtual bool IsNumber => Tag == JSONNodeType.Number;
        public virtual bool IsString => Tag == JSONNodeType.String;
        public virtual bool IsObject => Tag == JSONNodeType.Object;
        public virtual bool IsArray => Tag == JSONNodeType.Array;
        public virtual bool IsBoolean => Tag == JSONNodeType.Bool;

        public virtual double AsDouble
        {
            get { return 0; }
        }

        public virtual int AsInt => (int)AsDouble;

        public virtual float AsFloat => (float)AsDouble;

        public virtual string Value
        {
            get { return string.Empty; }
        }

        public static implicit operator int(JSONNode node)
        {
            return node != null ? node.AsInt : 0;
        }

        public static implicit operator float(JSONNode node)
        {
            return node != null ? node.AsFloat : 0f;
        }

        public static implicit operator double(JSONNode node)
        {
            return node != null ? node.AsDouble : 0;
        }

        public static implicit operator string(JSONNode node)
        {
            return node != null ? node.Value : string.Empty;
        }

        public static implicit operator bool(JSONNode node)
        {
            return node != null && node.AsDouble != 0;
        }

        public static JSONNode Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return JSONNull.Instance;
            }

            return JSONParser.Parse(json);
        }
    }

    sealed class JSONNull : JSONNode
    {
        public static readonly JSONNull Instance = new JSONNull();

        public override JSONNodeType Tag => JSONNodeType.NullValue;
    }

    sealed class JSONStringNode : JSONNode
    {
        readonly string value;

        public JSONStringNode(string value)
        {
            this.value = value ?? string.Empty;
        }

        public override JSONNodeType Tag => JSONNodeType.String;

        public override string Value => this.value;
    }

    sealed class JSONNumberNode : JSONNode
    {
        readonly double value;

        public JSONNumberNode(double value)
        {
            this.value = value;
        }

        public override JSONNodeType Tag => JSONNodeType.Number;

        public override double AsDouble => this.value;
    }

    sealed class JSONBoolNode : JSONNode
    {
        readonly bool value;

        public JSONBoolNode(bool value)
        {
            this.value = value;
        }

        public override JSONNodeType Tag => JSONNodeType.Bool;

        public override double AsDouble => this.value ? 1 : 0;
    }

    sealed class JSONArrayNode : JSONNode
    {
        readonly List<JSONNode> items = new List<JSONNode>();

        public override JSONNodeType Tag => JSONNodeType.Array;

        public override int Count => this.items.Count;

        public override IEnumerable<JSONNode> Children => this.items;

        public override JSONNode this[int index] => this.items[index];

        public void Add(JSONNode node)
        {
            this.items.Add(node);
        }
    }

    sealed class JSONObjectNode : JSONNode
    {
        readonly Dictionary<string, JSONNode> map = new Dictionary<string, JSONNode>();

        public override JSONNodeType Tag => JSONNodeType.Object;

        public override JSONNode this[string key]
        {
            get
            {
                if (this.map.TryGetValue(key, out JSONNode node))
                {
                    return node;
                }

                return JSONNull.Instance;
            }
        }

        public void Add(string key, JSONNode node)
        {
            this.map[key] = node;
        }
    }

    static class JSONParser
    {
        static string text;
        static int index;

        public static JSONNode Parse(string json)
        {
            text = json;
            index = 0;
            SkipWhite();
            JSONNode node = ReadValue();
            SkipWhite();
            return node ?? JSONNull.Instance;
        }

        static JSONNode ReadValue()
        {
            SkipWhite();
            if (index >= text.Length)
            {
                return JSONNull.Instance;
            }

            char c = text[index];
            if (c == '{')
            {
                return ReadObject();
            }

            if (c == '[')
            {
                return ReadArray();
            }

            if (c == '"')
            {
                return new JSONStringNode(ReadString());
            }

            if (c == 't' || c == 'f')
            {
                return new JSONBoolNode(ReadBool());
            }

            if (c == 'n')
            {
                ReadNull();
                return JSONNull.Instance;
            }

            return new JSONNumberNode(ReadNumber());
        }

        static JSONObjectNode ReadObject()
        {
            JSONObjectNode obj = new JSONObjectNode();
            index++;
            SkipWhite();
            if (TryConsume('}'))
            {
                return obj;
            }

            while (index < text.Length)
            {
                SkipWhite();
                string key = ReadString();
                SkipWhite();
                Expect(':');
                SkipWhite();
                obj.Add(key, ReadValue());
                SkipWhite();
                if (TryConsume('}'))
                {
                    return obj;
                }

                Expect(',');
            }

            return obj;
        }

        static JSONArrayNode ReadArray()
        {
            JSONArrayNode arr = new JSONArrayNode();
            index++;
            SkipWhite();
            if (TryConsume(']'))
            {
                return arr;
            }

            while (index < text.Length)
            {
                SkipWhite();
                arr.Add(ReadValue());
                SkipWhite();
                if (TryConsume(']'))
                {
                    return arr;
                }

                Expect(',');
            }

            return arr;
        }

        static string ReadString()
        {
            Expect('"');
            StringBuilder sb = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c == '\\' && index < text.Length)
                {
                    char esc = text[index++];
                    switch (esc)
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
                            if (index + 3 < text.Length)
                            {
                                string hex = text.Substring(index, 4);
                                index += 4;
                                sb.Append((char)int.Parse(hex, NumberStyles.HexNumber));
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        static double ReadNumber()
        {
            int start = index;
            if (text[index] == '-')
            {
                index++;
            }

            while (index < text.Length && char.IsDigit(text[index]))
            {
                index++;
            }

            if (index < text.Length && text[index] == '.')
            {
                index++;
                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
            {
                index++;
                if (index < text.Length && (text[index] == '+' || text[index] == '-'))
                {
                    index++;
                }

                while (index < text.Length && char.IsDigit(text[index]))
                {
                    index++;
                }
            }

            string num = text.Substring(start, index - start);
            return double.Parse(num, CultureInfo.InvariantCulture);
        }

        static bool ReadBool()
        {
            if (Match("true"))
            {
                index += 4;
                return true;
            }

            if (Match("false"))
            {
                index += 5;
                return false;
            }

            return false;
        }

        static void ReadNull()
        {
            Match("null");
            index += 4;
        }

        static void SkipWhite()
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }

        static bool TryConsume(char c)
        {
            if (index < text.Length && text[index] == c)
            {
                index++;
                return true;
            }

            return false;
        }

        static void Expect(char c)
        {
            if (!TryConsume(c))
            {
                throw new FormatException("JSON parse error at " + index + ", expected '" + c + "'");
            }
        }

        static bool Match(string s)
        {
            return index + s.Length <= text.Length && string.Compare(text, index, s, 0, s.Length, StringComparison.Ordinal) == 0;
        }
    }
}
