using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Woof.SystemEx;

namespace Woof.Command {

    /// <summary>
    /// Commands Hell v0.666 ;)
    /// </summary>
    public sealed class CommandShell {

        #region Events

        /// <summary>
        /// Occurs whenever the Enter key is pressed - current line is passed as event argument.
        /// The event handler can optionally end the shell session.
        /// </summary>
        public event EventHandler<CommandEventArgs> Command;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the shell header message.
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// Gets the internal man pages or allows to update them with additional pages when set.
        /// The set operation does not overwrite original pages, it only adds new pages.
        /// </summary>
        /// <remarks>
        /// Careful, the existing pages cannot be updated or removed.
        /// </remarks>
        public Dictionary<string, string[]> ManPages {
            get => InternalManPages;
            set {
                foreach (var p in value) if (!InternalManPages.ContainsKey(p.Key)) InternalManPages.Add(p.Key, p.Value);
                AutoComplete.AddCommands(value.Keys);
            }
        }

        /// <summary>
        /// Gets or sets the shell command prompt format string.
        /// </summary>
        public string PromptFormat { get; set; }

        /// <summary>
        /// Gets or sets optional settings key containing settings for this shell.
        /// </summary>
        public NameValueCollection Settings { get; set; }

        /// <summary>
        /// Gets or sets optional settings file to store this shell settings.
        /// </summary>
        public IniFile SettingsFile { get; set; }

        #endregion

        #region Private data

        #region Keyboard shortcuts

        private static readonly ConsoleKeyShortcut KeyExec = new ConsoleKeyShortcut(0, ConsoleKey.Enter);
        private static readonly ConsoleKeyShortcut KeyExit = new ConsoleKeyShortcut(ConsoleModifiers.Control, ConsoleKey.D);
        private static readonly ConsoleKeyShortcut KeyClear = new ConsoleKeyShortcut(ConsoleModifiers.Control, ConsoleKey.L);
        private static readonly ConsoleKeyShortcut KeyHistoryPrev = new ConsoleKeyShortcut(0, ConsoleKey.UpArrow);
        private static readonly ConsoleKeyShortcut KeyHistoryNext = new ConsoleKeyShortcut(0, ConsoleKey.DownArrow);
        private static readonly ConsoleKeyShortcut KeyAutoComplete = new ConsoleKeyShortcut(0, ConsoleKey.Tab);

        #endregion

        #region Console colors

        private readonly ConsoleColor ColorDefault = Console.ForegroundColor;
        private const ConsoleColor ColorHeader = ConsoleColor.DarkCyan;
        private const ConsoleColor ColorPrompt = ConsoleColor.Gray;
        private const ConsoleColor ColorSpecial = ConsoleColor.Green;
        private const ConsoleColor ColorNotice = ConsoleColor.Cyan;
        private const ConsoleColor ColorWarning = ConsoleColor.Yellow;
        private const ConsoleColor ColorError = ConsoleColor.Red;
        private const ConsoleColor ColorContent = ConsoleColor.DarkGray;
        private const ConsoleColor ColorHistory = ConsoleColor.DarkGray;
        private const ConsoleColor ColorPeek = ConsoleColor.DarkGreen;

        #endregion

        #region Other constants

        /// <summary>
        /// History setting name.
        /// </summary>
        private const string Settings_History = "history";

        #endregion

        #region Internal micro documentation

