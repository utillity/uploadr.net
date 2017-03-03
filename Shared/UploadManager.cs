using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Crc32C;
using FlickrNet;
using uTILLIty.UploadrNet.Windows.Models;

// ReSharper disable UnusedMember.Global

namespace uTILLIty.UploadrNet.Windows
{
	public class UploadManager : NotifyPropertyChangedBase
	{
		private const string FilenameMachineTag = "orig:filename";
		private const string UploaderMachineTag = "orig:uploader";
		private const string CrcMachineTag = "orig:crc32";
		private const string ImportPathMachineTag = "orig:importfrom";
		//we init this once so that if the function is repeatedly called
		//it isn't stressing the garbage man
		private static readonly Regex ColonRegex = new Regex(":");

		public static readonly string[] VideoFormats =
		{
			"mp4", "avi", "wmv", "mov", "mpeg", "m2ts",
			"3gp", "ogg", "ogv"
		};

		public static readonly string[] PhotoFormats =
		{
			"jpg", "jpeg", "png", "gif"
		};

		private readonly CancellationToken _cancellationToken;
		private readonly FlickrManager _mgr;
		private readonly List<PhotoModel> _processQueue = new List<PhotoModel>();

		public UploadManager(CancellationToken cancellationToken, FlickrManager mgr)
		{
			_cancellationToken = cancellationToken;
			_mgr = mgr;
		}

		public int MaxConcurrentOperations
		{
			get { return GetValue(20); }
			set { SetValue(value); }
		}

		public int MaxTries
		{
			get { return GetValue(5); }
			set { SetValue(value); }
		}

		public bool IsBusy
		{
			get { return GetValue<bool>(); }
			private set { SetValue(value); }
		}

		public bool CheckForDuplicates
		{
			get { return GetValue(true); }
			set { SetValue(value); }
		}

		public bool CheckOnly
		{
			get { return GetValue<bool>(); }
			set { SetValue(value); }
		}

		public bool UpdateOnly
		{
			get { return GetValue<bool>(); }
			set { SetValue(value); }
		}

		public Action<string> LogAction
		{
			get { return GetValue<Action<string>>(); }
			set { SetValue(value); }
		}

		public bool UpdateDuplicates
		{
			get { return GetValue(true); }
			set { SetValue(value); }
		}

		public void Add(PhotoModel item)
		{
			if (IsBusy)
				throw new InvalidOperationException("Cannot add items to queue while Upload-Manager is processing the queue");
			_processQueue.Add(item);
		}

		public void AddRange(List<PhotoModel> list)
		{
			if (IsBusy)
				throw new InvalidOperationException("Cannot add items to queue while Upload-Manager is processing the queue");
			_processQueue.AddRange(list);
		}

		private void Log(string msg, PhotoModel item = null)
		{
			Debug.WriteLine(msg);
			LogAction?.Invoke(msg);
			item?.AddMessage(msg);
		}

		public async Task ProcessAsync()
		{
			if (IsBusy)
				return;

			var token = _cancellationToken;
			var timer = new Timer(t =>
			{
				double total = _processQueue.Count;
				double left = _processQueue.Where(ShouldProcess).Count();
				Log($"*** Status-Update: {(total - left)/total*100:N0}% complete - {left} items left to process ***");
			}, null, 60000, 60000);

			try
			{
				IsBusy = true;
				while (true)
				{
					token.ThrowIfCancellationRequested();
					var items = _processQueue.Where(ShouldProcess).ToArray();
					if (!items.Any())
						break;

					Log($"Processing {items.Length} items from queue (with {MaxConcurrentOperations} worker-threads)...");
					await Task.Run(() =>
					{
						try
						{
							var result = Parallel.ForEach(items,
								new ParallelOptions {MaxDegreeOfParallelism = MaxConcurrentOperations, CancellationToken = token},
								ProcessItem);
							while (!result.IsCompleted)
							{
								Thread.Sleep(50);
							}
						}
						catch (OperationCanceledException)
						{
						}
					}, token).ConfigureAwait(false);
				}
			}
			finally
			{
				timer.Dispose();
				IsBusy = false;
			}
		}

