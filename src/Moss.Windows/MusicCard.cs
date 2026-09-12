using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Moss.Windows;

internal sealed class MusicCard : Form
{
	private readonly PetApplication app;

	private byte[]? artworkSource;

	private Bitmap? thumbnail;

	private readonly Timer timer = new Timer
	{
		Interval = 250
	};

	private readonly Font titleFont = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Pixel);

	private readonly Font small = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);

	private readonly Font caption = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel);

	private readonly Button consent = new Button
	{
		Text = "Allow song details",
		FlatStyle = FlatStyle.Flat
	};

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ClassStyle |= 131072;
			return createParams;
		}
	}

	public MusicCard(PetApplication owner)
	{
		app = owner;
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.AutoScaleMode = AutoScaleMode.Dpi;
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.ClientSize = new Size(338, 238);
		BackColor = Color.FromArgb(246, 244, 235);
		DoubleBuffered = true;
		Font = small;
		base.KeyPreview = true;
		Button button = new Button
		{
			Text = "×",
			FlatStyle = FlatStyle.Flat,
			Bounds = new Rectangle(300, 8, 28, 27),
			AccessibleName = "Close music card"
		};
		button.FlatAppearance.BorderSize = 0;
		button.Click += delegate
		{
			Close();
		};
		base.Controls.Add(button);
		consent.Visible = !app.Config.MediaMetadata;
		consent.SetBounds(20, 190, 180, 29);
		consent.Click += delegate
		{
			try
			{
				app.Config.MediaMetadata = true;
				app.Save();
				Invalidate();
			}
			catch (Exception error)
			{
				Log.Error("music-consent", error);
				MessageBox.Show(this, "The preference could not be saved.", "Moss");
			}
		};
		base.Controls.Add(consent);
		timer.Tick += delegate
		{
			consent.Visible = !app.Config.MediaMetadata;
			RefreshArtwork();
			Invalidate();
		};
		timer.Start();
		base.Deactivate += delegate
		{
			Close();
		};
		base.KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				Close();
			}
		};
		base.Shown += delegate
		{
			Rectangle workingArea = Screen.FromPoint(new Point((int)app.Creature.Position.X, (int)app.Creature.Position.Y)).WorkingArea;
			base.Location = new Point(Math.Clamp((int)app.Creature.Position.X - base.Width / 2, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - base.Width)), Math.Clamp((int)app.Creature.Position.Y - base.Height - 35, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - base.Height)));
		};
	}

	private void RefreshArtwork()
	{
		byte[] array = ((app.Config.MediaMetadata && !app.Media.UsingAudioFallback) ? app.Media.Artwork : null);
		if (array == artworkSource)
		{
			return;
		}
		artworkSource = array;
		thumbnail?.Dispose();
		thumbnail = null;
		if (array == null)
		{
			return;
		}
		try
		{
			using MemoryStream stream = new MemoryStream(array, writable: false);
			using Image image = Image.FromStream(stream);
			if (image.Width > 1024 || image.Height > 1024 || image.Width <= 0 || image.Height <= 0)
			{
				return;
			}
			thumbnail = new Bitmap(40, 40);
			using Graphics graphics = Graphics.FromImage(thumbnail);
			graphics.Clear(BackColor);
			float num = Math.Min(40f / (float)image.Width, 40f / (float)image.Height);
			float num2 = (float)image.Width * num;
			float num3 = (float)image.Height * num;
			graphics.DrawImage(image, (40f - num2) / 2f, (40f - num3) / 2f, num2, num3);
		}
		catch
		{
			thumbnail?.Dispose();
			thumbnail = null;
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		float num = (float)base.DeviceDpi / 96f;
		graphics.ScaleTransform(num, num);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(38, 59, 50)))
		{
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb(213, 225, 197));
			using SolidBrush brush3 = new SolidBrush(Color.FromArgb(103, 111, 98));
			graphics.FillEllipse(brush2, 20, 17, 34, 34);
			graphics.FillEllipse(brush, 32, 29, 10, 10);
			if (thumbnail != null)
			{
				graphics.DrawImage(thumbnail, 17, 14, 40, 40);
			}
			graphics.DrawString(app.Media.State.Playing ? "NOW PLAYING" : "MUSIC", caption, brush, 65f, 25f);
			bool usingAudioFallback = app.Media.UsingAudioFallback;
			string s = ((!app.Config.Media) ? "Music awareness is off" : (usingAudioFallback ? "Audio is playing" : ((!app.Config.MediaMetadata) ? "Your music, nearby" : (string.IsNullOrWhiteSpace(app.Media.Title) ? "Waiting for song details" : app.Media.Title))));
			using StringFormat format = new StringFormat
			{
				Trimming = StringTrimming.EllipsisCharacter
			};
			graphics.DrawString(s, titleFont, brush, new RectangleF(20f, 65f, 296f, 57f), format);
			string s2 = (usingAudioFallback ? "Output detected · song information unavailable" : ((!app.Config.MediaMetadata) ? "Song details are private until you allow them." : (string.IsNullOrWhiteSpace(app.Media.Artist) ? "Shared by Windows media controls" : app.Media.Artist)));
			graphics.DrawString(s2, small, brush3, new RectangleF(20f, 128f, 296f, 36f), format);
			if (app.Config.MediaMetadata && !usingAudioFallback && app.Media.Duration > TimeSpan.Zero)
			{
				float num2 = (float)Math.Clamp(app.Media.DisplayPosition.TotalSeconds / app.Media.Duration.TotalSeconds, 0.0, 1.0);
				graphics.FillRoundedRectangle(brush2, new RectangleF(20f, 180f, 296f, 5f), 2f);
				if (num2 > 0f)
				{
					graphics.FillRectangle(brush, 20f, 180f, 296f * num2, 5f);
				}
				graphics.DrawString(Clock(app.Media.DisplayPosition), small, brush3, 20f, 196f);
				graphics.DrawString(Clock(app.Media.Duration), small, brush3, 267f, 196f);
			}
			else if (app.Config.MediaMetadata)
			{
				graphics.DrawString(usingAudioFallback ? "This player is not sharing track details" : "Timeline unavailable from this player", small, brush3, 20f, 195f);
			}
		}
		static string Clock(TimeSpan t)
		{
			if (!(t.TotalHours >= 1.0))
			{
				return t.ToString("m\\:ss");
			}
			return t.ToString("h\\:mm\\:ss");
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			timer.Dispose();
			thumbnail?.Dispose();
			thumbnail = null;
			artworkSource = null;
		}
		base.Dispose(disposing);
		if (disposing)
		{
			titleFont.Dispose();
			small.Dispose();
			caption.Dispose();
		}
	}
}
