using System;
using System.IO;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace uTILLIty.UploadrNet.Windows
{
	internal abstract class ModeBase
	{
		/* CLP cannot traverse sub-types for properties! */

		public abstract bool ModeChosen { get; set; }
		public abstract FileInfo KeyFile { get; set; }
		public abstract bool ShowParsedArgs { get; set; }

		public virtual string GetAdditionalUsageHints()
		{
			return null;
		}

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