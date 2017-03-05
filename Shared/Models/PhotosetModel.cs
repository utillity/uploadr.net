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

		public string Key => Id ?? Title;

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
			return string.IsNullOrEmpty(Id) ? Title : $"{Title} ({Id})";
		}

		protected bool Equals(PhotosetModel other)
		{
			return string.Equals(Key, other.Key);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((PhotosetModel) obj);
		}

		public override int GetHashCode()
		{
			return Key?.GetHashCode() ?? 0;
		}

		public static bool operator ==(PhotosetModel left, PhotosetModel right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(PhotosetModel left, PhotosetModel right)
		{
			return !Equals(left, right);
		}
	}
}