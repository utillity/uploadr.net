using System;
using System.Diagnostics;

namespace uTILLIty.UploadrNet.Windows.Models
{
	internal class ProcessingItem
	{
		public ProcessingItem(PhotoModel item)
		{
			Item = item;
		}

		public PhotoModel Item { get; }

		public int RetryCount { get; set; }

		public void AddError(string msg, Exception ex)
		{
			Debug.WriteLine(ex.ToString());
			Item.Errors = $"{DateTime.Now:T} {msg}\r\n{Item.Errors}";
			Item.State = ProcessingStateType.Retry;
			RetryCount++;
		}
	}
}