using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Text;
using System.IO;

namespace KinectDJ
{
	/// <summary>
	/// App.xaml の相互作用ロジック
	/// </summary>
	public partial class App : Application
	{
	}

	struct RiffHeader
	{
		public string ID;
		public uint Size;
		public string Format;

		public static RiffHeader GetRiffHeader(Stream stream)
		{
			var header=new RiffHeader();
			var rawData=new byte[12];
			stream.Read(rawData,0,rawData.Length);
			header.ID=Encoding.ASCII.GetString(rawData.Take(4).ToArray());
			header.Size=BitConverter.ToUInt32(rawData.Skip(4).Take(4).ToArray(),0);
			header.Format=Encoding.ASCII.GetString(rawData.Skip(8).ToArray());
			return header;
		}
	}

	struct FormatChunk
	{
		public string ID;
		public int Size;
		public short FormatTag;
		public ushort Channels;
		public uint SamplesPerSecond;
		public uint AverageBytesPerSecond;
		public ushort BlockAlign;
		public ushort BitsPerSecond;

		public static FormatChunk GetFormatChunk(Stream stream)
		{
			var formatChunk=new FormatChunk();
			var rawData=new byte[24];
			stream.Read(rawData,0,rawData.Length);
			formatChunk.ID=Encoding.ASCII.GetString(rawData.Take(4).ToArray());
			formatChunk.Size=BitConverter.ToInt32(rawData.Skip(4).Take(4).ToArray(),0);
			formatChunk.FormatTag=BitConverter.ToInt16(rawData.Skip(8).Take(2).ToArray(),0);
			formatChunk.Channels=BitConverter.ToUInt16(rawData.Skip(10).Take(2).ToArray(),0);
			formatChunk.SamplesPerSecond=BitConverter.ToUInt32(rawData.Skip(12).Take(4).ToArray(),0);
			formatChunk.AverageBytesPerSecond=BitConverter.ToUInt32(rawData.Skip(16).Take(4).ToArray(),0);
			formatChunk.BlockAlign=BitConverter.ToUInt16(rawData.Skip(20).Take(2).ToArray(),0);
			formatChunk.BitsPerSecond=BitConverter.ToUInt16(rawData.Skip(22).ToArray(),0);
			return formatChunk;
		}
	}

	struct DataChunk
	{
		public string ID;
		public int Size;

		public static DataChunk GetDataChunk(Stream stream)
		{
			var dataChunk=new DataChunk();
			var rawData=new byte[8];
			stream.Read(rawData,0,rawData.Length);
			dataChunk.ID=Encoding.ASCII.GetString(rawData.Take(4).ToArray());
			dataChunk.Size=BitConverter.ToInt32(rawData.Skip(4).ToArray(),0);
			return dataChunk;
		}
	}

	class WaveFile:IDisposable
	{
		public RiffHeader RiffHeader{get;private set;}
		public FormatChunk FormatChunk{get;private set;}
		public DataChunk DataChunk{get;private set;}
		public short[] Data{get;private set;}

		public WaveFile(string fileName)
		{
			var stream=File.OpenRead(fileName);
			RiffHeader=RiffHeader.GetRiffHeader(stream);
			var pos=stream.Position;
			FormatChunk=FormatChunk.GetFormatChunk(stream);
			stream.Position=pos+FormatChunk.Size+8;
			DataChunk=DataChunk.GetDataChunk(stream);
			if(stream.Length<12+8+FormatChunk.Size+8+DataChunk.Size) throw new InvalidDataException("WAVEファイルの形式が不正です。");
			var waveDataStream=new BinaryReader(stream);
			Data=new short[DataChunk.Size/2];
			unsafe{
				fixed(short* ptr=&Data[0])
					using(var unmanagedStream=new UnmanagedMemoryStream((byte*)ptr,DataChunk.Size,DataChunk.Size,FileAccess.Write))
						stream.CopyTo(unmanagedStream);
			}
			stream.Dispose();
		}

		public void Dispose()
		{
			Data=null;
		}
	}
}