		private bool ShouldProcess(PhotoModel item)
		{
			switch (item.State)
			{
				case ProcessingStateType.Retry:
					if (item.RetryCount <= MaxTries)
					{
						item.State = ProcessingStateType.Pending;
						item.AddMessage("Retrying");
						return true;
					}
					item.State = ProcessingStateType.Failed;
					return false;
				case ProcessingStateType.Completed:
				case ProcessingStateType.Failed:
					return false;
				case ProcessingStateType.Duplicate:
					return UpdateDuplicates;
				default:
					//case ProcessingStateType.Pending:
					//case ProcessingStateType.ReadyToUpload:
					//case ProcessingStateType.Uploading:
					//case ProcessingStateType.Uploaded:
					return true;
			}
		}

		private void ProcessItem(PhotoModel item)
		{
			//Parallel.ForEach reuses the threads, and you can't change the name once set!
			//Thread.CurrentThread.Name = $"UploadManager.ProcessItem({item.LocalPath}) background thread";
			var token = _cancellationToken;
			try
			{
				token.ThrowIfCancellationRequested();
				if (!ShouldProcess(item))
					return;

				Debug.WriteLine($"Processing {item.Filename} ({item.State})...");
				item.LogStateChange = true;
				EnsureItemProperties(item);

				if (item.State == ProcessingStateType.Pending)
				{
					if (CheckForDuplicates && CheckIsDuplicate(item))
						item.State = ProcessingStateType.Duplicate;
					else
						item.State = ProcessingStateType.ReadyToUpload;
				}

				if (CheckOnly)
				{
					item.State = ProcessingStateType.Completed;
					return;
				}

				token.ThrowIfCancellationRequested();

				if (item.State == ProcessingStateType.ReadyToUpload)
				{
					if (UpdateOnly)
						item.State = ProcessingStateType.Completed;
					else
					{
						token.ThrowIfCancellationRequested();
						item.State = ProcessingStateType.Uploading;
						if (Upload(item))
							item.State = ProcessingStateType.Uploaded;
					}
				}

				if (item.State == ProcessingStateType.Uploaded)
				{
					AddToSets(item);
					FixDateTakenIfNotInExif(item);
					item.State = ProcessingStateType.Completed;
				}

				if (item.State == ProcessingStateType.Duplicate)
				{
					if (UpdateDuplicates)
					{
						UpdateItem(item);
						AddToSets(item);
						FixDateTakenIfNotInExif(item);
					}
					item.State = ProcessingStateType.Completed;
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				item.AddError(ex.Message, ex);
			}
			finally
			{
				item.LogStateChange = false;
				Debug.WriteLine($"Completed {item.Filename} in state ({item.State})");
				switch (item.State)
				{
					case ProcessingStateType.Failed:
					case ProcessingStateType.Retry:
						Log($"{item.Filename} completed in state {item.State}");
						break;
					//case ProcessingStateType.Duplicate:
					//case ProcessingStateType.Completed:
					//case ProcessingStateType.Pending:
					//case ProcessingStateType.ReadyToUpload:
					//case ProcessingStateType.Uploading:
					//case ProcessingStateType.Uploaded:
					//default:
					//	break;
				}
			}
		}

		private void EnsureItemProperties(PhotoModel item)
		{
			if (!item.DateTaken.HasValue)
				SetDateTaken(item);
			if (string.IsNullOrEmpty(item.Crc32))
				CalculateCrc32(item);

			EnsureMachineTags(item);
		}

		private void CalculateCrc32(PhotoModel item)
		{
			var crc = Crc32CAlgorithm.Compute(Encoding.Default.GetBytes(item.LocalPath.ToLowerInvariant()));
			var buffer = new byte[8096];
			using (var stream = File.OpenRead(item.LocalPath))
			{
				while (true)
				{
					var read = stream.Read(buffer, 0, 8096);
					crc = Crc32CAlgorithm.Append(crc, buffer, 0, read);
					if (read < 8096)
					{
						item.Crc32 = crc.ToString();
						return;
					}
				}
			}
		}

		private void EnsureMachineTags(PhotoModel item)
		{
			var tags = new List<string>(item.Tags ?? new string[0]);
			if (!tags.Any(t => t.StartsWith(FilenameMachineTag)))
				// ReSharper disable once PossibleNullReferenceException
				tags.Add(BuildMachineTagExpression(FilenameMachineTag, Path.GetFileName(item.LocalPath).ToLowerInvariant()));

			if (!tags.Any(t => t.StartsWith(ImportPathMachineTag)))
				tags.Add(BuildMachineTagExpression(ImportPathMachineTag,
					item.LocalPath.Replace(Path.DirectorySeparatorChar, '/').ToLowerInvariant()));

			if (!tags.Any(t => t.StartsWith(UploaderMachineTag)))
				tags.Add(BuildMachineTagExpression(UploaderMachineTag, "Uploadr.Net"));

			if (!tags.Any(t => t.StartsWith(CrcMachineTag)))
				tags.Add(BuildMachineTagExpression(CrcMachineTag, item.Crc32));

			//if (!tags.Any(t => "Uploadr.Net".Equals(t)))
			//	tags.Add("Uploadr.Net");

			item.Tags = tags.ToArray();
		}

		private string BuildMachineTagExpression(string tag, string value)
		{
			var expr = $"{tag}=\"{value?.Replace('"', '\'')}\"";
			return expr;
		}

