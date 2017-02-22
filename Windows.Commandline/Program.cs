using System;
using System.Reflection;
using CommandLineParser.Exceptions;

namespace uTILLIty.UploadrNet.Windows
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			try
			{
				var modes = new ModeBase[]
				{
					new AuthenticateMode(), new ListMode(), new ProcessMode()
				};
				foreach (var mode in modes)
				{
					var parser = new CommandLineParser.CommandLineParser();
					//parser.AdditionalArgumentsSettings.AcceptAdditionalArguments = false;
					try
					{
						parser.ExtractArgumentAttributes(mode);
						parser.ParseCommandLine(args);

						//parser.ShowParsedArguments();
						//Console.WriteLine("Ready to Process! Press any key to continue");
						//Console.ReadKey();
						//Console.WriteLine("Starting...");

						mode.Execute();
						Console.WriteLine("*** Completed ***");
						return;
					}
					catch (CommandLineException ex)
					{
						if (mode.ModeChosen)
						{
							Console.WriteLine($"\r\n\r\n\r\n{ex.Message}\r\n\r\nPlease check the parameters supplied!");
							parser.ShowUsage();
							parser.ShowParsedArguments();
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (ex is TargetInvocationException && ex.InnerException != null)
					ex = ex.InnerException;
				Console.WriteLine($"\r\n{ex.Message}");
				//Console.ReadLine();
				return;
			}
			Console.WriteLine("Usage: --authenticate | --process");
			//Console.ReadLine();
		}
	}
}