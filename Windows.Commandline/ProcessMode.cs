﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CommandLineParser.Arguments;
using FlickrNet;
using uTILLIty.UploadrNet.Windows.Models;

namespace uTILLIty.UploadrNet.Windows
{
	internal class ProcessMode
	{
		[SwitchArgument(Argument.UnsetShortNameChar, "process", false, Optional = false,
			Description = "Executes Process Mode, which uploads local files to Flickr")]
		public bool ModeChosen { get; set; }

		[DirectoryArgument('s', "source", Optional = false, DirectoryMustExist = true,
			Description = "The source directory to scan (will also scan all subdirectories below it")]
		public DirectoryInfo RootDirectory { get; set; }

		[ValueArgument(typeof(string), Argument.UnsetShortNameChar, "types", Optional = true, AllowMultiple = true,
			Description = "The file-types to process")]
		public string[] FileTypes { get; set; }

		[FileArgument('k', "key", Optional = false, FileMustExist = true)]
		public FileInfo KeyFile { get; set; }

		[ValueArgument(typeof(string), 'a', "album", AllowMultiple = true)]
		public string[] AddToSets { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "createAlbums", false)]
		public bool AutoCreateSets { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "checkonly", false)]
		public bool CheckOnly { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "updatedups", false)]
		public bool UpdateSetsOfDuplicates { get; set; }

		[ValueArgument(typeof(ContentType), Argument.UnsetShortNameChar, "ctype")]
		public ContentType ContentType { get; set; }

		[ValueArgument(typeof(byte), Argument.UnsetShortNameChar, "parallelism", DefaultValue = (byte)10)]
		public byte MaxConcurrentOperations { get; set; } = 10;

		[ValueArgument(typeof(string), Argument.UnsetShortNameChar, "desc")]
		public string Description { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "family", false)]
		public bool IsFamily { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "friend", false)]
		public bool IsFriend { get; set; }

		[SwitchArgument(Argument.UnsetShortNameChar, "public", false)]
		public bool IsPublic { get; set; }

		[ValueArgument(typeof(SafetyLevel), Argument.UnsetShortNameChar, "safety")]
		public SafetyLevel SafetyLevel { get; set; }

		[ValueArgument(typeof(HiddenFromSearch), Argument.UnsetShortNameChar, "search")]
		public HiddenFromSearch SearchState { get; set; }

		[ValueArgument(typeof(string), Argument.UnsetShortNameChar, "tags")]
		public string Tags { get; set; }

		[ValueArgument(typeof(string), Argument.UnsetShortNameChar, "title")]
		public string Title { get; set; }

		public void Execute()
		{
			if (!ModeChosen)
				throw new InvalidOperationException("Invalid mode");

			var mgr = new FlickrManager();
			AccessToken token = null;
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
				UpdateSetsOfDuplicates = UpdateSetsOfDuplicates,
				CheckOnly = CheckOnly,
				LogAction = Console.WriteLine
			};
			var list = new List<PhotoModel>(1000);
			//var di = new DirectoryInfo(cmdLine.RootDirectory);
			var di = RootDirectory;
			FillPhotosFromPath(di, list, di.Parent.FullName.Length + 1);
			SetFromCommandline(mgr, list);
			Console.WriteLine($"Ready to process {list.Count} photos.");
			//Console.ReadLine();
			uploadMgr.AddRange(list);
			uploadMgr.ProcessAsync().Wait(ct);
		}

		private void SetFromCommandline(FlickrManager mgr, List<PhotoModel> list)
		{
			var f = mgr.Surrogate;
			var sets = f.PhotosetsGetList(mgr.AccountDetails.UserId);
			var setsToAdd = new List<PhotosetModel>();
			foreach (var setName in AddToSets)
			{
				var set = sets.FirstOrDefault(s => s.Title.Equals(setName, StringComparison.CurrentCultureIgnoreCase));
				if (set == null)
				{
					if (AutoCreateSets)
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
				item.Tags = Tags;
				item.Title = Title ?? item.Filename;
				item.ContentType = ContentType;
				item.Description = Description;
				item.SafetyLevel = SafetyLevel;
				item.SearchState = SearchState;
				item.IsFamily = IsFamily;
				item.IsFriend = IsFriend;
				item.IsPublic = IsPublic;
				foreach (var s in setsToAdd)
					item.Sets.Add(s);
			}
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
					//if (list.Count > 100)
					//	break;
					var ext = f.Extension.ToLowerInvariant().Substring(1); //remove .
					if (FileTypes.Any(t => t.Equals(ext, StringComparison.InvariantCultureIgnoreCase)))
					{
						var item = new PhotoModel { LocalPath = f.FullName };
						list.Add(item);
						added++;
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