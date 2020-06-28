using System.Collections;
using System.Collections.Generic;

namespace ChessBot.Uci
{
    class Options : IEnumerable<Option>
    {
        private readonly Dictionary<string, Option> _dict = new Dictionary<string, Option>();

        public Options()
        {
            var optionsArray = new[]
            {
                new Option("Hash", "spin", 1, 1, 128)
            };

            foreach (var option in optionsArray)
            {
                _dict.Add(option.Name, option);
            }
        }

        public Option this[string name] => _dict[name];

        public T Get<T>(string name) => (T)_dict[name].Value;

        public bool TrySet(string name, string valueText)
        {
            if (!_dict.TryGetValue(name, out var option) || !option.TryParse(valueText, out object value))
            {
                return false;
            }

            option.Value = value;
            return true;
        }

        public IEnumerator<Option> GetEnumerator() => _dict.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
