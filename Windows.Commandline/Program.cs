using System;
using System.Diagnostics;
using System.Reflection;
using CommandLineParser.Exceptions;

namespace uTILLIty.UploadrNet.Windows
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var modeSelected = false;
			var ver = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
			Console.WriteLine();
			PrintLine();
			Console.WriteLine($"{ver.ProductName} Version {ver.FileVersion}");
			Console.WriteLine($"{ver.Comments}");
			PrintLine();
			Console.WriteLine($"{ver.LegalCopyright} - {ver.CompanyName}");
			Console.WriteLine($"{ver.LegalTrademarks}");
			PrintLine();
			Console.WriteLine();
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

						if (mode.ShowParsedArgs)
						{
							modeSelected = true;
							PrintLine();
							parser.ShowParsedArguments();
							PrintLine();
							Console.WriteLine("Ready to Process! Press any key to continue");
							Console.ReadKey();
							Console.WriteLine("Starting...");
						}

						mode.Execute();
						Console.WriteLine("*** Completed ***");
						return;
					}
					catch (CommandLineException ex)
					{
						if (mode.ModeChosen)
						{
							modeSelected = true;
							parser.ShowUsage();
							var infos = mode.AdditionalCommandlineArgsInfos;
							if (!string.IsNullOrEmpty(infos))
								Console.WriteLine(infos);
							PrintLine();
							parser.ShowParsedArguments();
							PrintLine();
							Console.WriteLine("*** FATAL ERROR OCCURED - CANNOT CONTINUE ***");
							Console.WriteLine($"{ex.Message}");
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
			if (!modeSelected)
				Console.WriteLine("Usage: (--authenticate  | --list | --process) [parameters...]");
			//Console.ReadLine();
		}

		private static void PrintLine()
		{
			Console.Write(new string('-', Console.BufferWidth));
		}
	}
}