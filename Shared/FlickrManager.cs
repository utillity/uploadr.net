using System;
using System.Diagnostics;
using FlickrNet;

namespace uTILLIty.UploadrNet.Windows
{
	public class FlickrManager : NotifyPropertyChangedBase
	{
		private static readonly string ApiKey = "97ef70f319d4445df3355494c2b72bf0";
		private static readonly string ApiSharedSecret = "1a17f62347ac7de7";

		private OAuthRequestToken _reqToken;

		public FlickrManager()
		{
			Surrogate = new Flickr(ApiKey, ApiSharedSecret);
		}

		public Flickr Surrogate { get; }

		public FlickrAccountDetails AccountDetails
		{
			get { return GetValue<FlickrAccountDetails>(); }
			private set
			{
				SetValue(value);
				IsAuthenticated = value != null;
			}
		}

		public bool IsAuthenticated
		{
			get { return GetValue<bool>(); }
			private set { SetValue(value); }
		}

		public void ApplyToken(AccessToken token)
		{
			//https://www.flickr.com/services/apps/create/noncommercial/

			var f = Surrogate;
			if (!string.IsNullOrEmpty(token.Token) && !string.IsNullOrEmpty(token.Secret))
			{
				f.OAuthAccessToken = token.Token;
				f.OAuthAccessTokenSecret = token.Secret;
				try
				{
					var t = f.AuthOAuthCheckToken();
					AccountDetails = new FlickrAccountDetails(t.User.FullName, t.User.UserId, t.User.UserName);
					//f.OAuthAccessToken = "72157680515506825-0acc7d86a66f777e";
					//f.OAuthAccessTokenSecret = "714c78ccc97b5a76"
				}
				catch (Exception ex)
				{
					//TODO: log or something
					AccountDetails = null;
					throw;
				}
			}
		}

		public AccessToken GetToken()
		{
			var f = Surrogate;
			if (string.IsNullOrEmpty(f.OAuthAccessToken))
				throw new InvalidOperationException($"Not authenticated. Call {nameof(InteractiveAuthorize)} first!");

			var token = new AccessToken(f.OAuthAccessToken, f.OAuthAccessTokenSecret);
			return token;
		}

		public void InteractiveAuthorize()
		{
			var f = Surrogate;
			var requestToken = _reqToken = f.OAuthGetRequestToken("oob");
			var url = f.OAuthCalculateAuthorizationUrl(requestToken.Token, AuthLevel.Write);
			Process.Start(url);
		}

		public void InteractiveAuthorizeComplete(string code)
		{
			if (_reqToken == null)
				throw new InvalidOperationException($"Must call {nameof(InteractiveAuthorize)}() first");

			//var code = "150-597-873";
			var f = Surrogate;
			var requestToken = _reqToken;
			try
			{
				var t = f.OAuthGetAccessToken(requestToken, code);
				f.OAuthAccessToken = t.Token;
				f.OAuthAccessTokenSecret = t.TokenSecret;
				AccountDetails = new FlickrAccountDetails(t.FullName, t.UserId, t.Username);
			}
			catch (Exception ex)
			{
				//TODO: log or something
				AccountDetails = null;
				throw;
			}
		}
	}
}