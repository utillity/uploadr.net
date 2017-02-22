using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace uTILLIty.UploadrNet.Windows
{
	[TestClass]
	public class Spikes
	{
		public TestContext TestContext { get; set; }

		[TestMethod]
		public void SpikeGetVideoDateCreated()
		{
			var file = @"C:\Users\Tilli\odrive\OneDrive\Bilder\Familie\2010\12\Saalbach\Saalbach 12 (1).MOV";

			var fi = new FileInfo(file);
			if (!fi.Exists)
				throw new FileNotFoundException($"File {file} was not found. Cannot retrieve date-modified");

			var dlm = fi.LastWriteTime;
			TestContext.WriteLine($"{dlm}");

			Assert.AreEqual(2011, dlm.Year);
			Assert.AreEqual(1, dlm.Month);
			Assert.AreEqual(2, dlm.Day);
			Assert.AreEqual(13, dlm.Hour);
			Assert.AreEqual(57, dlm.Minute);
		}
	}
}