using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Moss.Core;

public sealed class Character
{
	public int FormatVersion { get; set; } = 1;

	public string Name { get; set; } = "Moss";

	public string Rig { get; set; } = "bean-1";

	public string Species { get; set; } = "bean";

	public string Paper { get; set; } = "#F5F1DF";

	public string MusicProp { get; set; } = "headphones";

	public SpriteAtlasRef? SpriteAtlas { get; set; }

	public string Body { get; set; } = "#A9BB82";

	public string Belly { get; set; } = "#E4E9CC";

	public string Ink { get; set; } = "#263B32";

	public string Accent { get; set; } = "#EBA18E";

	public float Width { get; set; } = 66f;

	public float Height { get; set; } = 74f;

	public float EarLength { get; set; } = 23f;

	public bool Headphones { get; set; } = true;

	public SoundSpec Sound { get; set; } = new SoundSpec();

	public Personality Personality { get; set; } = new Personality();

	public Dictionary<string, Clip> Animations { get; set; } = new Dictionary<string, Clip>();

	public static Character MigrationDefaults()
	{
		Character defaults = new Character();
		defaults.Animations["Hanging"] = new Clip { Rate = 1.2f, Bob = 0.4f, Lean = 0f, Crouch = 0.08f, Eyes = 1f, Ears = 0.1f, Arms = 2.5f, Duration = 1.6f, Loop = true, Markers = Array.Empty<float>() };
		defaults.Animations["Carrying"] = new Clip { Rate = 2f, Bob = 1.6f, Lean = 1.5f, Crouch = 0.05f, Eyes = 1f, Ears = 0f, Arms = 1.8f, Duration = 1f, Loop = true, Markers = Array.Empty<float>() };
		defaults.Animations["Hammering"] = new Clip { Rate = 3.2f, Bob = 2.2f, Lean = 1f, Crouch = 0.12f, Eyes = 1f, Ears = 0f, Arms = 3f, Duration = 0.9f, Loop = true, Markers = new float[1] };
		defaults.Animations["Peeking"] = new Clip { Rate = 0.8f, Bob = 0.3f, Lean = 2f, Crouch = 0f, Eyes = 1f, Ears = 1f, Arms = 0f, Duration = 2.2f, Loop = true, Markers = Array.Empty<float>() };
		defaults.Animations["Balancing"] = new Clip { Rate = 1.4f, Bob = 1.2f, Lean = 0f, Crouch = 0.15f, Eyes = 1f, Ears = 0.3f, Arms = 2.2f, Duration = 1.2f, Loop = true, Markers = Array.Empty<float>() };
		defaults.Animations["Hammering"].Markers[0] = 0.5f;
		return defaults;
	}

	public void FillMissingClips(Character defaults)
	{		foreach (string name in Enum.GetNames<Motion>())
		{
			if (!Animations.ContainsKey(name) && defaults.Animations.TryGetValue(name, out Clip? clip) && clip != null)
			{
				Animations[name] = new Clip
				{
					Rate = clip.Rate,
					Bob = clip.Bob,
					Lean = clip.Lean,
					Crouch = clip.Crouch,
					Eyes = clip.Eyes,
					Ears = clip.Ears,
					Arms = clip.Arms,
					Duration = clip.Duration,
					Loop = clip.Loop,
					Markers = (float[])clip.Markers.Clone(),
					Frames = (string[])clip.Frames.Clone()
				};
			}
		}
	}

	public static Character Load(string file)
	{		FileInfo fileInfo = new FileInfo(file);
		if (!fileInfo.Exists || fileInfo.Length > 262144)
		{
			throw new InvalidDataException("Character file is absent or exceeds 256 KiB.");
		}
		Character? obj = JsonSerializer.Deserialize<Character>(File.ReadAllText(file), Json.Options) ?? throw new InvalidDataException("Empty character.");
		obj.Validate();
		return obj;
	}

