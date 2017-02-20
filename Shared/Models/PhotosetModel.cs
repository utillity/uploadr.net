namespace uTILLIty.UploadrNet.Windows.Models
{
	public class PhotosetModel : NotifyPropertyChangedBase
	{
		public PhotosetModel()
		{
		}

		public PhotosetModel(string id, string title, string description)
		{
			Title = title;
			Description = description;
			Id = id;
		}

		public string Id
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

		public int PhotosCount
		{
			get { return GetValue<int>(); }
			set { SetValue(value); }
		}

		public int VideosCount
		{
			get { return GetValue<int>(); }
			set { SetValue(value); }
		}

		public override string ToString()
		{
			return $"{Title} ({Id})";
		}
	}
}