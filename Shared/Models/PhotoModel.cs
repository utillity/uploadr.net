using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using FlickrNet;

namespace uTILLIty.UploadrNet.Windows.Models
{
	public class PhotoModel : NotifyPropertyChangedBase
	{
		public string LocalPath
		{
			get { return GetValue<string>(); }
			set
			{
				SetValue(value);
				Filename = Path.GetFileNameWithoutExtension(LocalPath);
			}
		}

		public string Filename { get; private set; }

		public string Tags
		{
			get { return GetValue<string>(); }
			set { SetValue(value); }
		}

		public string Title
		{
			get { return GetValue<string>(); }
			set { SetValue(value); }
		}

		public string Description
		{
			get { return GetValue<string>(); }
			set { SetValue(value); }
		}

		public bool IsPublic
		{
			get { return GetValue<bool>(); }
			set { SetValue(value); }
		}

		public bool IsFriend
		{
			get { return GetValue<bool>(); }
			set { SetValue(value); }
		}

		public bool IsFamily
		{
			get { return GetValue<bool>(); }
			set { SetValue(value); }
		}

		public PhotoProcessingStateType State
		{
			get { return GetValue<PhotoProcessingStateType>(); }
			set { SetValue(value); }
		}

		public ContentType ContentType
		{
			get { return GetValue<ContentType>(); }
			set { SetValue(value); }
		}

		public SafetyLevel SafetyLevel
		{
			get { return GetValue<SafetyLevel>(); }
			set { SetValue(value); }
		}

		public HiddenFromSearch SearchState
		{
			get { return GetValue<HiddenFromSearch>(); }
			set { SetValue(value); }
		}

		public string Errors
		{
			get { return GetValue<string>(); }
			set { SetValue(value); }
		}

		public string PhotoId
		{
			get { return GetValue<string>(); }
			set { SetValue(value); }
		}

		public ObservableCollection<PhotosetModel> Sets { get; } = new ObservableCollection<PhotosetModel>();

		public int RetryCount
		{
			get { return GetValue<int>(); }
			set { SetValue(value); }
		}


		public void AddError(string msg, Exception ex)
		{
			Debug.WriteLine(ex.ToString());
			Errors = $"{DateTime.Now:T} {msg}\r\n{Errors}";
			State = PhotoProcessingStateType.Retry;
			RetryCount++;
		}
	}
}