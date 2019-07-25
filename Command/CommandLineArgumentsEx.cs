using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Woof.Command {

    /// <summary>
    /// Command line arguments processing class.
    /// </summary>
    public class CommandLineArgumentsEx {
        
        /// <summary>
        /// Gets a value indicating whether the command line is empty.
        /// </summary>
        public bool IsEmpty { get; }

        /// <summary>
        /// Gets the original raw arguments.
        /// </summary>
        public string[] Raw { get; }

        /// <summary>
        /// Gets all parameters (arguments not prefixed with a switch).
        /// </summary>
        public CommandLinePositionalArgumentCollection Positional { get; }

        /// <summary>
        /// Gets all switches.
        /// </summary>
        public CommandLineSwitchCollection Switches { get; }

        /// <summary>
        /// Gets all options (arguments prefixed with a switch).
        /// </summary>
        public CommandLineOptionCollection Options { get; }

        /// <summary>
        /// Parses command line arguments into positional arguments, switches and options.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <param name="markedAsOptions">Optional switches to be treated as options separated with '|', ',' or ' ' character.</param>
        public CommandLineArgumentsEx(IEnumerable<string> args = null, string markedAsOptions = null) {
            Raw = args?.ToArray() ?? new string[] { };
            string[] marked = markedAsOptions != null ? markedAsOptions.Split('|', ',', ' ') : new string[] { };
            IsEmpty = !Raw.Any();
            var e = (Raw as IEnumerable<string>).GetEnumerator();
            var positional = new List<string>();
            var switches = new List<string>();
            var options = new Dictionary<string, string>();
            while (e.MoveNext()) {
                var match = RxElement.Match(e.Current);
                if (match.Success) {
                    var key = match.Groups[1].Value;
                    if (marked.Any() && marked.Contains(key, StringComparer.Ordinal)) {
                        if (e.MoveNext() && !options.ContainsKey(key)) options.Add(key, e.Current);
                    }
                    else switches.Add(key);
                }
                else positional.Add(e.Current);
            }
            Positional = new CommandLinePositionalArgumentCollection(positional);
            Switches = new CommandLineSwitchCollection(switches);
            Options = new CommandLineOptionCollection(options);
        }

        /// <summary>
        /// Matches element with or without optional switch.
        /// </summary>
        private static readonly Regex RxElement = new Regex(@"^(?:-|--|/)(.*)$", RegexOptions.Compiled);

    }

    /// <summary>
    /// A collection containing positional command line arguments.
    /// Can be queried without range checking.
    /// </summary>
    public class CommandLinePositionalArgumentCollection : IEnumerable<string> {

        /// <summary>
        /// Command line arguments.
        /// </summary>
        public readonly string[] Arguments;

        /// <summary>
        /// Creates new collection from any collection.
        /// </summary>
        /// <param name="values">Initial collection.</param>
        public CommandLinePositionalArgumentCollection(IEnumerable<string> values) => Arguments = values.ToArray();

        /// <summary>
        /// Returns the command line argument selected by the index or null if such element doesn't exist.
        /// </summary>
        /// <param name="index">Zero based index of the positional argument.</param>
        /// <returns>Argument value or null.</returns>
        public string this[int index] => index >= 0 && index < Arguments.Length ? Arguments[index] : null;

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)Arguments).GetEnumerator();

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<string>)Arguments).GetEnumerator();

    }

    /// <summary>
    /// A collection containing command line switches.
    /// </summary>
    public class  CommandLineSwitchCollection : IEnumerable<string> {

        /// <summary>
        /// Command line switches.
        /// </summary>
        private readonly string[] Switches;

        /// <summary>
        /// Creates new collection from any collection.
        /// </summary>
        /// <param name="values">Initial collection.</param>
        public CommandLineSwitchCollection(IEnumerable<string> values) => Switches = values.ToArray();

        /// <summary>
        /// Returns a value indicating whether the collection contain the switch with specified name or alias.
        /// </summary>
        /// <param name="name">Name of the switch, or name aliases separated with '|' character.</param>
        /// <returns></returns>
        public bool this[string name] => !Switches.Any() ? false : (
            name.Contains('|')
            ? name.Split('|').Any(i => Switches.Contains(i, StringComparer.Ordinal))
            : Switches.Contains(name, StringComparer.Ordinal)
        );

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)Switches).GetEnumerator();

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<string>)Switches).GetEnumerator();

    }

    /// <summary>
    /// A collection containing command line arguments accessible by name or alias.
    /// </summary>
    public class CommandLineOptionCollection : IEnumerable<KeyValuePair<string, string>> {

        /// <summary>
        /// Command line options.
        /// </summary>
        private readonly Dictionary<string, string> Options;

        /// <summary>
        /// Creates new collection from a dictionary.
        /// </summary>
        /// <param name="values">Initial dictionary.</param>
        public CommandLineOptionCollection(Dictionary<string, string> values) => Options = values;

        /// <summary>
        /// Gets the option value with the specified name or alias in the <see cref="CommandLineOptionCollection"/>.
        /// </summary>
        /// <param name="name">Name of the option, or name aliases separated with '|' character.</param>
        /// <returns>Option value, null if it doesn't exist.</returns>
        public string this[string name] => !Options.Any() ? null : (
                name.Contains('|')
                    ? name.Split('|').Where(i => Options.ContainsKey(i)).Select(i => Options[i]).FirstOrDefault()
                    : (Options.ContainsKey(name) ? Options[name] : null)
            );

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, string>>)Options).GetEnumerator();

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<string, string>>)Options).GetEnumerator();

    }

}