	public void Validate()
	{
		bool flag;
		switch (Species)
		{
		case "bean":
		case "bird":
		case "cat":
		case "dog":
		case "octopus":
		case "penguin":
		case "rabbit":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			throw new InvalidDataException("Unsupported species.");
		}
		switch (MusicProp)
		{
		case "headphones":
		case "radio":
		case "turntable":
		case "cassette":
		case "cd":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			throw new InvalidDataException("Unsupported accessory.");
		}
		if (SpriteAtlas != null)
		{
			SpriteAtlas.Validate();
		}
		if (FormatVersion != 1 || Rig != "bean-1")
		{
			throw new InvalidDataException("Unsupported character format or rig.");
		}
		if (string.IsNullOrWhiteSpace(Name) || Name.Length > 40)
		{
			throw new InvalidDataException("Invalid character name.");
		}
		string[] array = new string[5] { Body, Belly, Ink, Accent, Paper };
		foreach (string text in array)
		{
			if (text == null || !Regex.IsMatch(text, "^#[0-9a-fA-F]{6}$"))
			{
				throw new InvalidDataException("Colors must use #RRGGBB.");
			}
		}
		flag = !float.IsFinite(Width + Height + EarLength);
		if (!flag)
		{
			float width = Width;
			bool flag2 = ((width < 40f || width > 90f) ? true : false);
			flag = flag2;
		}
		bool flag3 = flag;
		if (!flag3)
		{
			float width = Height;
			bool flag2 = ((width < 40f || width > 100f) ? true : false);
			flag3 = flag2;
		}
		bool flag4 = flag3;
		if (!flag4)
		{
			float width = EarLength;
			bool flag2 = ((width < 0f || width > 35f) ? true : false);
			flag4 = flag2;
		}
		if (flag4)
		{
			throw new InvalidDataException("Rig dimensions out of range.");
		}
		if (Personality == null || Animations == null || Animations.Count > 40)
		{
			throw new InvalidDataException("Invalid character sections.");
		}
		Personality.Validate();
		if (Sound == null)
		{
			throw new InvalidDataException("Missing sound configuration.");
		}
		Sound.Validate();
		array = Enum.GetNames<Motion>();
		foreach (string text2 in array)
		{
			if (!Animations.ContainsKey(text2))
			{
				throw new InvalidDataException("Missing animation: " + text2);
			}
		}
		foreach (Clip value in Animations.Values)
		{
			if (value == null || value.Markers == null || value.Markers.Length > 16)
			{
				throw new InvalidDataException("Invalid clip.");
			}
			float[] array2 = new float[8] { value.Rate, value.Bob, value.Lean, value.Crouch, value.Eyes, value.Ears, value.Arms, value.Duration };
			foreach (float num in array2)
			{
				if (!float.IsFinite(num) || Math.Abs(num) > 30f)
				{
					throw new InvalidDataException("Invalid animation parameter.");
				}
			}
			flag4 = value.Rate <= 0f || value.Duration <= 0f;
			if (!flag4)
			{
				float width = value.Eyes;
				flag = ((width < 0f || width > 1f) ? true : false);
				flag4 = flag;
			}
			flag3 = flag4;
			if (!flag3)
			{
				float width = value.Crouch;
				flag = ((width < -0.3f || width > 0.65f) ? true : false);
				flag3 = flag;
			}
			if (flag3)
			{
				throw new InvalidDataException("Animation out of range.");
			}
			if (value.Markers.Any((float m) => !float.IsFinite(m) || m < 0f || m > 1f))
			{
				throw new InvalidDataException("Invalid marker.");
			}
			if (value.Frames == null || value.Frames.Length > 8)
			{
				throw new InvalidDataException("Invalid frames.");
			}
			foreach (string frame in value.Frames)
			{
				if (string.IsNullOrEmpty(frame) || frame.Length > 64)
				{
					throw new InvalidDataException("Invalid frame key.");
				}
				foreach (char ch in frame)
				{
					bool ok = char.IsAsciiLetterOrDigit(ch) || ch == '#' || ch == '_' || ch == '-' || ch == '.';
					if (!ok)
					{
						throw new InvalidDataException("Invalid frame key.");
					}
				}
			}
		}
	}
}

/// <summary>
/// Optional sprite-atlas reference for a character. Null for all procedural
/// (vector) characters. When present, the detailed grid lives in a sidecar
/// atlas file (see characters/wren/atlas.json) so character.json stays small;
/// Core never touches the filesystem for it — loading is lazy in the renderer
/// and only for the active character.
/// </summary>
public sealed class SpriteAtlasRef
{
	public string AtlasFile { get; set; } = "atlas.json";

	public string Source { get; set; } = "";

	public int SourceWidth { get; set; }

	public int SourceHeight { get; set; }

	public bool HasAlpha { get; set; }

	public string Mode { get; set; } = "reference-only";

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(AtlasFile) || AtlasFile.Length > 256)
		{
			throw new InvalidDataException("Invalid atlas file reference.");
		}
		foreach (char c in AtlasFile)
		{
			bool ok = char.IsAsciiLetterOrDigit(c) || c == '.' || c == '-' || c == '_';
			if (!ok)
			{
				throw new InvalidDataException("Invalid atlas file reference.");
			}
		}
		if (Source == null || Source.Length > 256 || Mode == null || Mode.Length > 32)
		{
			throw new InvalidDataException("Invalid atlas metadata.");
		}
		if (SourceWidth < 0 || SourceWidth > 8192 || SourceHeight < 0 || SourceHeight > 8192)
		{
			throw new InvalidDataException("Invalid atlas dimensions.");
		}
	}
}
