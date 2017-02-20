using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FlickrNet;
using uTILLIty.UploadrNet.Windows.Models;

namespace uTILLIty.UploadrNet.Windows
{
	public class MainWindowViewModel : NotifyPropertyChangedBase, IDisposable
	{
		private readonly FlickrManager _mgr;
		private readonly HashSet<string> _subPhotosets = new HashSet<string>();
		private readonly Timer _updateSetsTimer;

		public MainWindowViewModel(FlickrManager mgr)
		{
			_mgr = mgr;
			_updateSetsTimer = new Timer(OnUpdateSets, null, 5000, 30000);
			Task.Run(() => ReloadTree()).ConfigureAwait(false);
			Task.Run(() => FillPhotosFromPath(@"C:\Users\Tilli\odrive\OneDrive\Bilder\Familie\2001")).ConfigureAwait(false);
		}

		public PhotosetCollectionModel PhotosetsTree
		{
			get { return GetValue<PhotosetCollectionModel>(); }
			set { SetValue(value); }
		}

		public ObservableCollection<PhotoModel> Photos
		{
			get { return GetValue<ObservableCollection<PhotoModel>>(); }
			set { SetValue(value); }
		}

		public void Dispose()
		{
			_updateSetsTimer.Dispose();
		}

		private void FillPhotosFromPath(string path)
		{
			var di = new DirectoryInfo(path);
			var files = di.GetFiles("*.*", SearchOption.AllDirectories);
			var list = new List<PhotoModel>(files.Length);
			foreach (var f in files)
			{
				//if (list.Count > 100)
				//	break;
				var ext = f.Extension.ToLowerInvariant();
				switch (ext)
				{
					case ".png":
					case ".jpg":
						var item = new PhotoModel {LocalPath = f.FullName};
						list.Add(item);
						break;
				}
			}
			Application.Current.Dispatcher.Invoke(() =>
			{
				var col = new ObservableCollection<PhotoModel>(list);
				Photos = col;
			});
		}

		private void OnUpdateSets(object state)
		{
			var f = _mgr.Surrogate;
			var sets = f.PhotosetsGetList();
			foreach (var set in sets)
			{
				var model = TryGetSet(set.PhotosetId, PhotosetsTree);
				if (model != null)
				{
					model.PhotosCount = set.NumberOfPhotos;
					model.VideosCount = set.NumberOfVideos;
				}
			}
		}

		private PhotosetModel TryGetSet(string photosetId, PhotosetCollectionModel parent)
		{
			var i =
				parent.Items.FirstOrDefault(
					e => e.GetType() == typeof (PhotosetModel) && e.Id.Equals(photosetId, StringComparison.InvariantCulture));
			if (i != null)
				return i;
			var colls = parent.Items.OfType<PhotosetCollectionModel>().ToArray();
			foreach (var col in colls)
			{
				i = TryGetSet(photosetId, col);
				if (i != null)
					return i;
			}
			return null;
		}

		private void ReloadTree()
		{
			var f = _mgr.Surrogate;
			var coll = f.CollectionsGetTree();
			var tree = new PhotosetCollectionModel {Title = "Root"};
			foreach (var t in coll)
			{
				BuildTree(t, 0, tree);
			}

			var sets = f.PhotosetsGetList();
			foreach (var set in sets)
			{
				if (_subPhotosets.Contains(set.PhotosetId))
					continue;
				AddPhotoset(set, tree);
				Debug.WriteLine($"Set: {set.Title} ({set.PhotosetId})");
			}

			PhotosetsTree = tree;
		}

		private void BuildTree(Collection coll, int depth, PhotosetCollectionModel parent)
		{
			var cur = new PhotosetCollectionModel(coll.CollectionId, coll.Title, coll.Description);
			parent.Items.Add(cur);
			var spaces = new string(' ', depth*2);
			Debug.WriteLine($"{spaces}Collection: {coll.Title}");
			foreach (var subT in coll.Collections)
			{
				BuildTree(subT, depth + 1, cur);
			}
			//var f = _flickr;
			//var result = f.PhotosSearch(new PhotoSearchOptions("147869897@N08") {Text = "Aqua"});
			//f.PhotosetsAddPhoto(setId, photoId);

			foreach (var set in coll.Sets)
			{
				_subPhotosets.Add(set.SetId);

				var s = new Photoset {PhotosetId = set.SetId, Title = set.Title, Description = set.Description};
				//too slow calling this for each set - using background task instead
				//f.PhotosetsGetInfo(set.SetId);

				AddPhotoset(s, cur);
				Debug.WriteLine($"{spaces}  Set: {set.Title} ({set.SetId})");
			}
		}

		private void AddPhotoset(Photoset set, PhotosetCollectionModel tree)
		{
			var m = new PhotosetModel(set.PhotosetId, set.Title, set.Description)
			{
				PhotosCount = set.NumberOfPhotos,
				VideosCount = set.NumberOfVideos
			};
			tree.Items.Add(m);
		}
	}
}