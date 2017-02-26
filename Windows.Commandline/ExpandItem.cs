using System;
using uTILLIty.UploadrNet.Windows.Models;

namespace uTILLIty.UploadrNet.Windows
{
	internal class ExpandItem
	{
		public ExpandItem(string key, string description, Func<PhotoModel, string> expandAction)
		{
			Key = key;
			Description = description;
			Expand = expandAction;
		}

		public string Key { get; }
		public Func<PhotoModel, string> Expand { get; private set; }
		public string Description { get; private set; }

		protected bool Equals(ExpandItem other)
		{
			return string.Equals(Key, other.Key, StringComparison.InvariantCultureIgnoreCase);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((ExpandItem) obj);
		}

		public override int GetHashCode()
		{
			return StringComparer.InvariantCultureIgnoreCase.GetHashCode(Key);
		}

		public static bool operator ==(ExpandItem left, ExpandItem right)
		{
			return Equals(left, right);
		}

		public static bool operator !=(ExpandItem left, ExpandItem right)
		{
			return !Equals(left, right);
		}
	}
}