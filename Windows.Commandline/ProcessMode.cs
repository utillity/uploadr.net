using System;
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
	internal class ProcessMode : ModeBase
	{
		private static readonly Regex ExpressionRegex = new Regex(@"(?in)(?<exp>\{(?<name>[^}]+)\})");

		[SwitchArgument(Argument.UnsetShortNameChar, "process", false, Optional = false,
			Description = "Executes Process Mode, which uploads local files to Flickr")]
		public override bool ModeChosen { get; set; }

		[DirectoryArgument('s', "source", Optional = false, DirectoryMustExist = true,
			Description = "The source directory to scan (will also scan all subdirectories below it")]
		public DirectoryInfo RootDirectory { get; set; }

		[ValueArgument(typeof (string), Argument.UnsetShortNameChar, "types", Optional = true, AllowMultiple = true,
			Description = "The file-types to process. Use 'videos' and 'photos', or individual extension (ie 'png jpg jpeg')")]
		public string[] FileTypes { get; set; }

		[FileArgument('k', "key", Optional = false, FileMustExist = true)]
		public override FileInfo KeyFile { get; set; }

		[ValueArgument(typeof (string), 'a', "album", AllowMultiple = true,
			Description = "Use either the album-name, or ID:<albumId>. AlbumID can be retrieved using LIST mode.")]
		public string[] AddToSets { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "createAlbums", false)]
		public bool AutoCreateSets { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "updateonly", false,
			Description = "Only process existing flickr objects, don't upload new media files")]
		public bool UpdateOnly { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "checkonly", false)]
		public bool CheckOnly { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "checkfordups", false)]
		public bool CheckForDuplicates { get; set; }

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

		[ValueArgument(typeof (string), Argument.UnsetShortNameChar, "title",
			DefaultValue = "{fname}")]
		public string Title { get; set; }

		public override void Execute()
		{
			if (!ModeChosen)
				throw new InvalidOperationException("Invalid mode");

			if (FileTypes == null || !FileTypes.Any())
			{
				FileTypes = UploadManager.PhotoFormats
					.Concat(UploadManager.VideoFormats)
					.ToArray();
			}
			if (FileTypes.Any(t => t.ToLowerInvariant() == "photos"))
			{
				FileTypes = FileTypes
					.Where(t => t.ToLowerInvariant() != "photos")
					.Concat(UploadManager.PhotoFormats)
					.ToArray();
			}
			if (FileTypes.Any(t => t.ToLowerInvariant() == "videos"))
			{
				FileTypes = FileTypes
					.Where(t => t.ToLowerInvariant() != "videos")
					.Concat(UploadManager.VideoFormats)
					.ToArray();
			}

			var mgr = LoadToken();

			var ct = new CancellationToken();
			var uploadMgr = new UploadManager(ct, mgr)
			{
				MaxConcurrentOperations = MaxConcurrentOperations,
				UpdateDuplicates = UpdateDuplicates,
				CheckOnly = CheckOnly,
				CheckForDuplicates = CheckForDuplicates,
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
			var knownSets = new List<PhotosetModel>(50);
			foreach (var s in sets.Select(s => new PhotosetModel(s.PhotosetId, s.Title, s.Description)))
			{
				knownSets.Add(s);
			}
			var tagReplace = new Dictionary<string, string> {{",", " "}, {"  ", " "}};

			foreach (var item in list)
			{
				item.Tags = Expand(Tags, item)?.Split(',');
				item.Title = Expand(Title, item);
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
					var keyIsId = key.StartsWith("id:");
					if (keyIsId)
					{
						key = key.Substring(3);
					}
					PhotosetModel set;
					var candidates = knownSets.Where(s =>
						(keyIsId && s.Id.ToLowerInvariant() == key)
						|| (!keyIsId && s.Title.ToLowerInvariant() == key))
						.ToArray();
					if (candidates.Length > 1)
					{
						var albums = string.Join(", ", sets.Select(s => $"'{s.Title}' (ID:{s.PhotosetId})"));
						throw new InvalidOperationException(
							$"Multiple albums with '{setName}' were found on the server. Please use the ID instead (ID:<id> syntax). Possible albums are: {albums}");
					}
					if (!candidates.Any())
					{
						if (keyIsId)
						{
							var albums = string.Join(", ", sets.Select(s => $"'{s.Title}' (ID:{s.PhotosetId})"));
							throw new InvalidOperationException(
								$"The Album with ID '{key}' was not found on the server. Possible albums are: {albums}");
						}
						if (AutoCreateSets)
						{
							set = new PhotosetModel(null, setName, null);
							knownSets.Add(set);
						}
						else
						{
							var albums = string.Join(", ", sets.Select(s => $"'{s.Title}' (ID:{s.PhotosetId})"));
							throw new InvalidOperationException(
								$"The Album with ID or named '{setName}' was not found on the server. Possible albums are: {albums}");
						}
					}
					else
					{
						set = candidates.Single();
					}
					item.Sets.Add(set);
				}
			}
		}

		internal string Expand(string expression, PhotoModel item, Dictionary<string, string> inlineReplace = null)
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
					case "fname":
						output = Path.GetFileNameWithoutExtension(item.LocalPath);
						break;
					case "fnameandext":
						output = Path.GetFileName(item.LocalPath);
						break;
					case "relrootfolder":
					{
						var path = Path.GetDirectoryName(item.LocalPath) ?? item.LocalPath;
						output = path.Length == RootDirectory.FullName.Length
							? string.Empty
							: path.Substring(RootDirectory.FullName.Length + 1).Split(Path.DirectorySeparatorChar)[0];
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
							: path.Substring(RootDirectory.FullName.Length + 1)
								.Replace(',', '_')
								.Replace(Path.DirectorySeparatorChar, ',');
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