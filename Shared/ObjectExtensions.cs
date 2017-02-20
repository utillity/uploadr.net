using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace uTILLIty.UploadrNet.Windows
{
	public static class ObjectExtensions
	{
		/// <summary>
		///   Serializes the supplied <paramref name="source" /> to XML
		/// </summary>
		public static string ToXml<T>(this T source, Encoding encoding, params Type[] knownTypes)
		{
			using (var stream = new MemoryStream())
			{
				source.ToXml(stream, encoding, knownTypes);
				stream.Seek(0, SeekOrigin.Begin);
				string output = new StreamReader(stream).ReadToEnd();
				return output;
			}
		}

		/// <summary>
		///   Serializes the supplied <paramref name="source" /> to XML
		/// </summary>
		public static void ToXml<T>(this T source, Stream stream, Encoding encoding, params Type[] knownTypes)
		{
			using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
			{
#if !WINDOWS_PHONE //TODO: check, if supported by WP8
				Indent = true,
#endif
				Encoding = encoding
			}))
			{
				var serializer = new XmlSerializer(source.GetType(), knownTypes);
				serializer.Serialize(writer, source);
				writer.Flush();
				//stream.Seek(0, SeekOrigin.Begin);
			}
		}

		/// <summary>
		///   Deserializes the supplied <paramref name="data" /> to an instance of the specified type
		/// </summary>
		//public static T Load<T>(this XmlElement data, Type[] knownTypes)
		//{
		//	if (data == null)
		//		throw new ArgumentNullException("data");

		//	using (XmlReader reader = XmlReader.Create(new StringReader(data.OuterXml)))
		//	{
		//		return Load<T>(reader, knownTypes);
		//	}
		//}

		/// <summary>
		///   Deserializes the supplied <paramref name="data" /> to an instance of the specified type
		/// </summary>
		public static T Load<T>(this string data, params Type[] knownTypes)
		{
			if (string.IsNullOrEmpty(data))
				throw new ArgumentNullException("data");

			using (XmlReader reader = XmlReader.Create(new StringReader(data)))
			{
				return Load<T>(reader, knownTypes);
			}
		}

		/// <summary>
		///   Deserializes the supplied <paramref name="data" /> to an instance of the specified type
		/// </summary>
		public static object Load(this string data, Type targetType, params Type[] knownTypes)
		{
			if (string.IsNullOrEmpty(data))
				throw new ArgumentNullException("data");

			using (XmlReader reader = XmlReader.Create(new StringReader(data)))
			{
				return Load(reader, targetType, knownTypes);
			}
		}

		/// <summary>
		///   Deserializes the supplied <paramref name="stream" /> to an instance of the specified type
		/// </summary>
		public static T Load<T>(this Stream stream, params Type[] knownTypes)
		{
			if (stream == null)
				throw new ArgumentNullException("stream");

			using (XmlReader reader = XmlReader.Create(stream))
			{
				return Load<T>(reader, knownTypes);
			}
		}

		/// <summary>
		///   Deserializes the supplied <paramref name="reader" /> to an instance of the specified type
		/// </summary>
		public static T Load<T>(this XmlReader reader, params Type[] knownTypes)
		{
			var serializer = new XmlSerializer(typeof(T), knownTypes);
			var output = (T)serializer.Deserialize(reader);
			return output;
		}

		/// <summary>
		///   Deserializes the supplied <paramref name="reader" /> to an instance of the specified type
		/// </summary>
		public static object Load(this XmlReader reader, Type targetType, params Type[] knownTypes)
		{
			var serializer = new XmlSerializer(targetType, knownTypes);
			object output = serializer.Deserialize(reader);
			return output;
		}
	}
}
