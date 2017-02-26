using System;
using System.IO;
using System.Text;
using CommandLineParser.Arguments;

namespace uTILLIty.UploadrNet.Windows
{
	internal class AuthenticateMode : ModeBase
	{
		[SwitchArgument(' ', "authenticate", false, Optional = false,
			Description = "Executes Authentication Mode, which interactively " +
			              "generates an authorization token required for Process Mode")]
		public override bool ModeChosen { get; set; }

		[FileArgument('k', "key", Optional = false, FileMustExist = false,
			Description = "The file to save the token to (will be overwritten, if it exists)")]
		public override FileInfo KeyFile { get; set; }

		public override string AdditionalCommandlineArgsInfos { get; } = null;

		public override void Execute()
		{
			if (!ModeChosen)
				throw new InvalidOperationException("Invalid mode");

			var mgr = new FlickrManager();
			mgr.InteractiveAuthorize();
			Console.WriteLine("Please enter the code displayed on the website:");
			var code = Console.ReadLine();
			mgr.InteractiveAuthorizeComplete(code);
			var token = mgr.GetToken();
			using (var stream = KeyFile.OpenWrite())
			{
				token.ToXml(stream, Encoding.UTF8, null);
			}
			Console.WriteLine($"Token successfully exported to\r\n{KeyFile.FullName}");
		}
	}
}