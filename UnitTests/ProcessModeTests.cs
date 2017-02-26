using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using uTILLIty.UploadrNet.Windows.Models;

namespace uTILLIty.UploadrNet.Windows
{
	[TestClass]
	public class ProcessModeTests
	{
		public TestContext TestContext { get; set; }

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_relpathastags_1()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{relpathastags}", pi);
			var expected = "2001,Vacation";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_relpathastags_2()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001, 2002\Vacation\image1.jpg"};
			var actual = target.Expand("{RelPathAsTags}", pi);
			var expected = "2001_ 2002,Vacation";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_relrootfolder()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{relrootfolder}", pi);
			var expected = "2001";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_path()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{path}", pi);
			var expected = @"c:\Demo\2001\Vacation";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_relpath()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{relpath}", pi);
			var expected = @"2001\Vacation";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_fname()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{fname}", pi);
			var expected = @"image1";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_fnameandext()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{fnameandext}", pi);
			var expected = @"image1.jpg";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_folder()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{folder}", pi);
			var expected = @"Vacation";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_now()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{now}", pi);
			TestContext.WriteLine("{0}", actual);
			Assert.IsFalse(actual.Contains("{"));
		}

		[TestMethod, TestCategory("ProcessMode")]
		public void Expand_Unknown()
		{
			var target = new ProcessMode {RootDirectory = new DirectoryInfo(@"c:\Demo")};
			var pi = new PhotoModel {LocalPath = @"c:\Demo\2001\Vacation\image1.jpg"};
			var actual = target.Expand("{i dont exist}", pi);
			var expected = @"{i dont exist}";
			TestContext.WriteLine("{0}", actual);
			Assert.AreEqual(expected, actual);
		}
	}
}