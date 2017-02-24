﻿using System;
using System.Collections.Generic;
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
		private readonly List<ProcessingItem> _processQueue = new List<ProcessingItem>();

		public UploadManager(CancellationToken cancellationToken, FlickrManager mgr)
		{
			_cancellationToken = cancellationToken;
			_mgr = mgr;
		}

		public int MaxConcurrentOperations
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
			_processQueue.Add(new ProcessingItem(item));
		}

		public void AddRange(List<PhotoModel> list)
		{
			if (IsBusy)
				throw new InvalidOperationException("Cannot add items to queue while Upload-Manager is processing the queue");
			_processQueue.AddRange(list.Select(i => new ProcessingItem(i)));
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
				LogAction($"Status-Update: {(total - left)/total*100:N0}% complete - {left} items left to process");
			}, null, 60000, 60000);

			try
			{
				IsBusy = true;
				while (true)
				{
					if (token.IsCancellationRequested)
						return;

					var items = _processQueue.Where(ShouldProcess).ToArray();
					if (!items.Any() || token.IsCancellationRequested)
						break;

					LogAction($"Processing {items.Length} items from queue...");
					var result = Parallel.ForEach(items,
						new ParallelOptions {MaxDegreeOfParallelism = MaxConcurrentOperations, CancellationToken = token},
						ProcessItem);
					await Task.Run(() =>
					{
						while (!result.IsCompleted)
						{
							Thread.Sleep(50);
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

		private bool ShouldProcess(ProcessingItem item)
		{
			switch (item.Item.State)
			{
				case ProcessingStateType.Retry:
					if (item.RetryCount <= 3)
					{
						item.Item.State = ProcessingStateType.Pending;
						item.AddMessage("Retrying");
						return true;
					}
					item.Item.State = ProcessingStateType.Failed;
					return false;
				case ProcessingStateType.Success:
				case ProcessingStateType.Failed:
					return false;
				default:
					//case ProcessingStateType.Pending:
					//case ProcessingStateType.Duplicate:
					//case ProcessingStateType.ReadyToUpload:
					//case ProcessingStateType.Uploading:
					//case ProcessingStateType.Uploaded:
					return true;
			}
		}

		private void ProcessItem(ProcessingItem item)
		{
			var token = _cancellationToken;
			try
			{
				if (token.IsCancellationRequested)
					return;
				if (!ShouldProcess(item))
					return;

				LogAction($"Processing {item.Item.Filename} ({item.Item.State})...");
				EnsureItemProperties(item);

				if (item.Item.State == ProcessingStateType.Pending)
				{
					if (CheckForDuplicates && CheckIsDuplicate(item))
						item.Item.State = ProcessingStateType.Duplicate;
					else
						item.Item.State = ProcessingStateType.ReadyToUpload;
				}

				if (CheckOnly)
				{
					item.Item.State = ProcessingStateType.Success;
					return;
				}

				if (token.IsCancellationRequested)
					return;

				if (item.Item.State == ProcessingStateType.ReadyToUpload)
				{
					if (UpdateOnly)
						item.Item.State = ProcessingStateType.Success;
					else
					{
						if (token.IsCancellationRequested)
							return;
						item.Item.State = ProcessingStateType.Uploading;
						if (Upload(item))
							item.Item.State = ProcessingStateType.Uploaded;
					}
				}

				if (item.Item.State == ProcessingStateType.Uploaded)
				{
					AddToSets(item);
					FixDateTakenIfVideo(item);
					item.Item.State = ProcessingStateType.Success;
				}

				if (item.Item.State == ProcessingStateType.Duplicate && UpdateDuplicates)
				{
					UpdateItem(item);
					AddToSets(item);
					FixDateTakenIfVideo(item);
					item.Item.State = ProcessingStateType.Success;
				}
			}
			catch (Exception ex)
			{
				item.AddError(ex.Message, ex);
			}
			finally
			{
				LogAction($"Completed {item.Item.Filename} ({item.Item.State})");
				if (item.Item.State != ProcessingStateType.Success)
				{
					LogAction($"{item.Item.Filename} Errors:\r\n{item.Item.Errors}");
				}
			}
		}

		private void EnsureItemProperties(ProcessingItem item)
		{
			if (!item.Item.DateTaken.HasValue)
				SetDateTaken(item);
			if (string.IsNullOrEmpty(item.Item.Crc32))
				CalculateCrc32(item.Item);

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

		private void EnsureMachineTags(ProcessingItem item)
		{
			var tags = new List<string>(item.Item.Tags ?? new string[0]);
			if (!tags.Any(t => t.StartsWith(FilenameMachineTag)))
				tags.Add(BuildMachineTagExpression(FilenameMachineTag, Path.GetFileName(item.Item.LocalPath).ToLowerInvariant()));

			if (!tags.Any(t => t.StartsWith(ImportPathMachineTag)))
				tags.Add(BuildMachineTagExpression(ImportPathMachineTag,
					item.Item.LocalPath.Replace(Path.DirectorySeparatorChar, '/').ToLowerInvariant()));

			if (!tags.Any(t => t.StartsWith(UploaderMachineTag)))
				tags.Add(BuildMachineTagExpression(UploaderMachineTag, "Uploadr.Net"));

			if (!tags.Any(t => t.StartsWith(CrcMachineTag)))
				tags.Add(BuildMachineTagExpression(CrcMachineTag, item.Item.Crc32));

			//if (!tags.Any(t => "Uploadr.Net".Equals(t)))
			//	tags.Add("Uploadr.Net");

			item.Item.Tags = tags.ToArray();
		}

		private string BuildMachineTagExpression(string tag, string value)
		{
			var expr = $"{tag}=\"{value?.Replace('"', '\'')}\"";
			return expr;
		}

		private void FixDateTakenIfVideo(ProcessingItem item)
		{
			if (!IsVideo(item.Item.LocalPath) || !item.Item.DateTaken.HasValue)
				return;

			var f = _mgr.Surrogate;
			var r = item.Item.GetRemoteDetails(f);
			var dt = item.Item.DateTaken.Value;
			var rdt = r.DateTakenUnknown ? DateTime.MinValue : r.DateTaken.Date.ToUniversalTime();
			if (!dt.ToUniversalTime().Date.Equals(rdt))
			{
				f.PhotosSetDates(item.Item.PhotoId, dt, DateGranularity.FullDate);
				LogAction?.Invoke($"Updated Date-Taken (to {dt:d}) for video {item.Item.Filename}");
			}
		}

		private void SetDateTaken(ProcessingItem item)
		{
			var path = item.Item.LocalPath;
			item.Item.DateTaken = IsVideo(path) ? GetDateModified(path) : GetDateTaken(path);
		}

		private bool IsVideo(string path)
		{
			var ext = Path.GetExtension(path).ToLowerInvariant().Substring(1);
			return VideoFormats.Contains(ext);
		}

		private DateTime GetDateModified(string path)
		{
			var fi = new FileInfo(path);
			if (!fi.Exists)
				throw new FileNotFoundException($"File {path} was not found. Cannot retrieve date-modified");
			return fi.LastWriteTime;
		}

		private DateTime GetDateTaken(string path)
		{
			if (!File.Exists(path))
				throw new FileNotFoundException($"File {path} was not found. Cannot retrieve date-modified");

			try
			{
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
			catch (ArgumentException) //Property cannot be found.
			{
				LogAction($"Date-Taken EXIF Property (36867) for '{path}' could not be found. Using Date-Modified instead");
				return GetDateModified(path);
			}
		}

		private void AddToSets(ProcessingItem item)
		{
			var f = _mgr.Surrogate;
			foreach (var set in item.Item.Sets)
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
								LogAction?.Invoke($"Creating new Set '{set.Title}'");
								var newSet = f.PhotosetsCreate(set.Title, item.Item.PhotoId);
								set.Id = newSet.PhotosetId;
								LogAction?.Invoke($"Created new Set '{set.Title}'");
								added = true;
							}
						}
					}

					if (!added)
					{
						f.PhotosetsAddPhoto(set.Id, item.Item.PhotoId);
					}
					LogAction?.Invoke($"Added {item.Item.Filename} to Set '{set.Title}'");
				}
				catch (FlickrApiException ex)
				{
					//Photo already in set (3)
					if (ex.Code == 3)
						return;
				}
				catch (Exception ex)
				{
					item.AddError($"Error adding photo to Set {set.Title}: {ex.Message}", ex);
					return;
				}
			}
		}

		private bool CheckIsDuplicate(ProcessingItem item)
		{
			try
			{
				var f = _mgr.Surrogate;
				var title = item.Item.Title ?? item.Item.Filename;
				var query = new PhotoSearchOptions(_mgr.AccountDetails.UserId)
				{
					MachineTagMode = MachineTagMode.AnyTag,
					MachineTags =
						BuildMachineTagExpression(FilenameMachineTag, Path.GetFileName(item.Item.LocalPath).ToLowerInvariant()) + " " +
						BuildMachineTagExpression(CrcMachineTag, item.Item.Crc32)
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
				var dlm = item.Item.DateTaken;
				var dupItem = result.Count == 1
					? result[0]
					: result.FirstOrDefault(
						d => !d.DateTakenUnknown && dlm.HasValue && d.DateTaken.Subtract(dlm.Value).TotalSeconds < 1) ??
					  result.FirstOrDefault(d => d.DateTakenUnknown);
				PhotoInfo xItem;
				if (dupItem != null)
				{
					xItem = f.PhotosGetInfo(dupItem.PhotoId);
					item.Item.PhotoId = dupItem.PhotoId;
					return true;
				}
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error executing search-query. {ex.Message}", ex);
			}
			return false;
		}

		private void UpdateItem(ProcessingItem pItem)
		{
			var l = pItem.Item;
			var f = _mgr.Surrogate;
			var r = pItem.Item.GetRemoteDetails(f);

			if (!string.IsNullOrEmpty(l.Title) || !string.IsNullOrEmpty(l.Description))
			{
				if (l.Title != r.Title) //bug: r.Description always null?! || l.Description != r.Description)
				{
					f.PhotosSetMeta(l.PhotoId, l.Title, l.Description);
					LogAction?.Invoke($"Updated title/description of {l.Filename}");
				}
			}

			if (l.Tags?.Any(t => !r.Tags.Any(rt => rt.Raw.Equals(t, StringComparison.CurrentCultureIgnoreCase))) ?? false)
			{
				var allTags = r.Tags.Any()
					? (bool) l.Tags?.Any()
						? r.Tags.Select(t => t.Raw).Concat(l.Tags).Distinct().ToArray()
						: r.Tags.Select(t => t.Raw).ToArray()
					: l.Tags;
				f.PhotosSetTags(l.PhotoId, l.Tags);
				LogAction?.Invoke($"Updated tags of {l.Filename}");
			}

			if (l.IsFamily != r.IsFamily || l.IsPublic != r.IsPublic || l.IsFriend != r.IsFriend)
			{
				f.PhotosSetPerms(l.PhotoId, l.IsPublic, l.IsFriend, l.IsFamily, PermissionComment.Everybody, PermissionAddMeta.Owner);
				LogAction?.Invoke($"Updated rights/visibility of {l.Filename}");
			}
			if (l.SafetyLevel != r.SafetyLevel)
			{
				f.PhotosSetSafetyLevel(l.PhotoId, l.SafetyLevel, l.SearchState);
				LogAction?.Invoke($"Updated safety-level of {l.Filename}");
			}
		}

		private bool Upload(ProcessingItem pItem)
		{
			var item = pItem.Item;
			if (!string.IsNullOrEmpty(item.PhotoId))
			{
				return true;
			}

			using (var stream = File.OpenRead(item.LocalPath))
			{
				var fname = item.Filename;
				if (string.IsNullOrEmpty(item.Title))
					item.Title = fname;

				try
				{
					var tags = item.Tags == null ? null : "\"" + string.Join("\",\"", item.Tags) + "\"";
					var f = _mgr.Surrogate;
					item.PhotoId = f.UploadPicture(stream, fname, item.Title, item.Description, tags, item.IsPublic, item.IsFamily,
						item.IsFriend, item.ContentType, item.SafetyLevel, item.SearchState);
					LogAction?.Invoke($"{item.Filename} uploaded");
				}
				catch (Exception ex)
				{
					pItem.AddError($"Error uploading photo: {ex.Message}", ex);
					return false;
				}
			}
			return true;
		}
	}
}