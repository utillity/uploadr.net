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
		//we init this once so that if the function is repeatedly called
		//it isn't stressing the garbage man
		private static readonly Regex ColonRegex = new Regex(":");
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
			get { return GetValue(5); }
			set { SetValue(value); }
		}

		public bool IsBusy
		{
			get { return GetValue<bool>(); }
			private set { SetValue(value); }
		}

		public bool CheckOnly
		{
			get { return GetValue<bool>(); }
			set { SetValue(value); }
		}

		public Action<string> LogAction
		{
			get { return GetValue<Action<string>>(); }
			set { SetValue(value); }
		}

		public bool UpdateSetsOfDuplicates
		{
			get { return GetValue(true); }
			set { SetValue(value); }
		}

		public void Add(PhotoModel item)
		{
			_processQueue.Add(item);
		}

		public void AddRange(List<PhotoModel> list)
		{
			_processQueue.AddRange(list);
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

					var result = Parallel.ForEach(items, new ParallelOptions {MaxDegreeOfParallelism = MaxConcurrentOperations},
						item =>
						{
							if (!token.IsCancellationRequested && ShouldProcess(item))
								CheckIsDuplicate(item);

							if (!CheckOnly)
							{
								if (item.State == PhotoProcessingStateType.ReadyToUpload)
									Upload(item);
								if (item.State == PhotoProcessingStateType.Uploaded)
									AddToSets(item);
								if (item.State == PhotoProcessingStateType.Duplicate && UpdateSetsOfDuplicates)
									AddToSets(item);
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

		private void AddToSets(PhotoModel item)
		{
			var f = _mgr.Surrogate;
			foreach (var set in item.Sets)
			{
				try
				{
					if (string.IsNullOrEmpty(set.Id))
					{
						var newSet = f.PhotosetsCreate(set.Title, item.PhotoId);
						set.Id = newSet.PhotosetId;
						LogAction?.Invoke($"Created new Set '{set.Title}'");
					}
					else
					{
						f.PhotosetsAddPhoto(set.Id, item.PhotoId);
					}
					LogAction?.Invoke($"Added {item.Filename} to Set '{set.Title}'");
				}
				catch (Exception ex)
				{
					item.AddError($"Error adding photo to Set {set.Title}: {ex.Message}", ex);
					return;
				}
			}
		}

		private bool ShouldProcess(PhotoModel item)
		{
			switch (item.State)
			{
				case PhotoProcessingStateType.Pending:
					return true;
				case PhotoProcessingStateType.Retry:
					return item.RetryCount <= 3;
			}
			return false;
		}

		private void CheckIsDuplicate(PhotoModel item)
		{
			try
			{
				var f = _mgr.Surrogate;
				var query = new PhotoSearchOptions(_mgr.AccountDetails.UserId)
				{
					Text = Path.GetFileNameWithoutExtension(item.LocalPath)
				};
				item.State = PhotoProcessingStateType.Pending;
				var result = f.PhotosSearch(query);
				CheckIsDuplicate(item, result);
			}
			catch (Exception ex)
			{
				item.AddError($"Error executing search-query. {ex.Message}", ex);
			}
		}

		private void CheckIsDuplicate(PhotoModel item, PhotoCollection result)
		{
			var dups = result.Where(r => r.Title.Equals(item.Filename)).ToArray();

			if (!dups.Any())
			{
				item.State = PhotoProcessingStateType.ReadyToUpload;
				return;
			}
			var localDateTaken = GetDateTakenFromImage(item.LocalPath);
			item.State = dups.Any(d => !d.DateTakenUnknown && d.DateTaken.Subtract(localDateTaken).TotalSeconds < 1)
				? PhotoProcessingStateType.Duplicate
				: PhotoProcessingStateType.ReadyToUpload;
			if (item.State == PhotoProcessingStateType.Duplicate)
				LogAction?.Invoke($"{item.Filename} is a duplicate!");
		}

		public static DateTime GetDateTakenFromImage(string path)
		{
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
			using (var myImage = Image.FromStream(fs, false, false))
			{
				var propItem = myImage.GetPropertyItem(36867);
				var dateTaken = ColonRegex.Replace(Encoding.UTF8.GetString(propItem.Value), "-", 2);
				return DateTime.Parse(dateTaken);
			}
		}

		private void Upload(PhotoModel item)
		{
			if (!string.IsNullOrEmpty(item.PhotoId))
			{
				item.State = PhotoProcessingStateType.Uploaded;
				return;
			}

			using (var stream = File.OpenRead(item.LocalPath))
			{
				var token = _cancellationToken;
				if (token.IsCancellationRequested)
					return;

				item.State = PhotoProcessingStateType.Uploading;
				var fname = item.Filename;

				try
				{
					var f = _mgr.Surrogate;
					item.PhotoId = f.UploadPicture(stream, fname, item.Title ?? fname, item.Description,
						item.Tags, item.IsPublic, item.IsFamily, item.IsFriend, item.ContentType,
						item.SafetyLevel, item.SearchState);
					item.State = PhotoProcessingStateType.Uploaded;
					LogAction?.Invoke($"{item.Filename} uploaded");
				}
				catch (Exception ex)
				{
					item.AddError($"Error uploading photo: {ex.Message}", ex);
				}
			}
		}
	}
}