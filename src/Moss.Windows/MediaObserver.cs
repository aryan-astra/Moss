using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moss.Core;
using NAudio.Dmo;
using NAudio.Wave;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Moss.Windows;

internal sealed class MediaObserver : IDisposable
{
	private GlobalSystemMediaTransportControlsSessionManager? manager;

	private GlobalSystemMediaTransportControlsSession? session;

	private WasapiLoopbackCapture? audio;

	private readonly List<GlobalSystemMediaTransportControlsSession> subscriptions = new List<GlobalSystemMediaTransportControlsSession>();

	private bool busy;

	private bool metadataBusy;

	private bool disposed;

	private bool sessionPlaying;

	private readonly OutputActivity output = new OutputActivity();

	private long timelineRead;

	private double playbackRate = 1.0;

	private int generation;

	private int revision = 1;

	private int seenRevision = -1;

	private int metadataRevision = -1;

	private int audioStopped;

	private bool simulateTrackBump;

	private int failures;

	private long nextPoll;

	private long lastAudioTry = -30000L;

	private long nextMetadata;

	private readonly RhythmDetector detector = new RhythmDetector();

	private volatile float pulse;

	private string sessionStatus = "Waiting for a media session";

	public bool? SimulatedPlaying { get; private set; }

	public void SimulatePlaying(bool? playing)
	{
		SimulatedPlaying = playing;
		if (playing.HasValue)
		{
			sessionPlaying = playing.Value;
			State.Update(playing.Value);
		}
	}

	public void SimulatePulse(float amount)
	{
		pulse = Math.Clamp(amount, 0f, 1f);
	}

	public void SimulateTrackChange()
	{
		simulateTrackBump = true;
		State.Update(State.Playing, trackChanged: true);
	}

	public bool UsingAudioFallback
	{
		get
		{
			if (!sessionPlaying)
			{
				return output.Active;
			}
			return false;
		}
	}

	public string OutputStatus => output.Status;

	public TimeSpan DisplayPosition
	{
		get
		{
			if (!(Duration <= TimeSpan.Zero))
			{
				return TimeSpan.FromSeconds(Math.Clamp(Position.TotalSeconds + (sessionPlaying ? ((double)(Environment.TickCount64 - timelineRead) / 1000.0 * playbackRate) : 0.0), 0.0, Duration.TotalSeconds));
			}
			return Position;
		}
	}

	public MediaState State { get; } = new MediaState();

	public string Status
	{
		get
		{
			if (SimulatedPlaying.HasValue)
			{
				return SimulatedPlaying.Value ? "Simulated playback (Feature Lab)" : "Simulated pause (Feature Lab)";
			}
			if (!UsingAudioFallback)
			{
				return sessionStatus;
			}
			return "Audio detected from output levels · track identity unavailable";
		}
		private set
		{
			sessionStatus = value;
		}
	}

	public string RhythmStatus { get; private set; } = "Audio analysis off";

	public string? Title { get; private set; }

	public string? Artist { get; private set; }

	public string? Album { get; private set; }

	public string? Player { get; private set; }

	public TimeSpan Position { get; private set; }

	public TimeSpan Duration { get; private set; }

	public byte[]? Artwork { get; private set; }

	public float Pulse => pulse;

	public void Decay(float dt)
	{
		pulse *= MathF.Exp((0f - dt) * 10f);
	}

	private void PlaybackChanged(GlobalSystemMediaTransportControlsSession _, PlaybackInfoChangedEventArgs __)
	{
		Interlocked.Increment(ref revision);
	}

	private void PropertiesChanged(GlobalSystemMediaTransportControlsSession _, MediaPropertiesChangedEventArgs __)
	{
		Interlocked.Increment(ref revision);
	}

	private void CurrentChanged(GlobalSystemMediaTransportControlsSessionManager _, CurrentSessionChangedEventArgs __)
	{
		Interlocked.Increment(ref revision);
	}

