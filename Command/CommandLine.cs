using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Woof.Command {

    /// <summary>
    /// Optionally interactive command line processor / console renderer.
    /// </summary>
    public class CommandLine {

        #region Properties

        /// <summary>
        /// Gets the command arguments processed.
        /// </summary>
        public CommandLineArgumentsEx Arguments { get; private set; }

        /// <summary>
        /// Gets or sets the display console color of the command line arguments.
        /// </summary>
        public ConsoleColor ArgumentsColor { get; set; } = ConsoleColor.White;

        /// <summary>
        /// Gets the exact command alone, without arguments.
        /// </summary>
        public string Command { get; private set; }

        /// <summary>
        /// Gets or sets the display console color of the command line command element.
        /// </summary>
        public ConsoleColor CommandColor { get; set; } = ConsoleColor.Yellow;

        /// <summary>
        /// Gets the index of the part pointed with the current cursor position.
        /// </summary>
        public int CurrentPartIndex { get; private set; }

        /// <summary>
        /// Gets the line offset of the part pointed with the current cursor position.
        /// </summary>
        public int CurrentPartOffset { get; private set; }

        /// <summary>
        /// Gets the length of the part pointed with the current cursor position.
        /// </summary>
        public int CurrentPartLength { get; private set; }

        /// <summary>
        /// Gets or sets (replaces) the unquoted part pointed with the current cursor position.
        /// </summary>
        public string CurrentPart {
            get => (CurrentPartIndex >= 0 && CurrentPartOffset >= 0 && CurrentPartLength > 0) ? _Text.Substring(CurrentPartOffset, CurrentPartLength) : null;
            set {
                if (value == null) return;
                if (CurrentPartIndex >= 0 && CurrentPartOffset >= 0 && CurrentPartLength > 0) {
                    var quoted = Quote(value);
                    int offset = CurrentPartOffset, length = quoted.Length;
                    Text = _Text.Remove(CurrentPartOffset, CurrentPartLength).Insert(CurrentPartOffset, quoted);
                    Cursor = offset + length;
                }
            }
        }

        /// <summary>
        /// Gets or sets a position of the cursor that is used to point the parts within the command line.
        /// </summary>
        public int Cursor {
            get => _Cursor;
            set {
                _Cursor = value;
                CurrentPartIndex = -1;
                CurrentPartOffset = -1;
                CurrentPartLength = -1;
                if (_Cursor < 0 || _Cursor > _Text.Length || _Text.Length < 1) return;
                var mapIndex = _Cursor < Text.Length ? _Cursor : _Text.Length - 1;
                CurrentPartIndex = _Map[mapIndex];
                CurrentPartLength = 0;
                for (int i = 0, n = _Map.Length; i < n; i++) {
                    if (_Map[i] == CurrentPartIndex) {
                        if (CurrentPartOffset < 0) { CurrentPartOffset = i; }
                        CurrentPartLength++;
                    }
                }
                while (CurrentPartOffset + CurrentPartLength > _Text.Length) CurrentPartLength--;
            }
        }

        /// <summary>
        /// Gets the current command line length.
        /// </summary>
        public int Length => _Text.Length;

        /// <summary>
        /// Gets or sets a value indicating whether the line input should work in overtype mode.
        /// </summary>
        public bool IsOvertype {
            get => _IsOvertype;
            set {
                _IsOvertype = value;
                Console.CursorSize = value ? 100 : 10;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this command line is rendered to the console.
        /// </summary>
        public bool IsRendered { get; private set; }

        /// <summary>
        /// Gets or sets command line text.
        /// </summary>
        public string Text {
            get => _Text;
            set {
                _Text = value;
                Parse();
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new empty command line.
        /// </summary>
        public CommandLine() => Empty();

        /// <summary>
        /// Creates a new command line from string.
        /// </summary>
        /// <param name="line">Source command line.</param>
        public CommandLine(string line) => Text = line;

        #endregion

        #region Cursor position

        /// <summary>
        /// Moves the cursor to the beginning of the command line.
        /// </summary>
        /// <param name="update">True if the console cursor should be updated with the virtual cursor.</param>
        public void Home(bool update = true) {
            Cursor = 0;
            if (update) UpdateCursor();
        }

        /// <summary>
        /// Moves the cursor to previous part or beginning of the current part.
        /// </summary>
        /// <param name="update">True if the console cursor should be updated with the virtual cursor.</param>
        public void Prev(bool update = true) {
            if (Cursor > 0) {
                int i = _Cursor;
                if (i > 0 && _Text[i - 1] == ' ') Cursor = (--i);
                for (; i >= 0; i--) if (GetPartIndex(i) != CurrentPartIndex) break;
                Cursor = ++i;
                if (update) UpdateCursor();
            }
        }

        /// <summary>
        /// Moves the cursor one character left if applicable.
        /// </summary>
        /// <param name="update">True if the console cursor should be updated with the virtual cursor.</param>
        public void Left(bool update = true) {
            if (Cursor > 0) {
                Cursor--;
                if (update) UpdateCursor();
            }
        }

        /// <summary>
        /// Moves the cursor one character right if applicable.
        /// </summary>
        /// <param name="update">True if the console cursor should be updated with the virtual cursor.</param>
        public void Right(bool update = true) {
            if (Cursor < _Text.Length) {
                Cursor++;
                if (update) UpdateCursor();
            }
        }

        /// <summary>
        /// Moves the cursor to the next part or the line end.
        /// </summary>
        /// <param name="update">True if the console cursor should be updated with the virtual cursor.</param>
        public void Next(bool update = true) {
            if (Cursor < _Text.Length) {
                int i = _Cursor;
                if (i < _Text.Length && _Text[i] == ' ') Cursor = (++i);
                for (; i <= _Text.Length; i++) if (GetPartIndex(i) != CurrentPartIndex) break;
                Cursor = --i;
                if (update) UpdateCursor();
            }
        }

        /// <summary>
        /// Moves the cursor to the end of the command line.
        /// </summary>
        /// <param name="update">True if the console cursor should be updated with the virtual cursor.</param>
        public void End(bool update = true) {
            Cursor = _Text.Length;
            if (update) UpdateCursor();
        }

        /// <summary>
        /// Resets the X coordinate offset for the home cursor position.
        /// </summary>
        public void SetCursorHome() => _XOffset = Console.CursorLeft;

        #endregion

        #region Console interactive renderer

        /// <summary>
        /// Renders the current command line to the console.
        /// </summary>
        public void Render() {
            if (!IsRendered) {
                Console.CursorVisible = false;
                _XOffset = Console.CursorLeft;
                if (_Cursor < 0) Cursor = _Text.Length;
                IsRendered = true;
            }
            if (String.IsNullOrEmpty(_Text)) {
                UpdateCursor(true);
                _LastLength = 0;
                return;
            }
            int i, j = 1, n = _Map.Length;
            for (i = 0; i < n; i++) {
                if (j == 0 && _Map[i] != 0) break;
                if (j != 0 && _Map[i] == 0) j--;
            }
            var foregroundDefault = Console.ForegroundColor;
            Console.CursorVisible = false;
            Console.ForegroundColor = CommandColor;
            Console.CursorLeft = _XOffset;
            Console.Write(_Text.Substring(0, i));
            Console.ForegroundColor = ArgumentsColor;
            Console.Write(_Text.Substring(i));
            Console.ForegroundColor = foregroundDefault;
            Console.CursorLeft = _XOffset + Cursor;
            Console.CursorVisible = true;
            _LastLength = _Text.Length;
        }

        /// <summary>
        /// Updates the text of the command line.
        /// </summary>
        public void UpdateText() {
            if (!IsRendered) return;
            var l0 = _LastLength;
            Console.CursorVisible = false;
            Render();
            var dl = l0 - _Text.Length;
            if (dl > 0) {
                var lastLeft = Console.CursorLeft;
                Console.CursorLeft = _XOffset + _Text.Length;
                Console.Write("".PadRight(dl));
                Console.CursorLeft = lastLeft;
            }
            if (Console.CursorLeft > _XOffset + _Text.Length) Console.CursorLeft = _XOffset + _Text.Length;
            Cursor = _Cursor;
            Console.CursorVisible = true;
        }

        /// <summary>
        /// Updates the console cursor position and optionally - visibility.
        /// </summary>
        /// <param name="visible">If not null, the cursor visibility will be changed to this value.</param>
        public void UpdateCursor(bool? visible = null) {
            if (!IsRendered) return;
            if (visible == false) Console.CursorVisible = false;
            Console.CursorLeft = _XOffset + Cursor;
            if (visible == true) Console.CursorVisible = true;
        }

        /// <summary>
        /// Types one character into this command line.
        /// </summary>
        /// <param name="c">Character to type.</param>
        public void Type(char c) {
            if (IsOvertype) {
                if (_Cursor == _Text.Length) Text += c;
                else Text = _Text.Substring(0, _Cursor) + c + _Text.Substring(_Cursor + 1);
            }
            else {
                Text = Text.Insert(_Cursor, c.ToString());
            }
            Cursor++;
            UpdateText();
        }

        /// <summary>
        /// Types a sequence of characters into this command line.
        /// </summary>
        /// <param name="s">A string of characters to type.</param>
        public void Type(string s) {
            if (IsOvertype) {
                if (_Cursor == _Text.Length) Text += s;
                else Text = _Text.Substring(0, _Cursor) + s + ((_Cursor + s.Length <= _Text.Length) ? _Text.Substring(_Cursor + s.Length) : "");
            }
            else {
                Text = _Text.Insert(_Cursor, s);
            }
            Cursor += s.Length;
            UpdateText();
        }

        /// <summary>
        /// Deletes one character back from the cursor position. Moves the cursor 1 character left.
        /// </summary>
        public void Backspace() {
            if (Cursor > 0) {
                Text = _Text.Remove(--Cursor, 1);
                UpdateText();
            }
        }

        /// <summary>
        /// Deletes one character on the cursor position.
        /// </summary>
        public void Delete() {
            if (Cursor < _Text.Length) {
                Text = _Text.Remove(Cursor, 1);
                UpdateText();
            }
        }

        /// <summary>
        /// Accepts a single key and performs an operation on this command line if applicable.
        /// </summary>
        /// <param name="k">Key that was pressed.</param>
        /// <returns>True if the key was handled by the command line itself.</returns>
        public bool AcceptKey(ConsoleKeyInfo k) {
            if (k.Modifiers == 0) {
                switch (k.Key) {
                    case ConsoleKey.Backspace: Backspace(); return true;
                    case ConsoleKey.Delete: Delete(); return true;
                    case ConsoleKey.Home: Home(); return true;
                    case ConsoleKey.LeftArrow: Left(); return true;
                    case ConsoleKey.RightArrow: Right(); return true;
                    case ConsoleKey.End: End(); return true;
                    case ConsoleKey.Insert: IsOvertype = !IsOvertype; return true;
                }
            }
            if (k.Modifiers.HasFlag(ConsoleModifiers.Control) && !k.Modifiers.HasFlag(ConsoleModifiers.Alt)) {
                switch (k.Key) {
                    case ConsoleKey.LeftArrow: Prev(); return true;
                    case ConsoleKey.RightArrow: Next(); return true;
                }
            }
            if ((!k.Modifiers.HasFlag(ConsoleModifiers.Control) || k.Modifiers.HasFlag(ConsoleModifiers.Alt)) && !_NonPrintableKeys.Contains(k.Key)) {
                Type(k.KeyChar);
                return true;
            }
            return false;
        }

        #endregion

        #region Static methods

        /// <summary>
        /// Joins the parts into string separated with spaces.
        /// </summary>
        /// <param name="parts">Parts.</param>
        /// <returns>Line.</returns>
        public static string Join(string[] parts) => String.Join(" ", parts.Select(i => Quote(i)));

        /// <summary>
        /// Quotes the part if the part contains one or more spaces and isn't already quoted.
        /// </summary>
        /// <param name="part">A single command line part.</param>
        /// <returns>Quoted part.</returns>
        public static string Quote(string part) => part.Contains(' ') && part[0] != '"' && part[part.Length - 1] != '"' ? ('"' + part.Replace("\"", "\"\"") + '"') : part;

        /// <summary>
        /// Unquotes the part if its quoted with double quotes. Also unquotes incomplete quoting.
        /// </summary>
        /// <param name="part">A single command line part.</param>
        /// <returns>Unquoted part.</returns>
        public static string Unquote(string part) => _RxUnquotedDoubleQuotes.Replace(part, "");

        /// <summary>
        /// Splits the command line with space character, shell style.
        /// Quotes (both single and double) prevent space from being a separator.
        /// </summary>
        /// <param name="line">A line to split.</param>
        /// <param name="leaveQuotes">If set true, quoted parts will still contain quotes.</param>
        /// <returns>Parts.</returns>
        public static string[] Split(string line, bool leaveQuotes = false) {
            var result = new List<string>();
            var builder = new StringBuilder();
            var q = '-';
            for (int i = 0, n = line.Length; i < n; i++) {
                char c = line[i];
                if (i > 0 && line[i - 1] == c && (c == '\'' || c == '"')) builder.Append(c);
                if (q == '-' && (c == '\'' || c == '"')) { q = c; if (leaveQuotes) builder.Append(c); continue; }
                if (c != '-' && c == q) { q = '-'; if (leaveQuotes) builder.Append(c); continue; }
                if (q == '-' && c == ' ') {
                    if (builder.Length > 0) result.Add(builder.ToString());
                    builder.Clear();
                }
                else builder.Append(c);
            }
            if (builder.Length > 0) result.Add(builder.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// Reads a password from the console in a secure way.
        /// </summary>
        /// <returns>Password as an unmanaged <see cref="SecureString"/>.</returns>
        public static SecureString ReadPassword() {
            var passwd = new SecureString();
            ConsoleKeyInfo k;
            while ((k = Console.ReadKey(true)).Key != ConsoleKey.Enter) {
                if (k.Key == ConsoleKey.Escape) { Console.WriteLine(); return null; }
                else if (k.Key == ConsoleKey.Backspace) {
                    if (passwd.Length > 0) {
                        passwd.RemoveAt(passwd.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else if ((k.Modifiers.HasFlag(ConsoleModifiers.Control) && !k.Modifiers.HasFlag(ConsoleModifiers.Alt)) || _NonPrintableKeys.Contains(k.Key)) continue;
                else {
                    if (k.Modifiers != ConsoleModifiers.Control && k.Modifiers != (ConsoleModifiers.Control | ConsoleModifiers.Shift)) {
                        passwd.AppendChar(k.KeyChar);
                        Console.Write("*");
                    }
                }
            }
            Console.WriteLine();
            return passwd;
        }

        #endregion

        #region Private code

        /// <summary>
        /// Sets properties for the empty instance.
        /// </summary>
        private void Empty() {
            _Text = "";
            Command = String.Empty;
            Arguments = new CommandLineArgumentsEx();
            Cursor = 0;
        }

        /// <summary>
        /// Gets the part index at specified position within the command line text.
        /// </summary>
        /// <param name="at">Position within the command line text.</param>
        /// <returns>Part index, negative for whitespace that doesn't belong to any part.</returns>
        private int GetPartIndex(int at) => at < 0 ? 0 : _Map[at < _Text.Length ? at : _Text.Length - 1];

        /// <summary>
        /// Splits the command line into command and arguments, creates parts map.
        /// </summary>
        private void Parse() {
            if (String.IsNullOrEmpty(_Text)) { Empty(); return; }
            _Parts_U = Split(_Text);
            _Parts_Q = Split(_Text, true);
            _Map = new int[_Text.Length];
            int partIndex = -1, partLeft = -1;
            bool isOutside = true;
            for (int i = 0, n = _Map.Length; i < n; i++) {
                if (isOutside) {
                    if (_Text[i] == ' ') _Map[i] = -1;
                    else {
                        isOutside = false;
                        partIndex++;
                        _Map[i] = partIndex;
                        partLeft = _Parts_Q[partIndex].Length;
                    }
                    continue;
                }
                else {
                    _Map[i] = partIndex;
                    if (--partLeft < 1) isOutside = true;
                }
            }
            Command = _Parts_U.FirstOrDefault();
            Arguments = new CommandLineArgumentsEx(_Parts_U.Skip(1));
        }

        #endregion

        #region Conversions

        /// <summary>
        /// Implicit <see cref="string"/> conversion, just returns the <see cref="CommandLine.Text"/>.
        /// </summary>
        /// <param name="l"></param>
        public static implicit operator string(CommandLine l) => l.Text;

        /// <summary>
        /// Implicit <see cref="CommandLine"/> conversion, creates new <see cref="CommandLine"/> from <see cref="string"/>.
        /// </summary>
        /// <param name="s"></param>
        public static implicit operator CommandLine(string s) => new CommandLine(s);

        #endregion

        #region Private data

        /// <summary>
        /// Cursor position cache.
        /// </summary>
        private int _Cursor;

        /// <summary>
        /// Command line text cache.
        /// </summary>
        private string _Text;

        /// <summary>
        /// Command line parts: quoted.
        /// </summary>
        private string[] _Parts_Q;

        /// <summary>
        /// Command line parts: unquoted.
        /// </summary>
        private string[] _Parts_U;

        /// <summary>
        /// Parts map.
        /// </summary>
        private int[] _Map;

        /// <summary>
        /// <see cref="IsOvertype"/> cache.
        /// </summary>
        private bool _IsOvertype;

        /// <summary>
        /// The length of the command line in last render operation.
        /// </summary>
        private int _LastLength;

        /// <summary>
        /// Keys to be ignored by <see cref="Type(char)"/> method.
        /// </summary>
        private static readonly ConsoleKey[] _NonPrintableKeys = new ConsoleKey[] {
            ConsoleKey.Backspace, ConsoleKey.Tab, ConsoleKey.Enter,
            ConsoleKey.Pause, ConsoleKey.Escape, ConsoleKey.PageUp, ConsoleKey.PageDown, ConsoleKey.End, ConsoleKey.Home,
            ConsoleKey.LeftArrow, ConsoleKey.UpArrow, ConsoleKey.RightArrow, ConsoleKey.DownArrow,
            ConsoleKey.PrintScreen, ConsoleKey.Insert, ConsoleKey.Delete,
            ConsoleKey.LeftWindows, ConsoleKey.RightWindows,
            ConsoleKey.F1, ConsoleKey.F2, ConsoleKey.F3, ConsoleKey.F4, ConsoleKey.F5, ConsoleKey.F6, ConsoleKey.F7, ConsoleKey.F8,
            ConsoleKey.F9, ConsoleKey.F10, ConsoleKey.F11, ConsoleKey.F12, ConsoleKey.F13, ConsoleKey.F14, ConsoleKey.F15, ConsoleKey.F16,
            ConsoleKey.F17, ConsoleKey.F18, ConsoleKey.F19, ConsoleKey.F20, ConsoleKey.F21, ConsoleKey.F22, ConsoleKey.F23, ConsoleKey.F24
        };

        private static readonly Regex _RxUnquotedDoubleQuotes = new Regex(@"""{1}", RegexOptions.Compiled);

        /// <summary>
        /// X coordinate of the original console position of the rendered command line.
        /// </summary>
        private int _XOffset;

        #endregion

    }

}