		private void FixDateTakenIfNotInExif(PhotoModel item)
		{
			if (item.HasDateTakenExifProp || !item.DateTaken.HasValue)
				return;

			var f = _mgr.Surrogate;
			var r = item.GetRemoteDetails(f);
			var dt = item.DateTaken.GetValueOrDefault();
			var rdt = r.DateTakenUnknown ? DateTime.MinValue : r.DateTaken.Date.ToUniversalTime();
			if (!dt.ToUniversalTime().Date.Equals(rdt))
			{
				//https://www.flickr.com/services/api/flickr.photos.setDates.html
				try
				{
					f.PhotosSetDates(item.PhotoId, dt, DateGranularity.FullDate);
					Log($"Updated Date-Taken (to {dt:d}) for {item.Filename}", item);
				}
				catch (FlickrApiException ex)
				{
					switch (ex.Code)
					{
						case 6: //Invalid Date Taken. The date taken passed is invalid. It may be in the future or way in the past. 
							Log($"Unable to set Date-Taken (to {dt:d}) for {item.Filename}. {ex.Message}", item);
							return;
					}
					throw;
				}
			}
		}

		public static void SetDateTaken(PhotoModel item)
		{
			var path = item.LocalPath;
			try
			{
				item.DateTaken = GetDateTaken(path);
				item.HasDateTakenExifProp = true;
			}
			catch (ArgumentException) //Property cannot be found.
			{
				//Log($"Date-Taken EXIF Property (36867) for '{path}' could not be found. Using Date-Modified instead");
				item.DateTaken = GetDateModified(path);
				item.HasDateTakenExifProp = false;
			}
			if (!item.HasDateTakenExifProp)
				item.AddMessage("Using date-modified of file as date-taken");
			//Log($"Date-Taken EXIF Property (36867) for '{path}' could not be found. Using Date-Modified instead");
		}

		//private bool IsVideo(string path)
		//{
		//	// ReSharper disable once PossibleNullReferenceException
		//	var ext = Path.GetExtension(path).ToLowerInvariant().Substring(1);
		//	return VideoFormats.Contains(ext);
		//}

		public static DateTime GetDateModified(string path)
		{
			var fi = new FileInfo(path);
			if (!fi.Exists)
				throw new FileNotFoundException($"File {path} was not found. Cannot retrieve date-modified");
			return fi.LastWriteTime;
		}

