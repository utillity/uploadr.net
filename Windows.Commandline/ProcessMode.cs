using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using CommandLineParser.Arguments;
using FlickrNet;
using uTILLIty.UploadrNet.Windows.Models;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace uTILLIty.UploadrNet.Windows
{
	internal class ProcessMode
	{
		private static readonly Regex ExpressionRegex = new Regex(@"(?in)(?<exp>\{(?<name>[^}]+)\})");

		[SwitchArgument(Argument.UnsetShortNameChar, "process", false, Optional = false,
			Description = "Executes Process Mode, which uploads local files to Flickr")]
		public bool ModeChosen { get; set; }

		[DirectoryArgument('s', "source", Optional = false, DirectoryMustExist = true,
			Description = "The source directory to scan (will also scan all subdirectories below it")]
		public DirectoryInfo RootDirectory { get; set; }

		[ValueArgument(typeof (string), Argument.UnsetShortNameChar, "types", Optional = true, AllowMultiple = true,
			Description = "The file-types to process")]
		public string[] FileTypes { get; set; } = {
			"jpg", "jpeg", "png", "gif", "mp4", "avi", "wmv", "mov", "mpeg", "m2ts",
			"3gp", "ogg", "ogv"
		};

		[FileArgument('k', "key", Optional = false, FileMustExist = true)]
		public FileInfo KeyFile { get; set; }

		[ValueArgument(typeof (string), 'a', "album", AllowMultiple = true)]
		public string[] AddToSets { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "createAlbums", false)]
		public bool AutoCreateSets { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "checkonly", false)]
		public bool CheckOnly { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "updatedups", false)]
		public bool UpdateDuplicates { get; set; }

		[ValueArgument(typeof (ContentType), Argument.UnsetShortNameChar, "ctype")]
		public ContentType ContentType { get; set; }

		[ValueArgument(typeof (byte), Argument.UnsetShortNameChar, "parallelism", DefaultValue = (byte) 10)]
		public byte MaxConcurrentOperations { get; set; } = 10;

		[ValueArgument(typeof (string), Argument.UnsetShortNameChar, "desc")]
		public string Description { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "family", false)]
		public bool IsFamily { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "friend", false)]
		public bool IsFriend { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "public", false)]
		public bool IsPublic { get; set; }

		[ValueArgument(typeof (SafetyLevel), Argument.UnsetShortNameChar, "safety")]
		public SafetyLevel SafetyLevel { get; set; }

		[ValueArgument(typeof (HiddenFromSearch), Argument.UnsetShortNameChar, "search")]
		public HiddenFromSearch SearchState { get; set; }

		[ValueArgument(typeof (string), Argument.UnsetShortNameChar, "tags")]
		public string Tags { get; set; }

		[ValueArgument(typeof (string), Argument.UnsetShortNameChar, "title")]
		public string Title { get; set; }

		public void Execute()
		{
			if (!ModeChosen)
				throw new InvalidOperationException("Invalid mode");

			if (!FileTypes?.Any() ?? false)
			{
				FileTypes = new[]
				{
					"jpg", "jpeg", "png", "gif", "mp4", "avi", "wmv", "mov", "mpeg", "m2ts",
					"3gp", "ogg", "ogv"
				};
			}

			var mgr = new FlickrManager();
			AccessToken token;
			try
			{
				using (var stream = KeyFile.OpenRead())
				{
					token = stream.Load<AccessToken>();
				}
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error loading token '{KeyFile.FullName}'. {ex.Message}");
			}
			try
			{
				mgr.ApplyToken(token);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Supplied token is invalid. {ex.Message}");
			}

			var ct = new CancellationToken();
			var uploadMgr = new UploadManager(ct, mgr)
			{
				MaxConcurrentOperations = MaxConcurrentOperations,
				UpdateDuplicates = UpdateDuplicates,
				CheckOnly = CheckOnly,
				LogAction = Console.WriteLine
			};
			var list = new List<PhotoModel>(1000);
			//var di = new DirectoryInfo(cmdLine.RootDirectory);
			var di = RootDirectory;
			FillPhotosFromPath(di, list, (di.Parent?.FullName.Length ?? -1) + 1);
			SetFromCommandline(mgr, list);
			Console.WriteLine($"Starting to process {list.Count} files...");
			//Console.ReadLine();
			uploadMgr.AddRange(list);
			uploadMgr.ProcessAsync().Wait(ct);
		}

		private void SetFromCommandline(FlickrManager mgr, List<PhotoModel> list)
		{
			var f = mgr.Surrogate;
			var sets = f.PhotosetsGetList(mgr.AccountDetails.UserId);
			var knownSets = new Hashtable(50);
			foreach (var s in sets.Select(s => new PhotosetModel(s.PhotosetId, s.Title, s.Description)))
			{
				knownSets.Add(s.Title.ToLowerInvariant(), s);
			}
			var tagReplace = new Dictionary<string, string> {{",", " "}, {"  ", " "}};

			foreach (var item in list)
			{
				item.Tags = "\"" + string.Join("\",\"", Expand(Tags, item, tagReplace).Split(',')) + "\"";
				item.Title = Expand(Title, item) ?? item.Filename;
				item.ContentType = ContentType;
				item.Description = Expand(Description, item);
				item.SafetyLevel = SafetyLevel;
				item.SearchState = SearchState;
				item.IsFamily = IsFamily;
				item.IsFriend = IsFriend;
				item.IsPublic = IsPublic;

				foreach (var setNameExpression in AddToSets)
				{
					var setName = Expand(setNameExpression, item);
					var key = setName.ToLowerInvariant();
					var set = knownSets.ContainsKey(key) ? (PhotosetModel) knownSets[key] : null;
					if (set == null)
					{
						if (AutoCreateSets)
						{
							set = new PhotosetModel(null, setName, null);
							knownSets.Add(key, set);
						}
						else
						{
							var names = string.Join("', '", sets.Select(s => s.Title));
							throw new InvalidOperationException(
								$"The Album named '{setName}' was not found on the server. Possible names are: '{names}'");
						}
					}
					item.Sets.Add(set);
				}
			}
		}

		private string Expand(string expression, PhotoModel item, Dictionary<string, string> inlineReplace = null)
		{
			if (string.IsNullOrEmpty(expression))
				return null;

			var expanded = ExpressionRegex.Replace(expression, m =>
			{
				string output;
				switch (m.Groups["name"].Value.ToLowerInvariant())
				{
					case "now":
						output = DateTime.Now.ToString("R");
						break;
					case "folder":
						output = new FileInfo(item.LocalPath).Directory?.Name;
						break;
					case "path":
						output = Path.GetDirectoryName(item.LocalPath);
						break;
					case "relrootfolder":
					{
						var path = Path.GetDirectoryName(item.LocalPath) ?? item.LocalPath;
						output = path.Length == RootDirectory.FullName.Length
							? string.Empty
							: path.Substring(RootDirectory.FullName.Length + 1).Split(Path.PathSeparator)[0];
						break;
					}
					case "relpath":
					{
						var path = Path.GetDirectoryName(item.LocalPath) ?? item.LocalPath;
						output = path.Length == RootDirectory.FullName.Length
							? string.Empty
							: path.Substring(RootDirectory.FullName.Length + 1);
						break;
					}
					case "relpathastags":
					{
						var path = Path.GetDirectoryName(item.LocalPath) ?? item.LocalPath;
						output = path.Length == RootDirectory.FullName.Length
							? string.Empty
							: path.Substring(RootDirectory.FullName.Length + 1).Replace(Path.PathSeparator, ',');
						break;
					}
					default:
						output = m.Groups["exp"].Value;
						break;
				}
				if (!string.IsNullOrEmpty(output) && inlineReplace != null)
				{
					foreach (var i in inlineReplace)
					{
						output = output.Replace(i.Key, i.Value);
					}
				}
				return output;
			});
			return expanded;
		}

		private void FillPhotosFromPath(DirectoryInfo di, List<PhotoModel> list, int prefixLen)
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
					var ext = f.Extension.ToLowerInvariant().Substring(1); //remove .
					if (FileTypes.Any(t => t.Equals(ext, StringComparison.InvariantCultureIgnoreCase)))
					{
						var item = new PhotoModel {LocalPath = f.FullName};
						list.Add(item);
						added++;
					}
				}
				Console.WriteLine($"{added} files queued");
				var dirs = di.GetDirectories();
				foreach (var dir in dirs)
				{
					q.Enqueue(dir);
				}
			}
		}
	}
}