        /// <summary>
        /// Man pages for the <see cref="CommandShell"/> module.
        /// </summary>
        private readonly Dictionary<string, string[]> InternalManPages = new Dictionary<string, string[]> {
            ["cat"] = new string[] {
                "Usage: cat [FILE]",
                "Concatenates a file to this shell output."
            },
            ["cd"] = new string[] {
                "Usage: cd [[DIRECTORY]]",
                "Changes current directory or shows current directory when used without a parameter."
            },
            ["cls"] = new string[] {
                "Usage: cls",
                "Clears the console window. Press Ctrl+L instead."
            },
            ["exit"] = new string[] {
                "Usage: exit",
                "Exits this PSQL shell session. Press Ctrl+D instead."
            },
            ["history"] = new string[] {
                "Usage: history [[-c]|[-clear]]",
                "Shows current command history or clear it if '-clear' switch is used."
            },
            ["ls"] = new string[] {
                "Usage: ls [[DIRECTORY]]",
                "Lists the detailed content of the current or specified directory."
            },
            ["man"] = new string[] {
                "Usage: man [[PAGE]]",
                "Shows a micro-manual for the specified command of this shell.",
                "Shows list of available internal commands when used without [PAGE] parameter."
            },
            ["pwd"] = new string[] {
                "Usage: pwd",
                "Shows the path to the current working directory."
            },
            ["touch"] = new string[] {
                "Usage: touch [FILE]",
                "Creates a new empty file, or sets the last write time of the existing one to current."
            }
        };

        #endregion

        #region State

        /// <summary>
        /// Current line interactive object.
        /// </summary>
        private readonly CommandLine CurrentLine;

        /// <summary>
        /// Command history.
        /// </summary>
        private readonly CommandHistory History;

        /// <summary>
        /// Auto complete list.
        /// </summary>
        private readonly CommandAutoCompleteList AutoComplete;

        #endregion

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the instance with optional default header and prompt format.
        /// </summary>
        /// <param name="header">Header to display when the shell starts.</param>
        /// <param name="prompt">Command prompt format string, use {0} for current directory.</param>
        public CommandShell(string header = "Commands Hell v0.666", string prompt = "CS {0}> ") {
            Header = header;
            PromptFormat = prompt;
            AutoComplete = new CommandAutoCompleteList(InternalManPages.Keys);
            CurrentLine = new CommandLine();
            History = new CommandHistory();
        }

        /// <summary>
        /// Starts the shell session. Blocks the current thread until exited.
        /// </summary>
        public void Start() {
            HistoryRestore();
            Console.ForegroundColor = ColorHeader;
            Console.WriteLine(Header);
            for (; ; ) {
                Prompt();
                CurrentLine.Render();
                for (; ; ) {
                    var k = Console.ReadKey(true);
                    HandleResets(k);
                    HandleAutoComplete(k);
                    HandleHistory(k);
                    HandleClear(k);
                    if (HandleExec(k, out var shouldExit)) if (shouldExit) return; else break;
                    if (HandleExit(k)) return;
                    CurrentLine.AcceptKey(k);
                }
            }
        }

        #endregion

        #region Helpers
        /// <summary>
        /// Shows a text message.
        /// </summary>
        /// <param name="message">Text message.</param>
        /// <param name="type">Message type from <see cref="CommandMessageType"/> enumeration.</param>
        public void ShowMsg(string message, CommandMessageType type = CommandMessageType.Content) {
            if (message == null) return;
            var foregroundCurrent = Console.ForegroundColor;
            switch (type) {
                case CommandMessageType.Content: Console.ForegroundColor = ColorContent; break;
                case CommandMessageType.Info: Console.ForegroundColor = ColorHeader; break;
                case CommandMessageType.Notice: Console.ForegroundColor = ColorNotice; break;
                case CommandMessageType.Special: Console.ForegroundColor = ColorSpecial; break;
                case CommandMessageType.Warning: Console.ForegroundColor = ColorWarning; break;
                case CommandMessageType.Error: Console.ForegroundColor = ColorError; break;
            }
            Console.WriteLine(message);
            Console.ForegroundColor = foregroundCurrent;
        }

        /// <summary>
        /// Shows a line collection.
        /// </summary>
        /// <param name="lines">Any text lines collection.</param>
        /// <param name="color">Optional text color.</param>
        public void ShowLines(IEnumerable<string> lines, ConsoleColor color = ColorContent) {
            if (lines == null || !lines.Any()) return;
            var foregroundCurrent = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(String.Join(Environment.NewLine, lines));
            Console.ForegroundColor = foregroundCurrent;
        }

