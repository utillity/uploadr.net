using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using uTILLIty.UploadrNet.Windows.Models;

namespace uTILLIty.UploadrNet.Windows
{
	[TestClass]
	public class ProcessModeTests
	{
		[TestMethod, TestCategory("ProcessMode")]
		public void Expand1()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{relpathastags}", pi);
			var expected = "2001,Vacation";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand2()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001, 2002\Vacation\image1.jpg"};
			var actual = target.Expand("{relpathastags}", pi);
			var expected = "2001_ 2002,Vacation";
			Assert.AreEqual(expected, actual);
		}
	}
}