using System;

namespace ChessBot.Uci
{
    class Option
    {
        public Option(string name, string type, object defaultValue, object min = null, object max = null)
        {
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
            Min = min;
            Max = max;

            Value = defaultValue;
        }

        public string Name { get; }
        public string Type { get; }
        public object DefaultValue { get; }
        public object Min { get; }
        public object Max { get; }

        public object Value { get; set; }

        public bool TryParse(string valueText, out object value)
        {
            bool result;
            switch (Type)
            {
                case "check":
                    result = bool.TryParse(valueText, out bool boolValue);
                    value = boolValue;
                    break;
                case "spin":
                    result = int.TryParse(valueText, out int intValue) && (intValue >= (int)Min && intValue <= (int)Max);
                    value = intValue;
                    break;
                case "string":
                    result = true;
                    value = valueText;
                    break;
                case "combo":
                case "button":
                default:
                    throw new NotImplementedException();
            }

            return result;
        }
    }
}