		/// <exception cref="ArgumentException">Thrown, if EXIF property was not found (&quot;Property cannot be found.&quot;)</exception>
		public static DateTime GetDateTaken(string path)
		{
			if (!File.Exists(path))
				throw new FileNotFoundException($"File {path} was not found. Cannot retrieve date-modified");

			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
			{
				using (var myImage = Image.FromStream(fs, false, false))
				{
					var propItem = myImage.GetPropertyItem(36867);
					var dateTaken = ColonRegex.Replace(Encoding.UTF8.GetString(propItem.Value), "-", 2);
					return DateTime.Parse(dateTaken);
				}
			}
		}

		private void AddToSets(PhotoModel item)
		{
			var f = _mgr.Surrogate;
			foreach (var set in item.Sets)
			{
				try
				{
					var added = false;
					if (string.IsNullOrEmpty(set.Id))
					{
						//multithreaded! don't create album multiple times!
						lock (set)
						{
							if (string.IsNullOrEmpty(set.Id))
							{
								Log($"Creating new Set '{set.Title}'");
								var newSet = f.PhotosetsCreate(set.Title, item.PhotoId);
								set.Id = newSet.PhotosetId;
								Log($"Created new Set '{set.Title}'");
								added = true;
							}
						}
					}

					if (!added)
					{
						f.PhotosetsAddPhoto(set.Id, item.PhotoId);
					}
					Log($"Added {item.Filename} to Set '{set.Title}'", item);
				}
				catch (FlickrApiException ex)
				{
					//Photo already in set (3)
					if (ex.Code == 3)
						return;
				}
				catch (OperationCanceledException)
				{
				}
				catch (Exception ex)
				{
					item.AddError($"Error adding photo to Set {set.Title}: {ex.Message}", ex);
					return;
				}
			}
		}

		private bool CheckIsDuplicate(PhotoModel item)
		{
			try
			{
				var f = _mgr.Surrogate;
				var title = item.Title ?? item.Filename;
				var crcTag = BuildMachineTagExpression(CrcMachineTag, item.Crc32);
				var query = new PhotoSearchOptions(_mgr.AccountDetails.UserId)
				{
					Extras = PhotoSearchExtras.Tags | PhotoSearchExtras.DateTaken,
					MachineTagMode = MachineTagMode.AnyTag,
					MachineTags =
						// ReSharper disable once PossibleNullReferenceException
						BuildMachineTagExpression(FilenameMachineTag, Path.GetFileName(item.LocalPath).ToLowerInvariant()) + " " + crcTag
				};
				var result = f.PhotosSearch(query);
				if (result.Count == 0)
				{
					query = new PhotoSearchOptions(_mgr.AccountDetails.UserId)
					{
						Text = title
					};
					result = f.PhotosSearch(query);
				}

				if (!result.Any())
				{
					return false;
				}
				var dlm = item.DateTaken;
				var dupItem = result.Count == 1
					? result[0]
					: result.FirstOrDefault(
						d => !d.DateTakenUnknown && dlm.HasValue && d.DateTaken.Subtract(dlm.Value).TotalSeconds < 1) ??
					  result.FirstOrDefault(d => d.DateTakenUnknown);
				if (dupItem != null)
				{
					var r = dupItem;
					var rCrcTag = r.Tags.FirstOrDefault(t => t.StartsWith(CrcMachineTag));
					if (rCrcTag != null && crcTag.Replace("\"", "").Equals(rCrcTag, StringComparison.InvariantCultureIgnoreCase))
					{
						item.AddMessage("Duplicate matched by CRC");
						item.PhotoId = dupItem.PhotoId;
						return true;
					}
					if (rCrcTag == null && dupItem.Title.Equals(item.Title, StringComparison.InvariantCultureIgnoreCase))
					{
						item.AddMessage("Duplicate matched by Title");
						item.PhotoId = dupItem.PhotoId;
						return true;
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error executing search-query. {ex.Message}", ex);
			}
			return false;
		}

		private void UpdateItem(PhotoModel item)
		{
			var l = item;
			var f = _mgr.Surrogate;
			var r = item.GetRemoteDetails(f);

			if (!string.IsNullOrEmpty(l.Title) || !string.IsNullOrEmpty(l.Description))
			{
				if (l.Title != r.Title) //bug: r.Description always null?! || l.Description != r.Description)
				{
					f.PhotosSetMeta(l.PhotoId, l.Title, l.Description);
					Log($"Updated title/description of {l.Filename} " +
					    $"from Title={r.Title} Description={r.Description} " +
					    $"to Title={l.Title} Description={l.Description}", item);
				}
			}

			//Tags on server don't contain quotes anymore!
			if (
				l.Tags?.Any(t => !r.Tags.Any(rt => rt.Raw.Equals(t.Replace("\"", ""), StringComparison.CurrentCultureIgnoreCase))) ??
				false)
			{
				//var allTags = r.Tags.Any()
				//	? (bool) l.Tags?.Any()
				//		? r.Tags.Select(t => t.Raw).Concat(l.Tags).Distinct().ToArray()
				//		: r.Tags.Select(t => t.Raw).ToArray()
				//	: l.Tags;
				f.PhotosSetTags(l.PhotoId, l.Tags);
				Log($"Updated tags of {l.Filename} " + $"from '{string.Join("','", r.Tags.Select(t => t.Raw))}' " +
				    $"to '{string.Join("','", l.Tags.Select(t => t.Replace("\"", "")))}'", item);
			}

