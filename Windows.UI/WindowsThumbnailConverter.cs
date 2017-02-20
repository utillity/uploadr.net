using System;
using System.Globalization;
using System.Windows.Data;
using Microsoft.WindowsAPICodePack.Shell;

namespace uTILLIty.UploadrNet.Windows
{
	public class WindowsThumbnailConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var filename = value as string;
			if (string.IsNullOrEmpty(filename))
				return null;
			return ShellFile.FromFilePath(filename).Thumbnail.BitmapSource;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		#region Singleton

		private static WindowsThumbnailConverter _singletonWindowsThumbnailConverter;
		private static readonly object WindowsThumbnailConverterSingletonLock = new object();

		public static WindowsThumbnailConverter Instance
		{
			get
			{
				if (_singletonWindowsThumbnailConverter == null)
				{
					// ReSharper disable PossibleMultipleWriteAccessInDoubleCheckLocking
					lock (WindowsThumbnailConverterSingletonLock)
					{
						if (_singletonWindowsThumbnailConverter == null)
							_singletonWindowsThumbnailConverter = new WindowsThumbnailConverter();
					}
					// ReSharper restore PossibleMultipleWriteAccessInDoubleCheckLocking
				}
				return _singletonWindowsThumbnailConverter;
			}
		}

		#endregion
	}
}
