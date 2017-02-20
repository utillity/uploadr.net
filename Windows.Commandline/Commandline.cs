using CommandLineParser.Arguments;
using FlickrNet;

namespace uTILLIty.UploadrNet.Windows
{
	internal class Commandline
	{
		[ValueArgument(typeof (string), 'a', "album", AllowMultiple = true)] public string[]
			AddToSets;

		[SwitchArgument('z', "createAlbums", false)] public bool AutoCreateSets;

		[SwitchArgument('c', "checkonly", false)] public bool CheckOnly;

		[SwitchArgument('u', "updatedups", false)] public bool UpdateSetsOfDuplicates;

		[ValueArgument(typeof (ContentType), 'n', "ctype")] public ContentType ContentType;

		[ValueArgument(typeof (byte), 'p', "parallelism", DefaultValue = (byte)10)] public byte MaxConcurrentOperations = 10;

		[ValueArgument(typeof (string), 'd', "desc")] public string Description;

		[SwitchArgument('y', "family", false)] public bool IsFamily;

		[SwitchArgument('f', "friend", false)] public bool IsFriend;

		[SwitchArgument('o', "public", false)] public bool IsPublic;

		[ValueArgument(typeof (string), 's', "source", Optional = false)] public string
			RootDirectory;

		[ValueArgument(typeof (SafetyLevel), 'e', "safety")] public SafetyLevel SafetyLevel;

		[ValueArgument(typeof (HiddenFromSearch), 'g', "search")] public HiddenFromSearch SearchState;

		[ValueArgument(typeof (string), 't', "tags")] public string Tags;

		[ValueArgument(typeof (string), 'i', "title")] public string Title;
	}
}