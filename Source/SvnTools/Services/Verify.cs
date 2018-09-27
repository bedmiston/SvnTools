using SvnTools.Utility;

// $Id$

namespace SvnTools.Services
{
    /// <summary>
    /// A class encapsulating the hotcopy command from svnadmin.
    /// </summary>
    public class Verify
      : SvnAdmin
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Verify"/> class.
        /// </summary>
        public Verify()
        {
            Command = "verify";
        }

      /// <summary>
      /// Generates the command line arguments.
      /// </summary>
      /// <returns>
      /// Returns a string value containing the command line arguments to pass directly to the executable file.
      /// </returns>
      protected override void AppendCommand(CommandLineBuilder commandLine)
        {
            base.AppendCommand(commandLine);
        }
    }
}