			if (l.IsFamily != r.IsFamily || l.IsPublic != r.IsPublic || l.IsFriend != r.IsFriend)
			{
				f.PhotosSetPerms(l.PhotoId, l.IsPublic, l.IsFriend, l.IsFamily, r.PermissionComment.GetValueOrDefault(),
					r.PermissionAddMeta.GetValueOrDefault());
				Log($"Updated rights/visibility of {l.Filename} " +
				    $"from IsPublic={r.IsPublic} IsFriend={r.IsFriend} IsFamily={r.IsFamily} " +
				    $"to IsPublic={l.IsPublic} IsFriend={l.IsFriend} IsFamily={l.IsFamily}", item);
			}
			if (l.SafetyLevel != r.SafetyLevel)
			{
				f.PhotosSetSafetyLevel(l.PhotoId, l.SafetyLevel, l.SearchState);
				Log($"Updated safety-level of {l.Filename} " +
				    $"from SafetyLevel.{r.SafetyLevel} (unknown SearchState) " +
				    $"to SafetyLevel.{l.SafetyLevel} SearchState.{l.SearchState}", item);
			}
		}

		private bool Upload(PhotoModel item)
		{
			if (!string.IsNullOrEmpty(item.PhotoId))
			{
				return true;
			}

			using (var stream = new ThrottledStream(File.OpenRead(item.LocalPath)))
			{
				var fname = item.Filename;
				if (string.IsNullOrEmpty(item.Title))
					item.Title = fname;

				try
				{
					var tags = item.Tags == null ? null : "\"" + string.Join("\",\"", item.Tags) + "\"";
					var f = _mgr.Surrogate;
					//https://www.flickr.com/services/api/upload.api.html
					item.PhotoId = f.UploadPicture(stream, fname, item.Title, item.Description, tags, item.IsPublic, item.IsFamily,
						item.IsFriend, item.ContentType, item.SafetyLevel, item.SearchState);
					Log($"{item.Filename} uploaded (remote ID={item.PhotoId})");
				}
				catch (OperationCanceledException)
				{
				}
				catch (FlickrApiException ex)
				{
					//Filetype was not recognised (5)
					if (ex.Code == 5)
					{
						item.AddError($"File not recognized by Flickr (file corrupt?): {ex.Message}", ex);
						item.State = ProcessingStateType.Failed;
						return false;
					}
					item.AddError($"Error uploading photo: {ex.Message}", ex);
					return false;
				}
				catch (Exception ex)
				{
					item.AddError($"Error uploading photo: {ex.Message}", ex);
					return false;
				}
			}
			return true;
		}
	}
}