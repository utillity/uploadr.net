using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace uTILLIty.UploadrNet.Windows.Models
{
	public class PhotosetCollectionModel : PhotosetModel
	{
		public PhotosetCollectionModel()
		{
		}

		public PhotosetCollectionModel(string id, string title, string description) : base(id, title, description)
		{
		}


		public ICollection<PhotosetModel> Items { get; } = new ObservableCollection<PhotosetModel>();
	}
}