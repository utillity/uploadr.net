using System;
using System.IO;
using System.Threading;
using Timer = System.Timers.Timer;

namespace uTILLIty.UploadrNet.Windows
{
	public class ThrottledStream : Stream
	{
		private static readonly AutoResetEvent ResetEvent = new AutoResetEvent(true);
		// ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
		private static readonly Timer ThrottleTimer;
		private static int _bytesProcessed;
		private static readonly object SyncObject = new object();
		private static int _maxBytesPerSecond;

		private readonly Stream _parentStream;

		static ThrottledStream()
		{
			ThrottleTimer = new Timer { Interval = 1000 };
			ThrottleTimer.Elapsed += (o, e) =>
			{
				lock (SyncObject)
				{
					//bugfix reset BytesProcessed to 0 to avoid "deadlocking" due to accumulation of more bytes being sent
					_bytesProcessed = 0;
				}
				ResetEvent.Set();
			};
			ThrottleTimer.Start();
		}

		/// <summary>
		///   Creates a new Stream with Databandwith cap
		/// </summary>
		/// <param name="parentStream"></param>
		public ThrottledStream(Stream parentStream)
		{
			_parentStream = parentStream;
		}

		/// <summary>
		///   Number of Bytes that are allowed per second
		/// </summary>
		public static int MaxBytesPerSecond
		{
			get { return _maxBytesPerSecond; }
			set
			{
				if (value < 1)
					// ReSharper disable once NotResolvedInText
					throw new ArgumentOutOfRangeException("has to be > 0");
				_maxBytesPerSecond = value;
			}
		}

		public override bool CanRead => _parentStream.CanRead;

		public override bool CanSeek => _parentStream.CanSeek;

		public override bool CanWrite => _parentStream.CanWrite;

		public override long Length => _parentStream.Length;

		public override long Position
		{
			get { return _parentStream.Position; }
			set { _parentStream.Position = value; }
		}

		private static int AddBytesProcessed(int bytes)
		{
			lock (SyncObject)
			{
				_bytesProcessed += bytes;
				return _bytesProcessed;
			}
		}

		protected void Throttle(int bytes)
		{
			try
			{
				var total = AddBytesProcessed(bytes);
				if (total >= _maxBytesPerSecond)
					ResetEvent.WaitOne();
			}
			catch
			{
				// we don't care
			}
		}

		public override void Close()
		{
			_parentStream.Close();
			base.Close();
		}

		protected override void Dispose(bool disposing)
		{
			_parentStream.Dispose();
			base.Dispose(disposing);
		}

		public override void Flush()
		{
			_parentStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			Throttle(count);
			return _parentStream.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return _parentStream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			_parentStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			Throttle(count);
			_parentStream.Write(buffer, offset, count);
		}
	}
}