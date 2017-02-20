using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CommandLineParser.Exceptions;
using uTILLIty.UploadrNet.Windows.Models;

namespace uTILLIty.UploadrNet.Windows
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var parser = new CommandLineParser.CommandLineParser();
			try
			{
				var cmdLine = new Commandline();
				parser.ExtractArgumentAttributes(cmdLine);
				parser.ParseCommandLine(args);
				parser.ShowParsedArguments();

				var mgr = new FlickrManager();
				var ct = new CancellationToken();
				var uploadMgr = new UploadManager(ct, mgr)
				{
					MaxConcurrentOperations = cmdLine.MaxConcurrentOperations,
					UpdateSetsOfDuplicates = cmdLine.UpdateSetsOfDuplicates,
					CheckOnly = cmdLine.CheckOnly,
					LogAction = Console.WriteLine
				};
				var list = new List<PhotoModel>(1000);
				var di = new DirectoryInfo(cmdLine.RootDirectory);
				FillPhotosFromPath(di, list, di.Parent.FullName.Length + 1);
				SetFromCommandline(mgr, list, cmdLine);
				Console.WriteLine($"Ready to process {list.Count} photos.");
				//Console.ReadLine();
				uploadMgr.AddRange(list);
				uploadMgr.ProcessAsync().Wait(ct);
				Console.WriteLine("*** Completed ***");
			}
			catch (CommandLineException ex)
			{
				parser.ShowUsage();
				Console.WriteLine($"\r\n{ex.Message}\r\nPlease check the parameters supplied!");
				Console.ReadLine();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"\r\n{ex.Message}");
				Console.ReadLine();
			}
		}

		private static void SetFromCommandline(FlickrManager mgr, List<PhotoModel> list, Commandline cmdLine)
		{
			var f = mgr.Surrogate;
			var sets = f.PhotosetsGetList(mgr.AccountDetails.UserId);
			var setsToAdd = new List<PhotosetModel>();
			foreach (var setName in cmdLine.AddToSets)
			{
				var set = sets.FirstOrDefault(s => s.Title.Equals(setName, StringComparison.CurrentCultureIgnoreCase));
				if (set == null)
				{
					if (cmdLine.AutoCreateSets)
					{
						setsToAdd.Add(new PhotosetModel(null, setName, null));
					}
					else
					{
						var names = string.Join("', '", sets.Select(s => s.Title));
						throw new InvalidOperationException(
							$"The Album named '{setName}' was not found on the server. Possible names are: '{names}'");
					}
				}
				else
				{
					setsToAdd.Add(new PhotosetModel(set.PhotosetId, set.Title, set.Description));
				}
			}
			foreach (var item in list)
			{
				item.Tags = cmdLine.Tags;
				item.Title = cmdLine.Title ?? item.Filename;
				item.ContentType = cmdLine.ContentType;
				item.Description = cmdLine.Description;
				item.SafetyLevel = cmdLine.SafetyLevel;
				item.SearchState = cmdLine.SearchState;
				item.IsFamily = cmdLine.IsFamily;
				item.IsFriend = cmdLine.IsFriend;
				item.IsPublic = cmdLine.IsPublic;
				foreach (var s in setsToAdd)
					item.Sets.Add(s);
			}
		}

		private static void FillPhotosFromPath(DirectoryInfo di, List<PhotoModel> list, int prefixLen)
		{
			var q = new Queue<DirectoryInfo>();
			q.Enqueue(di);
			while (q.Count > 0)
			{
				di = q.Dequeue();
				Console.Write($"Scanning {di.FullName.Substring(prefixLen)}... ");
				var files = di.GetFiles();
				var added = 0;
				foreach (var f in files)
				{
					//if (list.Count > 100)
					//	break;
					var ext = f.Extension.ToLowerInvariant();
					switch (ext)
					{
						case ".png":
						case ".jpg":
							var item = new PhotoModel {LocalPath = f.FullName};
							list.Add(item);
							added++;
							break;
					}
				}
				Console.WriteLine($"added {added} images");
				var dirs = di.GetDirectories();
				foreach (var dir in dirs)
				{
					q.Enqueue(dir);
				}
			}
		}
	}
}