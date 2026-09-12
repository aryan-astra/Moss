using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Moss.Core;

public sealed class Settings
{
	public int Version { get; set; } = 1;

	public string ActivePet { get; set; } = "moss";

	public Dictionary<string, PetProfile> Pets { get; set; } = new Dictionary<string, PetProfile>();

	public MusicStyle MusicStyle { get; set; } = MusicStyle.Dance;

	public bool ReducedMotion { get; set; }

	public SoundLevel SoundLevel { get; set; } = SoundLevel.Soft;

	public bool ReminderAlerts { get; set; } = true;

	public bool ClosedAppReminders { get; set; }

	public float PetScale { get; set; } = 1f;

	public int FrameLimit { get; set; } = 240;

	[JsonIgnore]
	public PetProfile Profile
	{
		get
		{
			if (!Pets.TryGetValue(ActivePet, out PetProfile value))
			{
				value = (Pets[ActivePet] = new PetProfile());
			}
			return value;
		}
	}

	public bool Visible { get; set; } = true;

	public bool Interaction { get; set; } = true;

	public bool CursorAwareness { get; set; } = true;

	public bool WindowGeometry { get; set; } = true;

	public bool ForegroundAwareness { get; set; } = true;

	public bool ApplicationIdentity { get; set; }

	public bool Media { get; set; } = true;

	public bool AudioLevelFallback { get; set; } = true;

	public bool MusicComments { get; set; } = true;

	public bool SurprisePlay { get; set; } = true;

	public bool MediaMetadata { get; set; }

	public bool RhythmAnalysis { get; set; }

	public bool Notifications { get; set; }

	public bool SoundEffects { get; set; }

	public bool ExcludeFromCapture { get; set; } = true;

	public bool Debug { get; set; }

	public QuietPolicy Fullscreen { get; set; }

	public QuietPolicy Presentation { get; set; }

	public PerformanceMode Performance { get; set; }

	public float Sensitivity { get; set; } = 1f;

	public float Activity { get; set; } = 1f;

	public bool OverridePersonality { get; set; }

	public Personality Personality { get; set; } = new Personality();

	public string CharacterPath { get; set; } = "";

	public void Validate()
	{
		if (Version != 1 || !Enum.IsDefined(Fullscreen) || !Enum.IsDefined(Presentation) || !Enum.IsDefined(Performance))
		{
			throw new InvalidDataException("Unsupported settings.");
		}
		if (!float.IsFinite(Sensitivity) || !float.IsFinite(Activity))
		{
			throw new InvalidDataException("Invalid settings.");
		}
		Sensitivity = Math.Clamp(Sensitivity, 0.5f, 2f);
		Activity = Math.Clamp(Activity, 0.3f, 1.7f);
		if (Personality == null || CharacterPath == null || CharacterPath.Length > 4096)
		{
			throw new InvalidDataException("Invalid settings fields.");
		}
		if (Pets == null || ActivePet == null || Pets.Count > 100 || ActivePet.Length > 100 || !Regex.IsMatch(ActivePet, "^[a-zA-Z0-9-]+$") || !Enum.IsDefined(SoundLevel) || !Enum.IsDefined(MusicStyle))
		{
			throw new InvalidDataException("Invalid profiles.");
		}
		bool flag;
		foreach (PetProfile value in Pets.Values)
		{
			flag = value == null || value.Name == null || value.Name.Length > 40 || !float.IsFinite(value.Size);
			if (!flag)
			{
				float size = value.Size;
				bool flag2 = ((size < 0.6f || size > 1.7f) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				throw new InvalidDataException("Invalid pet profile.");
			}
		}
		foreach (PetProfile value2 in Pets.Values)
		{
			value2.Memory?.Validate();
		}
		flag = !float.IsFinite(PetScale);
		if (!flag)
		{
			float size = PetScale;
			bool flag2 = ((size < 0.6f || size > 1.7f) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			PetScale = 1f;
		}
		FrameLimit = Math.Clamp(FrameLimit, 15, 360);
		Personality.Validate();
	}

	public static Settings Load(string path)
	{
		if (!File.Exists(path))
		{
			return new Settings();
		}
		if (new FileInfo(path).Length > 65536)
		{
			throw new InvalidDataException("Settings too large.");
		}
		Settings? obj = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Json.Options) ?? throw new InvalidDataException("Empty settings.");
		obj.Validate();
		return obj;
	}

	public void Save(string path)
	{
		Validate();
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
		string text = path + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(this, Json.Options));
		File.Move(text, path, overwrite: true);
	}
}
