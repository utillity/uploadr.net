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
				var parser = new CommandLineParser.CommandLineParser();
				var processMode = new ProcessMode();
				try
				{
					parser.ExtractArgumentAttributes(processMode);
					parser.ParseCommandLine(args);
					//parser.ShowParsedArguments();
					processMode.Execute();
					Console.WriteLine("*** Completed ***");
					return;
				}
				catch (CommandLineException ex)
				{
					if (processMode.ModeChosen)
					{
						Console.WriteLine($"Processing Mode\r\n{ex.Message}\r\nPlease check the parameters supplied!");
						//parser.ShowParsedArguments();
						parser.ShowUsage();
					}
				}
				parser = new CommandLineParser.CommandLineParser();
				var authenticateMode = new AuthenticateMode();
				try
				{
					parser.ExtractArgumentAttributes(authenticateMode);
					parser.ParseCommandLine(args);
					authenticateMode.Execute();
					Console.WriteLine("*** Completed ***");
					return;
				}
				catch (CommandLineException ex)
				{
					if (authenticateMode.ModeChosen)
					{
						Console.WriteLine($"Authentication Mode\r\n{ex.Message}\r\nPlease check the parameters supplied!");
						//parser.ShowParsedArguments();
						parser.ShowUsage();
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