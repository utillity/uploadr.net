namespace uTILLIty.UploadrNet.Windows
{
	public class FlickrAccountDetails
	{
		public FlickrAccountDetails(string fullName, string userId, string userName)
		{
			FullName = fullName;
			UserId = userId;
			UserName = userName;
		}

		public string FullName { get; private set; }
		public string UserId { get; private set; }
		public string UserName { get; private set; }
	}
}