        #endregion

        #region Keyboard event handlers

        /// <summary>
        /// Handles resetting special shell modes like history and auto-complete.
        /// </summary>
        /// <param name="k">Keyboard chord.</param>
        void HandleResets(ConsoleKeyInfo k) {
            if (History.CurrentIndex >= 0 && !KeyHistoryPrev.IsMatch(k) && !KeyHistoryNext.IsMatch(k)) History.Reset();
            if (AutoComplete.CurrentIndex >= 0 && !KeyAutoComplete.IsMatch(k)) AutoComplete.Reset();
        }

        void HandleClear(ConsoleKeyInfo k) {
            if (!KeyClear.IsMatch(k)) return;
            Console.Clear();
            Prompt();
            CurrentLine.UpdateText();
        }

        /// <summary>
        /// Handles auto-complete shortcut.
        /// </summary>
        /// <param name="k"></param>
        void HandleAutoComplete(ConsoleKeyInfo k) {
            if (!KeyAutoComplete.IsMatch(k)) return;
            if (AutoComplete.CurrentIndex < 0) {
                AutoComplete.Match(CurrentLine.CurrentPart, CurrentLine.CurrentPartIndex == 0 || CurrentLine.Command == "man");
            }
            var replacement = AutoComplete.Next();
            if (replacement != null) {
                CurrentLine.CurrentPart = replacement;
                CurrentLine.UpdateText();
            }
            if (AutoComplete.Count > 1) AutoComplete.Peek();
        }

        /// <summary>
        /// Handles history shortcuts.
        /// </summary>
        /// <param name="k">Keyboard chord.</param>
        void HandleHistory(ConsoleKeyInfo k) {
            string line = null;
            if (KeyHistoryPrev.IsMatch(k)) line = History.Prev(CurrentLine);
            else if (KeyHistoryNext.IsMatch(k)) line = History.Next();
            if (line != null) {
                CurrentLine.Text = line;
                CurrentLine.UpdateText();
                CurrentLine.End();
            }
        }

        /// <summary>
        /// Handles the enter key.
        /// </summary>
        /// <param name="k">Keyboard chord.</param>
        /// <param name="shouldExit">True when exit is requested either by internal or external command.</param>
        /// <returns>True if shortcut matched.</returns>
        bool HandleExec(ConsoleKeyInfo k, out bool shouldExit) {
            if (KeyExec.IsMatch(k)) {
                History.Add(CurrentLine);
                HistoryStore();
                var e = new CommandEventArgs(CurrentLine);
                Console.WriteLine();
                CurrentLine.Text = "";
                OnCommand(e);
                shouldExit = e.ShouldExit;
                return true;
            }
            shouldExit = false;
            return false;
        }

        /// <summary>
        /// Returns true if shell exit shortcut is detected.
        /// </summary>
        /// <param name="k">Keyboard chord.</param>
        /// <returns>True if exit shortcut is detected.</returns>
        bool HandleExit(ConsoleKeyInfo k) => KeyExit.IsMatch(k);

