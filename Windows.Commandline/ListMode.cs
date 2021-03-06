﻿using System;
using System.IO;
using CommandLineParser.Arguments;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace uTILLIty.UploadrNet.Windows
{
	internal class ListMode : ModeBase
	{
		[SwitchArgument(Argument.UnsetShortNameChar, "list", false, Optional = false,
			Description = "Executes List Mode")]
		public override bool ModeChosen { get; set; }

		[FileArgument('k', "key", Optional = false, FileMustExist = false,
			Description = "The file to save the token to (will be overwritten, if it exists)")]
		public override FileInfo KeyFile { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "showargs", false,
			Description = "Show parsed arguments (with a pause, before starting to process)")]
		public override bool ShowParsedArgs { get; set; }

		[SwitchArgument('a', "albums", false,
			Description = "Lists all albums")]
		public bool ListAlbums { get; set; }

		public override void Execute()
		{
			if (!ModeChosen)
				throw new InvalidOperationException("Invalid mode");

			var mgr = LoadToken();
			if (ListAlbums)
			{
				try
				{
					var f = mgr.Surrogate;
					var sets = f.PhotosetsGetList(mgr.AccountDetails.UserId);
					Console.WriteLine("The following sets have been retrieved from the server:");
					foreach (var set in sets)
					{
						Console.WriteLine($"ID:{set.PhotosetId} {set.Title}");
					}
					Console.WriteLine("To specify an album in PROCESS mode, use either it's (unique) name, or the ID:<id> syntax");
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException($"Error retrieving list of albums. {ex.Message}", ex);
				}
			}
		}
	}
}