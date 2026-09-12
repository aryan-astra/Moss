using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Moss.Core;

public sealed class DocumentStore(string root)
{
	private readonly HashSet<string> recovered = new HashSet<string>();

	public List<string> RecoveryWarnings { get; } = new List<string>();

	public string Root => root;

	public void Save<T>(string collection, Guid id, T value)
	{
		string text = Path.Combine(DirectoryFor(collection), id.ToString("N") + ".json");
		byte[] array = JsonSerializer.SerializeToUtf8Bytes(value, Json.Options);
		if (array.Length > 8000000)
		{
			throw new InvalidDataException("Document too large after encoding.");
		}
		string text2 = text + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text2, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
			{
				fileStream.Write(array);
				fileStream.Flush(flushToDisk: true);
			}
			if (recovered.Contains(text))
			{
				File.Copy(text, text + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), overwrite: false);
				File.Move(text2, text, overwrite: true);
				recovered.Remove(text);
			}
			else if (File.Exists(text))
			{
				File.Replace(text2, text, text + ".bak", ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(text2, text);
			}
		}
		finally
		{
			if (File.Exists(text2))
			{
				File.Delete(text2);
			}
		}
	}

	public List<T> LoadAll<T>(string collection, Action<T> validate)
	{
		List<T> list = new List<T>();
		foreach (string item in Directory.EnumerateFiles(DirectoryFor(collection), "*.json"))
		{
			try
			{
				list.Add(Read(item, validate));
			}
			catch (Exception ex) when (((ex is IOException || ex is JsonException || ex is InvalidDataException || ex is ArgumentException) ? 1 : 0) != 0)
			{
				try
				{
					list.Add(Read(item + ".bak", validate));
					recovered.Add(item);
					RecoveryWarnings.Add("Recovered a document from its backup. The damaged file is preserved until you save.");
				}
				catch (Exception ex2) when (((ex2 is IOException || ex2 is JsonException || ex2 is InvalidDataException || ex2 is ArgumentException) ? 1 : 0) != 0)
				{
					RecoveryWarnings.Add("A document could not be read. Its original and backup were retained; no data was deleted.");
				}
			}
		}
		return list;
	}

	private static T Read<T>(string path, Action<T> validate)
	{
		if (new FileInfo(path).Length > 8000000)
		{
			throw new InvalidDataException("Document too large.");
		}
		T val = JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Json.Options);
		if (val == null)
		{
			throw new InvalidDataException("Empty document.");
		}
		T val2 = val;
		validate(val2);
		return val2;
	}

	public void Archive(string collection, Guid id)
	{
		string text = Path.Combine(DirectoryFor(collection), id.ToString("N") + ".json");
		if (File.Exists(text))
		{
			File.Move(text, text + ".deleted", overwrite: true);
		}
	}

	private string DirectoryFor(string collection)
	{
		if (!(collection == "notes") && !(collection == "reminders"))
		{
			throw new ArgumentException("Unknown collection.");
		}
		string text = Path.Combine(root, collection);
		Directory.CreateDirectory(text);
		return text;
	}
}
