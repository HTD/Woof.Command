using System;

namespace Woof.Command {

    /// <summary>
    /// Event arguments for <see cref="Command"/> event.
    /// </summary>
    public class CommandEventArgs : EventArgs {

        /// <summary>
        /// Gets the associated command line instance.
        /// </summary>
        public CommandLine CommandLine { get; }

        /// <summary>
        /// Set if the command was handled.
        /// </summary>
        public bool IsHandled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the current shell session should end.
        /// </summary>
        public bool ShouldExit { get; set; }

        /// <summary>
        /// Creates new event arguments for the <see cref="Command"/> event.
        /// </summary>
        /// <param name="commandLine">Command line.</param>
        public CommandEventArgs(string commandLine) => CommandLine = new CommandLine(commandLine);

    }

}