        /// <summary>
        /// Internal command handler.
        /// If the command is not handled in <see cref="Command"/> event, it's handled here.
        /// </summary>
        /// <param name="e">Command arguments.</param>
        private void OnCommand(CommandEventArgs e) {
            Command?.Invoke(this, e);
            if (!e.IsHandled) {
                if (PathTools.IsFileAcessibleInPath(e.CommandLine.Command)) Execute(PathTools.GetFullPath(e.CommandLine.Command), false, e.CommandLine.Arguments.Raw);
                else if (e.CommandLine.Arguments.Switches["?|help"] && InternalManPages.ContainsKey(e.CommandLine.Command)) {
                    Man(e.CommandLine.Command);
                    e.IsHandled = true;
                }
                else switch (e.CommandLine.Command) {
                        case "cat": GetContent(e.CommandLine.Arguments.Positional[0]); break;
                        case "cd": SetLocation(e.CommandLine.Arguments.Positional[0]); break;
                        case "cls": Console.Clear(); break;
                        case "exit": e.ShouldExit = true; break;
                        case "history": if (e.CommandLine.Arguments.Switches["c|clear"]) HistoryClear(); else HistoryShow(); break;
                        case "ls": GetLocation(e.CommandLine.Arguments.Positional[0] ?? "."); break;
                        case "man": Man(e.CommandLine.Arguments.Positional[0]); break;
                        case "pwd": ShowMsg(Directory.GetCurrentDirectory()); break;
                        case "touch": Touch(e.CommandLine.Arguments.Positional[0]); break;
                        default:
                            Execute($"cmd.exe", false, "/c", $"{e.CommandLine.Command} {String.Join(" ", e.CommandLine.Arguments.Raw)}");
                            break;
                    }
            }
            //ShowMsg(e.Output);
        }

        #endregion

        #region Internal commands

        /// <summary>
        /// Shows command prompt as defined.
        /// </summary>
        private void Prompt() {
            var cd = Directory.GetCurrentDirectory();
            Console.ForegroundColor = ColorPrompt;
            Console.Write(PromptFormat, cd);
            Console.ForegroundColor = ColorDefault;
            CurrentLine.SetCursorHome();
        }

        /// <summary>
        /// Executes external command with STDOUT and STDERR redirected to this shell.
        /// Blocks current thread until external process exits.
        /// </summary>
        /// <param name="command">Command name or file name.</param>
        /// <param name="redirection">True to redirect standard output and standard error, if set true, the output from the process will be intercepted and own, colored output will occur.</param>
        /// <param name="arguments">Arguments to pass.</param>
        public void Execute(string command, bool redirection = true, params string[] arguments) {
            var currentForeground = Console.ForegroundColor;
            Console.ForegroundColor = ColorContent;
            try {
                using (var process = new Process() {
                    StartInfo = new ProcessStartInfo(command, CommandLine.Join(arguments)) { UseShellExecute = false },
                    EnableRaisingEvents = true
                }) {
                    if (redirection) {
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.OutputDataReceived += (s, e) => {
                            Console.ForegroundColor = ColorContent;
                            Console.WriteLine(e.Data);
                        };
                        process.ErrorDataReceived += (s, e) => {
                            Console.ForegroundColor = ColorError;
                            Console.WriteLine(e.Data);
                        };
                    }
                    if (process.Start()) {
                        if (redirection) {
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();
                        }
                        process.WaitForExit();
                    }
                }
            }
            catch (Exception x) {
                Console.ForegroundColor = ColorError;
                Console.WriteLine($"EXTERNAL COMMAND CAUSED EXCEPTION: {x.Message}");
            }
            finally {
                Console.ForegroundColor = currentForeground;
            }
        }

        /// <summary>
        /// Displays the content of the text file.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        void GetContent(string fileName) {
            if (fileName == null) ShowMsg(String.Join(Environment.NewLine, new string[] {
                    @"      |\__/,|   (`\  ",
                    @"    _.|o o  |_   ) ) ",
                    @"---(((---(((---------"
                }), CommandMessageType.Special);
            else if (!File.Exists(fileName)) ShowMsg("No such file.", CommandMessageType.Warning);
            else
                try {
                    ShowMsg(File.ReadAllText(fileName));
                }
                catch (Exception x) {
                    ShowMsg($"Could not read: {x.Message}", CommandMessageType.Error);
                }
        }