	private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager _, SessionsChangedEventArgs __)
	{
		Interlocked.Increment(ref revision);
	}

	public async void Update(Settings settings)
	{
		if (disposed)
		{
			return;
		}
		if (SimulatedPlaying.HasValue)
		{
			sessionPlaying = SimulatedPlaying.Value;
		}
		detector.Sensitivity = settings.Advanced.RhythmSensitivity;
		output.Update(settings.Media && settings.AudioLevelFallback);
		State.Update(settings.Media && (sessionPlaying || output.Active));
		if (!settings.Media)
		{
			StopObservation();
			sessionPlaying = false;
			State.Update(playing: false);
			Status = "Music awareness off";
			StopAudio();
			return;
		}
		if (!settings.MediaMetadata)
		{
			ClearMetadata();
		}
		if (Interlocked.Exchange(ref audioStopped, 0) != 0)
		{
			StopAudio();
		}
		if (!settings.RhythmAnalysis)
		{
			StopAudio();
		}
		else if (audio == null && Environment.TickCount64 - lastAudioTry > 15000)
		{
			StartAudio();
		}
		long now = Environment.TickCount64;
		if (busy || (now < nextPoll && seenRevision == Volatile.Read(in revision)) || now < nextPoll - 800)
		{
			return;
		}
		busy = true;
		int token = generation;
		nextPoll = now + 1000;
		try
		{
			if (manager == null)
			{
				GlobalSystemMediaTransportControlsSessionManager globalSystemMediaTransportControlsSessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
				if (disposed || token != generation || !settings.Media)
				{
					return;
				}
				manager = globalSystemMediaTransportControlsSessionManager;
				manager.CurrentSessionChanged += CurrentChanged;
				manager.SessionsChanged += SessionsChanged;
			}
			GlobalSystemMediaTransportControlsSession[] array = manager.GetSessions().ToArray();
			failures = 0;
			RefreshSubscriptions(array);
			GlobalSystemMediaTransportControlsSession currentSession = manager.GetCurrentSession();
			List<GlobalSystemMediaTransportControlsSession> list = new List<GlobalSystemMediaTransportControlsSession>();
			List<PlaybackCandidate> list2 = new List<PlaybackCandidate>();
			GlobalSystemMediaTransportControlsSession[] array2 = array;
			foreach (GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession in array2)
			{
				try
				{
					bool playing = globalSystemMediaTransportControlsSession.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
					list.Add(globalSystemMediaTransportControlsSession);
					list2.Add(new PlaybackCandidate(playing, object.Equals(globalSystemMediaTransportControlsSession, currentSession), object.Equals(globalSystemMediaTransportControlsSession, session)));
				}
				catch
				{
				}
			}
			int num = MediaSelection.Choose(list2);
			GlobalSystemMediaTransportControlsSession objA = ((num < 0) ? null : list[num]);
			bool flag = !object.Equals(objA, session);
			session = objA;
			if (flag)
			{
				ClearMetadata();
				metadataRevision = -1;
			}
			bool flag2 = (sessionPlaying = SimulatedPlaying ?? (num >= 0 && list2[num].Playing));
			seenRevision = Volatile.Read(in revision);
			State.Update(flag2 || output.Active, flag || simulateTrackBump);
			simulateTrackBump = false;
			Status = ((num < 0) ? "No player is sharing Windows media controls" : (flag2 ? "Music detected — a Windows media session is playing" : "Players are paused or stopped"));
			if (session == null || !settings.MediaMetadata)
			{
				return;
			}
			try
			{
				GlobalSystemMediaTransportControlsSessionTimelineProperties timelineProperties = session.GetTimelineProperties();
				Position = timelineProperties.Position - timelineProperties.StartTime;
				Duration = timelineProperties.EndTime - timelineProperties.StartTime;
				playbackRate = session.GetPlaybackInfo().PlaybackRate ?? 1.0;
				double totalSeconds = (DateTimeOffset.UtcNow - timelineProperties.LastUpdatedTime).TotalSeconds;
				if (flag2 && totalSeconds >= 0.0 && totalSeconds < 86400.0)
				{
					Position += TimeSpan.FromSeconds(totalSeconds * playbackRate);
				}
				timelineRead = Environment.TickCount64;
			}
			catch
			{
				MediaObserver mediaObserver = this;
				TimeSpan position = (Duration = TimeSpan.Zero);
				mediaObserver.Position = position;
			}
			if (!metadataBusy && now >= nextMetadata && (metadataRevision != seenRevision || Title == null))
			{
				nextMetadata = now + 3000;
				_ = ReadDetailsAsync(session, settings, token, seenRevision);
			}
		}
		catch (Exception error)
		{
			Log.Error("media-state", error);
			Status = "Windows media controls unavailable; retrying";
			sessionPlaying = false;
			State.Update(output.Active);
			nextPoll = now + Math.Min(60000, 5000 * (1 << Math.Min(4, failures++)));
			seenRevision = Volatile.Read(in revision);
		}
		finally
		{
			busy = false;
		}
	}

	private static string? Summary(string? value)
	{
		if (value != null)
		{
			if (value.Length > 240)
			{
				return value.Substring(0, 240);
			}
			return value;
		}
		return null;
	}

	private async Task ReadDetailsAsync(GlobalSystemMediaTransportControlsSession source, Settings settings, int token, int observedRevision)
	{
		metadataBusy = true;
		try
		{
			GlobalSystemMediaTransportControlsSessionMediaProperties globalSystemMediaTransportControlsSessionMediaProperties = await source.TryGetMediaPropertiesAsync();
			if (disposed || generation != token || !settings.Media || !settings.MediaMetadata || !object.Equals(source, session))
			{
				return;
			}
			Title = Summary(globalSystemMediaTransportControlsSessionMediaProperties.Title);
			Artist = Summary(globalSystemMediaTransportControlsSessionMediaProperties.Artist);
			Album = Summary(globalSystemMediaTransportControlsSessionMediaProperties.AlbumTitle);
			Player = Summary(source.SourceAppUserModelId);
			metadataRevision = observedRevision;
			Artwork = null;
			if (globalSystemMediaTransportControlsSessionMediaProperties.Thumbnail == null)
			{
				return;
			}
			using IRandomAccessStreamWithContentType stream = await globalSystemMediaTransportControlsSessionMediaProperties.Thumbnail.OpenReadAsync();
			if (stream.Size > 2097152)
			{
				return;
			}
			using DataReader reader = new DataReader(stream);
			uint size = (uint)stream.Size;
			await reader.LoadAsync(size);
			byte[] array = new byte[size];
			reader.ReadBytes(array);
			if (!disposed && generation == token && settings.MediaMetadata && settings.Media && object.Equals(source, session))
			{
				Artwork = array;
			}
		}
		catch (Exception error)
		{
			Log.Error("media-details", error);
		}
		finally
		{
			metadataBusy = false;
		}
	}

	private void RefreshSubscriptions(GlobalSystemMediaTransportControlsSession[] all)
	{
		for (int num = subscriptions.Count - 1; num >= 0; num--)
		{
			if (!all.Contains(subscriptions[num]))
			{
				Unsubscribe(subscriptions[num]);
				subscriptions.RemoveAt(num);
			}
		}
		foreach (GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession in all)
		{
			if (!subscriptions.Contains(globalSystemMediaTransportControlsSession))
			{
				globalSystemMediaTransportControlsSession.PlaybackInfoChanged += PlaybackChanged;
				globalSystemMediaTransportControlsSession.MediaPropertiesChanged += PropertiesChanged;
				subscriptions.Add(globalSystemMediaTransportControlsSession);
			}
		}
	}

	private void Unsubscribe(GlobalSystemMediaTransportControlsSession s)
	{
		try
		{
			s.PlaybackInfoChanged -= PlaybackChanged;
			s.MediaPropertiesChanged -= PropertiesChanged;
		}
		catch
		{
		}
	}

	private void StopObservation()
	{
		if (manager == null && session == null && subscriptions.Count == 0 && !busy)
		{
			ClearMetadata();
			return;
		}
		generation++;
		foreach (GlobalSystemMediaTransportControlsSession subscription in subscriptions)
		{
			Unsubscribe(subscription);
		}
		subscriptions.Clear();
		session = null;
		if (manager != null)
		{
			manager.CurrentSessionChanged -= CurrentChanged;
			manager.SessionsChanged -= SessionsChanged;
			manager = null;
		}
		ClearMetadata();
		nextPoll = 0L;
	}

	private void ClearMetadata()
	{
		string text = (Player = null);
		string text3 = (Album = text);
		string title = (Artist = text3);
		Title = title;
		Artwork = null;
		TimeSpan position = (Duration = TimeSpan.Zero);
		Position = position;
		metadataRevision = -1;
	}

	private void StartAudio()
	{
		lastAudioTry = Environment.TickCount64;
		try
		{
			audio = new WasapiLoopbackCapture();
			audio.DataAvailable += OnAudio;
			audio.RecordingStopped += OnStopped;
			audio.StartRecording();
			RhythmStatus = "Local speaker-output onset analysis active";
		}
		catch (Exception error)
		{
			Log.Error("audio", error);
			StopAudio();
			RhythmStatus = "Output analysis unavailable; playback reactions still work";
		}
	}

	private void OnStopped(object? sender, StoppedEventArgs e)
	{
		Interlocked.Exchange(ref audioStopped, 1);
		if (e.Exception != null)
		{
			Log.Error("audio", e.Exception);
		}
	}

	private void OnAudio(object? sender, WaveInEventArgs e)
	{
		if (!(sender is WasapiLoopbackCapture wasapiLoopbackCapture))
		{
			return;
		}
		int num = wasapiLoopbackCapture.WaveFormat.BitsPerSample / 8;
		if (num != 4 && num != 2)
		{
			return;
		}
		double num2 = 0.0;
		int num3 = 0;
		bool flag = wasapiLoopbackCapture.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat || (wasapiLoopbackCapture.WaveFormat is WaveFormatExtensible waveFormatExtensible && waveFormatExtensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);
		for (int i = 0; i + num <= e.BytesRecorded; i += num * 4)
		{
			float num4 = (flag ? BitConverter.ToSingle(e.Buffer, i) : ((num == 2) ? ((float)BitConverter.ToInt16(e.Buffer, i) / 32768f) : ((float)BitConverter.ToInt32(e.Buffer, i) / 2.1474836E+09f)));
			if (float.IsFinite(num4))
			{
				num2 += (double)(num4 * num4);
				num3++;
			}
		}
		float num5 = detector.PushRms((float)Math.Sqrt(num2 / (double)Math.Max(1, num3)), Environment.TickCount64);
		if (num5 > 0f)
		{
			pulse = num5;
		}
	}

	private void StopAudio()
	{
		WasapiLoopbackCapture wasapiLoopbackCapture = audio;
		audio = null;
		if (wasapiLoopbackCapture != null)
		{
			wasapiLoopbackCapture.DataAvailable -= OnAudio;
			wasapiLoopbackCapture.RecordingStopped -= OnStopped;
			try
			{
				wasapiLoopbackCapture.StopRecording();
			}
			catch
			{
			}
			wasapiLoopbackCapture.Dispose();
		}
		pulse = 0f;
		RhythmStatus = "Audio analysis off";
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			StopObservation();
			StopAudio();
			output.Dispose();
		}
	}
}
