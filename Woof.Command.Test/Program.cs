namespace Woof.Command.Test {

    class Program {

        /**
         * Type "man" to see internal commands.
         * Use Tab key a lot to see a list of possible completions.
         */

        static void Main(string[] args) {
            var cli = new CommandShell(); // creates a new default commands hell, a little CMD on steroids.
            cli.Start(); // starts the shell session.
        }

    }

}