        /// <summary>
        /// Displays a list of directories and files in the specified directory.
        /// </summary>
        /// <param name="dir">Existing directory. A warning is displayed when the directory doesn't exist.</param>
        void GetLocation(string dir) {
            if (Directory.Exists(dir))
                ShowMsg(String.Join(Environment.NewLine,
                    Directory.EnumerateDirectories(dir).Select(i => new DirectoryInfo(i)).OrderBy(i => i.Name).Select(i => $"{i.LastWriteTime}\t<DIR>\t{i.Name}").Concat(
                    Directory.EnumerateFiles(dir).Select(i => new FileInfo(i)).OrderBy(i => i.Name).Select(i => $"{i.LastWriteTime}\t{i.Length}\t{i.Name}")))
                );
            else ShowMsg($"No such directory: {dir}.", CommandMessageType.Warning);
        }

        /// <summary>
        /// Stores the current history to the settings file if one is configured.
        /// </summary>
        void HistoryStore() {
            if (SettingsFile != null && Settings != null) {
                Settings[Settings_History] = Convert.ToBase64String(History.Serialized);
                SettingsFile.Write();
            }
        }

        /// <summary>
        /// Restores the current history from the settings file if one is configured.
        /// </summary>
        void HistoryRestore() {
            if (SettingsFile != null && Settings != null) History.Serialized = Convert.FromBase64String(Settings[Settings_History] ?? "");
        }

        /// <summary>
        /// Clears the current history and updates the settings file if one is configured.
        /// </summary>
        void HistoryClear() {
            History.Clear();
            if (SettingsFile != null && Settings != null) {
                Settings[Settings_History] = null;
                SettingsFile.Write();
            }
        }

        /// <summary>
        /// Shows the history lines (if any available).
        /// </summary>
        void HistoryShow() => ShowMsg(History.ToString(1) ?? "The list is empty.");

        /// <summary>
        /// Shows the micro-manual for the internal shell command.
        /// </summary>
        /// <param name="page"></param>
        private void Man(string page) {
            if (page == null) {
                ShowMsg(
                    "Please specify micro-manual page from the following:" + Environment.NewLine +
                    "[ " + String.Join(", ", InternalManPages.Keys) + " ]"
                );
            }
            else if (InternalManPages.ContainsKey(page)) {
                ShowMsg(String.Join(Environment.NewLine, InternalManPages[page]));
            }
            else ShowMsg($"There's no manual page on \"{page}\".", CommandMessageType.Warning);
        }

        /// <summary>
        /// Sets the current location to the directory specified, or displays the current directory on null parameter.
        /// </summary>
        /// <param name="dir">Existing directory. A warning is displayed when the directory doesn't exist.</param>
        void SetLocation(string dir) {
            if (dir == null) ShowMsg(Directory.GetCurrentDirectory());
            else if (Directory.Exists(dir)) Directory.SetCurrentDirectory(dir);
            else ShowMsg($"No such directory: {dir}.", CommandMessageType.Warning);
        }
        /// <summary>
        /// Either creates an empty file or modifies last write time of an existing file.
        /// </summary>
        /// <param name="fileName">A path to the file.</param>
        private void Touch(string fileName) {
            try {
                if (File.Exists(fileName)) File.SetLastWriteTime(fileName, DateTime.Now);
                else File.Create(fileName).Dispose();
            }
            catch {
                ShowMsg("Can't touch this.", CommandMessageType.Error);
            }
        }

        #endregion

    }

    /// <summary>
    /// Console message type enumeration.
    /// </summary>
    public enum CommandMessageType {
        /// <summary>
        /// Generic text content.
        /// </summary>
        Content,
        /// <summary>
        /// Distinguished informational message.
        /// </summary>
        Info,
        /// <summary>
        /// Special distinctive message.
        /// </summary>
        Special,
        /// <summary>
        /// A notice. Nothing wrong here.
        /// </summary>
        Notice,
        /// <summary>
        /// A warning. Probably the user has done something wrong.
        /// </summary>
        Warning,
        /// <summary>
        /// An error. Either the user has entered invalid data, or the command encountered an exception.
        /// </summary>
        Error
    }

}