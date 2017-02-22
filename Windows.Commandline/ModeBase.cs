using System;
using System.IO;

namespace uTILLIty.UploadrNet.Windows
{
	internal abstract class ModeBase
	{
		public abstract bool ModeChosen { get; set; }
		public abstract FileInfo KeyFile { get; set; }

		public abstract void Execute();

		protected FlickrManager LoadToken()
		{
			var mgr = new FlickrManager();
			AccessToken token;
			try
			{
				using (var stream = KeyFile.OpenRead())
				{
					token = stream.Load<AccessToken>();
				}
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Error loading token '{KeyFile.FullName}'. {ex.Message}");
			}
			try
			{
				mgr.ApplyToken(token);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Supplied token is invalid. {ex.Message}");
			}
			return mgr;
		}
	}
}