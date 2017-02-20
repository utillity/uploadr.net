namespace uTILLIty.UploadrNet.Windows
{
	public class AccessToken
	{
		public AccessToken()
		{
		}

		public AccessToken(string token, string secret)
		{
			Token = token;
			Secret = secret;
		}

		public string Token { get; set; }
		public string Secret { get; set; }
	}
}