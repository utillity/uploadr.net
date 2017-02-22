using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FlickrNet;
using uTILLIty.UploadrNet.Windows.Models;

namespace uTILLIty.UploadrNet.Windows
{
	public class UploadManager : NotifyPropertyChangedBase
	{
		private const string FilenameMachineTag = "orig:filename";
		private const string UploaderMachineTag = "orig:uploader";

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
			_processQueue.Add(new ProcessingItem(item));
		}

		public void AddRange(List<PhotoModel> list)
		{
			_processQueue.AddRange(list.Select(i => new ProcessingItem(i)));
		}

		public async Task ProcessAsync()
		{
			if (IsBusy)
				return;

			var token = _cancellationToken;

			try
			{
				IsBusy = true;
				while (true)
				{
					var items = _processQueue.Where(ShouldProcess).ToArray();
					if (!items.Any())
						break;

					var result = Parallel.ForEach(items,
						new ParallelOptions {MaxDegreeOfParallelism = MaxConcurrentOperations, CancellationToken = token},
						item =>
						{
							try
							{
								if (token.IsCancellationRequested)
									return;
								if (!ShouldProcess(item))
									return;

								EnsureMachineTags(item);

								if (!item.Item.DateTaken.HasValue)
									SetDateTaken(item);

								if (CheckForDuplicates && CheckIsDuplicate(item))
									item.Item.State = ProcessingStateType.Duplicate;
								else
									item.Item.State = ProcessingStateType.ReadyToUpload;

								if (!CheckOnly)
								{
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

									if (token.IsCancellationRequested)
										return;
									if (item.Item.State == ProcessingStateType.Uploaded)
									{
										AddToSets(item);
										FixDateTakenIfVideo(item);
										item.Item.State = ProcessingStateType.Success;
									}

									if (token.IsCancellationRequested)
										return;
									if (item.Item.State == ProcessingStateType.Duplicate && UpdateDuplicates)
									{
										UpdateItem(item);
										AddToSets(item);
										FixDateTakenIfVideo(item);
										item.Item.State = ProcessingStateType.Success;
									}
								}
							}
							catch (Exception ex)
							{
								item.AddError(ex.Message, ex);
							}
						});
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
				IsBusy = false;
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

			if (!tags.Any(t => "Uploadr.Net".Equals(t)))
				tags.Add("Uploadr.Net");

			item.Item.Tags = tags.ToArray();
		}

		private string BuildMachineTagExpression(string tag, string value)
		{
			var expr = $"{tag}=\"{value.Replace('"', '\'')}\"";
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
			item.Item.DateTaken = IsVideo(path)
				? GetDateModified(path)
				: GetDateTaken(path);
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
				catch (Exception ex)
				{
					item.AddError($"Error adding photo to Set {set.Title}: {ex.Message}", ex);
					return;
				}
			}
		}

		private bool ShouldProcess(ProcessingItem item)
		{
			switch (item.Item.State)
			{
				case ProcessingStateType.Pending:
					return true;
				case ProcessingStateType.Retry:
					if (item.RetryCount <= 3)
						return true;
					item.Item.State = ProcessingStateType.Failed;
					return false;
			}
			return false;
		}

		private bool CheckIsDuplicate(ProcessingItem item)
		{
			try
			{
				var f = _mgr.Surrogate;
				var title = item.Item.Title ?? item.Item.Filename;
				var query = new PhotoSearchOptions(_mgr.AccountDetails.UserId)
				{
					MachineTags = BuildMachineTagExpression(FilenameMachineTag, item.Item.Filename)
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
						d => !d.DateTakenUnknown && dlm.HasValue && d.DateTaken.Subtract(dlm.Value).TotalSeconds < 1)
					  ?? result.FirstOrDefault(d => d.DateTakenUnknown);
				if (dupItem != null)
				{
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
				f.PhotosSetPerms(l.PhotoId, l.IsPublic, l.IsFriend, l.IsFamily,
					PermissionComment.Everybody, PermissionAddMeta.Owner);
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
					item.PhotoId = f.UploadPicture(stream, fname, item.Title, item.Description,
						tags, item.IsPublic, item.IsFamily, item.IsFriend, item.ContentType,
						item.SafetyLevel, item.SearchState);
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