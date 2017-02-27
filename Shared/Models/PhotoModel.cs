using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using FlickrNet;

namespace uTILLIty.UploadrNet.Windows.Models
{
	[XmlRoot("MediaItem")]
	public class PhotoModel : NotifyPropertyChangedBase
	{
		private PhotoInfo _remoteDetails;

		public string LocalPath
		{
			get { return GetValue<string>(); }
			set
			{
				SetValue(value);
				Filename = Path.GetFileNameWithoutExtension(LocalPath);
			}
		}

		[XmlIgnore]
		public string Filename
		{
			get { return GetValue<string>(); }
			private set { SetValue(value); }
		}

		public DateTime? DateTaken
		{
			get { return GetValue<DateTime?>(); }
			set { SetValue(value); }
		}

		public string[] Tags
		{
			get { return GetValue<string[]>(); }
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

		public ProcessingStateType State
		{
			get { return GetValue<ProcessingStateType>(); }
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

		public ObservableCollection<PhotosetModel> Sets { get; set; } = new ObservableCollection<PhotosetModel>();
		public string Crc32 { get; set; }
		public int RetryCount { get; set; }

		public void AddError(string msg, Exception ex)
		{
			Debug.WriteLine(ex.ToString());
			AddMessage(msg + "\r\n" + ex);
			State = ProcessingStateType.Retry;
			RetryCount++;
		}

		public void AddMessage(string msg)
		{
			Errors = $"{DateTime.Now:T} {msg}\r\n{Errors}";
		}

		public PhotoInfo GetRemoteDetails(Flickr f)
		{
			return _remoteDetails ?? (_remoteDetails = f.PhotosGetInfo(PhotoId));
		}
	}
}