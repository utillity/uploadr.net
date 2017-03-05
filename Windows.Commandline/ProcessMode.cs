using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
		private readonly Dictionary<string, ExpandItem> _expandItems;

		private readonly object _queueFileLock = new object();
		private decimal _lastMaxBandwidth;

		private DateTime _qLastSaved;

		public ProcessMode()
		{
			var l = new HashSet<ExpandItem>
			{
				new ExpandItem("now", "the current date/time in ISO format", i => DateTime.Now.ToString("R"))
				,
				new ExpandItem("folder", "the immediate folder name of the media file",
					i => new FileInfo(i.LocalPath).Directory?.Name)
				,
				new ExpandItem("path", "the complete path (without filename) of the media file",
					i => Path.GetDirectoryName(i.LocalPath))
				,
				new ExpandItem("fname", "the filename of the media file", i => Path.GetFileNameWithoutExtension(i.LocalPath))
				,
				new ExpandItem("fnameAndExt", "the filename with extension of the media file", i => Path.GetFileName(i.LocalPath))
				,
				new ExpandItem("relRootFolder", "the first folder relative from the root folder specified", i =>
				{
					var path = Path.GetDirectoryName(i.LocalPath) ?? i.LocalPath;
					return path.Length == RootDirectory.FullName.Length
						? string.Empty
						: path.Substring(RootDirectory.FullName.Length + 1).Split(Path.DirectorySeparatorChar)[0];
				})
				,
				new ExpandItem("relpath", "the path relative from the root folder specified", i =>
				{
					var path = Path.GetDirectoryName(i.LocalPath) ?? i.LocalPath;
					return path.Length == RootDirectory.FullName.Length
						? string.Empty
						: path.Substring(RootDirectory.FullName.Length + 1);
				})
				,
				new ExpandItem("relpathastags", "the path relative from the root folder specified, in tag notation", i =>
				{
					var path = Path.GetDirectoryName(i.LocalPath) ?? i.LocalPath;
					return path.Length == RootDirectory.FullName.Length
						? string.Empty
						: path.Substring(RootDirectory.FullName.Length + 1)
							.Replace(',', '_')
							.Replace(Path.DirectorySeparatorChar, ',');
				})
			};

			_expandItems = l.ToDictionary(i => i.Key.ToLowerInvariant());
		}

		[SwitchArgument(Argument.UnsetShortNameChar, "process", false, Optional = false,
			Description = "Executes Process Mode, which uploads local files to Flickr")]
		public override bool ModeChosen { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "showargs", false,
			Description = "Show parsed arguments (with a pause, before starting to process)")]
		public override bool ShowParsedArgs { get; set; }

		[DirectoryArgument('s', "source", Optional = false, DirectoryMustExist = true,
			Description = "The source directory to scan (will also scan all subdirectories below it)")]
		public DirectoryInfo RootDirectory { get; set; }

		[FileArgument(Argument.UnsetShortNameChar, "queue", Optional = true, FileMustExist = false,
			Description =
				"If the file exists, the queue (files to process) is loaded from it. The queue is also periodically saved to this file"
			)]
		public FileInfo QueueFile { get; set; }

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

		[ValueArgument(typeof (decimal), Argument.UnsetShortNameChar, "mbpersec")]
		public decimal MaxBandwidthMb { get; set; }

		[ValueArgument(typeof (byte), Argument.UnsetShortNameChar, "parallelism", DefaultValue = (byte) 20)]
		public byte MaxConcurrentOperations { get; set; } = 20;

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

		public override string GetAdditionalUsageHints()
		{
			RootDirectory = new DirectoryInfo(@"c:\Images");
			var pi = new PhotoModel {LocalPath = @"c:\Images\2001\Vacation\image1.jpg"};
			var sb = new StringBuilder(500);
			sb.AppendLine("The following expressions can be used within --title, --desc, and --tags:");
			sb.AppendLine($"Example with a --source of '{RootDirectory.FullName}' and an image located in '{pi.LocalPath}':");
			foreach (var i in _expandItems.Values)
			{
				sb.AppendLine($"{{{i.Key}}}:");
				sb.AppendLine($"    {i.Description}");
				sb.AppendLine($"    Example output: {i.Expand(pi)}");
			}
			return sb.ToString();
		}

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

			SetMaxBandwidth(MaxBandwidthMb);

			var mgr = LoadToken();

			var cts = new CancellationTokenSource();
			var ct = cts.Token;
			var uploadMgr = new UploadManager(ct, mgr)
			{
				MaxConcurrentOperations = MaxConcurrentOperations,
				UpdateDuplicates = UpdateDuplicates,
				UpdateOnly = UpdateOnly,
				CheckOnly = CheckOnly,
				CheckForDuplicates = CheckForDuplicates,
				LogAction = msg => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {msg}")
			};
			var list = TryLoadQueue() ?? new HashSet<PhotoModel>();
			var di = RootDirectory;
			FillPhotosFromPath(di, list, (di.Parent?.FullName.Length ?? -1) + 1);
			SetFromCommandline(mgr, list);
			Console.WriteLine($"Starting to process {list.Count} files...");
			PrintHelp();
			//Console.ReadLine();
			uploadMgr.AddRange(list);
			SaveQueue(list);
			var task = uploadMgr.ProcessAsync();
			var isPaused = false;
			while (!task.IsCompleted || isPaused)
			{
				Thread.Sleep(50);
				if (DateTime.Now.Subtract(_qLastSaved).TotalMinutes > 1)
					SaveQueue(list);

				if (Console.KeyAvailable)
				{
					var x = Console.ReadKey(true);
					switch (x.KeyChar)
					{
						case '?':
							PrintHelp();
							break;
						case '0':
							SetMaxBandwidth(0);
							break;
						case '1':
							SetMaxBandwidth(1);
							break;
						case '2':
							SetMaxBandwidth(2);
							break;
						case '3':
							SetMaxBandwidth(3);
							break;
						case '4':
							SetMaxBandwidth(4);
							break;
						case '5':
							SetMaxBandwidth(5);
							break;
						case '6':
							SetMaxBandwidth(6);
							break;
						case '7':
							SetMaxBandwidth(7);
							break;
						case '8':
							SetMaxBandwidth(8);
							break;
						case '9':
							SetMaxBandwidth(9);
							break;
						case '+':
							SetMaxBandwidth(_lastMaxBandwidth, 0.5m);
							break;
						case '-':
							SetMaxBandwidth(_lastMaxBandwidth, -0.5m);
							break;
						case 'p':
						case 'P':
							if (isPaused)
							{
								Console.WriteLine("*** Continuing Processing...");
								task = uploadMgr.ProcessAsync();
								isPaused = false;
							}
							else
							{
								Console.WriteLine("*** Pausing Processing...");
								isPaused = true;
								cts.Cancel();
								while (!task.IsCompleted)
									Thread.Sleep(10);
								Console.WriteLine("*** Processing paused! Press P again to continue ***");
								SaveQueue(list);
							}
							break;
						case 'q':
						case 'Q':
							if (!isPaused)
							{
								Console.WriteLine("*** Aborting Processing...");
								cts.Cancel();
								while (!task.IsCompleted)
									Thread.Sleep(10);
							}
							Console.WriteLine("*** Processing aborted! ***");
							break;
					}
				}
			}
			SaveQueue(list);
		}

		private static void PrintHelp()
		{
			Console.WriteLine("Use the following commands (lowercase keys):");
			Console.WriteLine("  ?    = print this cheat-sheet");
			Console.WriteLine("  P    = pause processing");
			Console.WriteLine("  Q    = abort processing");
			Console.WriteLine("  0    = no speedlimit");
			Console.WriteLine("  1..9 = [x]mbit speedlimit");
			Console.WriteLine("  +    = +0.5mbit speedlimit");
			Console.WriteLine("  -    = -0.5mbit speedlimit");
		}

		private HashSet<PhotoModel> TryLoadQueue()
		{
			if (!QueueFile.Exists)
				return null;
			lock (_queueFileLock)
			{
				using (var stream = QueueFile.OpenRead())
				{
					Console.WriteLine("*** Loading Queue...");
					var list = stream.Load<HashSet<PhotoModel>>(null);
					var knownSets = new Dictionary<string, PhotosetModel>();
					foreach (var i in list)
					{
						switch (i.State)
						{
							case ProcessingStateType.Failed:
								//TODO: check, if we should retry failed
								//i.State = ProcessingStateType.Pending;
								break;
							case ProcessingStateType.Uploading:
								//not sure if uploaded! restart
								i.State = ProcessingStateType.Pending;
								break;
							//case ProcessingStateType.Pending:
							//case ProcessingStateType.Duplicate:
							//case ProcessingStateType.ReadyToUpload:
							//case ProcessingStateType.Uploaded:
							//case ProcessingStateType.Retry:
							//case ProcessingStateType.Completed:
							//default:
							//	break;
						}
						var sets = i.Sets.ToArray();
						foreach (var s in sets)
						{
							if (!knownSets.ContainsKey(s.Key))
							{
								knownSets.Add(s.Key, s);
							}
							else
							{
								i.Sets.Remove(s);
								i.Sets.Add(knownSets[s.Key]);
							}
						}
					}
					Console.WriteLine($"*** Loaded {list.Count} items into Queue ***");
					foreach (var g in list.GroupBy(i => i.State))
					{
						var spaces = 15 - g.Key.ToString().Length;
						Console.WriteLine($"    {g.Key}:{new string(' ', spaces)}{g.Count()}");
					}
					return list;
				}
			}
		}

		private void SaveQueue(HashSet<PhotoModel> list)
		{
			if (QueueFile == null)
				return;
			lock (_queueFileLock)
			{
				var tmpFile = new FileInfo(QueueFile.FullName + ".tmp");
				using (var stream = tmpFile.OpenWrite())
				{
					Console.WriteLine("*** Saving Queue...");
					list.ToXml(stream, Encoding.Default, null);
					_qLastSaved = DateTime.Now;
					QueueFile.Refresh();
					if (QueueFile.Exists)
						QueueFile.Delete();
					tmpFile.MoveTo(QueueFile.FullName);
					QueueFile.Refresh();
					Console.WriteLine("*** Queue saved ***");
				}
			}
		}

		private void SetMaxBandwidth(decimal mbit, decimal relative = 0)
		{
			if (relative != 0)
			{
				if (_lastMaxBandwidth <= 0)
					return;
				mbit = _lastMaxBandwidth + relative;
			}
			if (mbit <= 0)
			{
				ThrottledStream.MaxBytesPerSecond = int.MaxValue;
				Console.WriteLine("Processing at full upload speed");
			}
			else
			{
				//take 95% of value to account for API overhead
				ThrottledStream.MaxBytesPerSecond = (int) (mbit*1024m*1024m/8m*0.95m);
				Console.WriteLine($"Processing at {mbit:N1}mbit ({ThrottledStream.MaxBytesPerSecond:N} bytes/sec.)");
			}
			_lastMaxBandwidth = mbit;
		}

		private void SetFromCommandline(FlickrManager mgr, HashSet<PhotoModel> list)
		{
			var f = mgr.Surrogate;
			var sets = f.PhotosetsGetList(mgr.AccountDetails.UserId);
			var knownSets = new List<PhotosetModel>(50);
			foreach (var s in sets.Select(s => new PhotosetModel(s.PhotosetId, s.Title, s.Description)))
			{
				knownSets.Add(s);
			}

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

				foreach (var s in item.Sets)
				{
					if (!knownSets.Contains(s))
						knownSets.Add(s);
				}

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
					var candidates =
						knownSets.Where(
							s => (keyIsId && s.Id.ToLowerInvariant() == key) || (!keyIsId && s.Title.ToLowerInvariant() == key)).ToArray();
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
					if (!item.Sets.Contains(set))
						item.Sets.Add(set);
				}
			}
		}

		internal string Expand(string expression, PhotoModel item)
		{
			if (string.IsNullOrEmpty(expression))
				return null;

			var expanded = ExpressionRegex.Replace(expression, m =>
			{
				var key = m.Groups["name"].Value.ToLowerInvariant();
				ExpandItem action;
				var output = _expandItems.TryGetValue(key, out action) ? action.Expand(item) : m.Groups["exp"].Value;
				return output;
			});
			return expanded;
		}

		private void FillPhotosFromPath(DirectoryInfo di, HashSet<PhotoModel> list, int prefixLen)
		{
			Console.Write($"Scanning {di.FullName.Substring(prefixLen)}... ");
			var files = di.GetFiles();
			var added = 0;
			foreach (var f in files)
			{
				var ext = f.Extension.ToLowerInvariant().Substring(1); //remove .
				if (FileTypes.Any(t => t.Equals(ext, StringComparison.InvariantCultureIgnoreCase)))
				{
					var item = new PhotoModel {LocalPath = f.FullName};
					if (!list.Contains(item))
					{
						list.Add(item);
						added++;
					}
				}
			}
			Console.WriteLine($"{added} files queued");
			var dirs = di.GetDirectories();
			foreach (var dir in dirs)
			{
				FillPhotosFromPath(dir, list, prefixLen);
			}
		}
	}
}