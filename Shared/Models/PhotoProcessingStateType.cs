namespace uTILLIty.UploadrNet.Windows.Models
{
	public enum PhotoProcessingStateType
	{
		Pending,
		Duplicate,
		ReadyToUpload,
		Uploading,
		Uploaded,
		Retry,
		Success,
		Failed
	}
}