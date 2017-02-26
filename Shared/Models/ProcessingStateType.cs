namespace uTILLIty.UploadrNet.Windows.Models
{
	public enum ProcessingStateType
	{
		Pending,
		Duplicate,
		ReadyToUpload,
		Uploading,
		Uploaded,
		Retry,
		Completed,
		Failed
	}
}