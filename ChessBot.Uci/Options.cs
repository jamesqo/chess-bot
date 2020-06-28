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

        public Option this[string optionName] => _dict[optionName];

        public IEnumerator<Option> GetEnumerator() => _dict.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
