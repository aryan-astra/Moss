using System;
using System.IO;
using System.Media;
using System.Text;
using Moss.Core;

namespace Moss.Windows;

internal sealed class Sounds : IDisposable
{
	private readonly MemoryStream stream;

	private readonly SoundPlayer player;

	private long last;

	public Sounds(SoundSpec spec, SoundLevel level = SoundLevel.Soft)
	{
		stream = new MemoryStream();
		using (BinaryWriter binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
		{
			binaryWriter.Write("RIFF"u8);
			binaryWriter.Write(4836);
			binaryWriter.Write("WAVEfmt "u8);
			binaryWriter.Write(16);
			binaryWriter.Write((short)1);
			binaryWriter.Write((short)1);
			binaryWriter.Write(24000);
			binaryWriter.Write(48000);
			binaryWriter.Write((short)2);
			binaryWriter.Write((short)16);
			binaryWriter.Write("data"u8);
			binaryWriter.Write(4800);
			for (int i = 0; i < 2400; i++)
			{
				double num = (double)i / 24000.0;
				BinaryWriter binaryWriter2 = binaryWriter;
				double num2 = Math.Sin(Math.PI * 2.0 * ((double)spec.Frequency * num - 700.0 * num * num)) * Math.Exp((0.0 - num) * (double)spec.Decay) * 32767.0 * (double)spec.Volume;
				binaryWriter2.Write((short)(num2 * level switch
				{
					SoundLevel.Off => 0.0, 
					SoundLevel.Soft => 0.4, 
					SoundLevel.Normal => 0.7, 
					_ => 1.0, 
				}));
			}
		}
		stream.Position = 0L;
		player = new SoundPlayer(stream);
	}

	public void Pop()
	{
		if (Environment.TickCount64 - last < 400)
		{
			return;
		}
		last = Environment.TickCount64;
		try
		{
			player.Play();
		}
		catch (Exception error)
		{
			Log.Error("sound", error);
		}
	}

	public void Dispose()
	{
		player.Dispose();
		stream.Dispose();
	}
}
