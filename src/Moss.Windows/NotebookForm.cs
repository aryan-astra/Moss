using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Moss.Core;

namespace Moss.Windows;

internal sealed class NotebookForm : Form
{
	private readonly PetApplication app;

	private readonly List<Note> notes;

	private readonly ListBox index = new ListBox
	{
		BorderStyle = BorderStyle.None,
		DrawMode = DrawMode.OwnerDrawFixed,
		IntegralHeight = false,
		TabStop = true
	};

	private readonly RichTextBox editor = new RichTextBox
	{
		BorderStyle = BorderStyle.None,
		DetectUrls = false,
		AcceptsTab = true,
		ScrollBars = RichTextBoxScrollBars.Vertical,
		HideSelection = false
	};

	private readonly Label status = new Label
	{
		AutoSize = false,
		TextAlign = ContentAlignment.MiddleRight
	};

	private readonly Button add = new Button();

	private readonly Button close = new Button();

	private readonly ContextMenuStrip editMenu = new ContextMenuStrip();

	private readonly ToolTip tips = new ToolTip();

	private readonly Timer saveTimer = new Timer
	{
		Interval = 700
	};

	private readonly Timer animation = new Timer
	{
		Interval = 16
	};

	private readonly Dictionary<FontStyle, Font> fonts = new Dictionary<FontStyle, Font>();

	private readonly Font tiny = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);

	private readonly Font smallBold = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);

	private readonly Bitmap grain;

	private readonly Color paper;

	private Note? current;

	private bool loading;

	private bool dirty;

	private bool closing;

	private bool closeAllowed;

	private bool selecting;

	private float appearance;

	private float fold = 1f;

	private long lastAnimation;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ClassStyle |= 131072;
			return createParams;
		}
	}

	public NotebookForm(PetApplication owner)
	{
		app = owner;
		notes = (from n in app.Documents.LoadAll("notes", delegate(Note n)
			{
				n.Validate();
			})
			orderby n.Modified descending
			select n).ToList();
		paper = ColorTranslator.FromHtml(app.Character.Paper);
		BackColor = paper;
		ForeColor = Color.FromArgb(67, 62, 48);
		Font = EditorFont(FontStyle.Regular);
		Text = app.PetName + " · Notes";
		base.AutoScaleMode = AutoScaleMode.Dpi;
		base.FormBorderStyle = FormBorderStyle.None;
		base.ClientSize = new Size(430, 380);
		MinimumSize = new Size(320, 250);
		base.StartPosition = FormStartPosition.Manual;
		base.ShowInTaskbar = true;
		DoubleBuffered = true;
		base.KeyPreview = true;
		grain = new Bitmap(64, 64);
		Random random = new Random(113);
		for (int num = 0; num < 64; num++)
		{
			for (int num2 = 0; num2 < 64; num2++)
			{
				int num3 = random.Next(-3, 4);
				grain.SetPixel(num2, num, Color.FromArgb(Math.Clamp(paper.R + num3, 0, 255), Math.Clamp(paper.G + num3, 0, 255), Math.Clamp(paper.B + num3, 0, 255)));
			}
		}
		index.BackColor = Blend(paper, Color.FromArgb(170, 143, 89), 0.08f);
		index.ForeColor = ForeColor;
		index.Font = tiny;
		editor.BackColor = paper;
		editor.ForeColor = ForeColor;
		editor.Font = EditorFont(FontStyle.Regular);
		editor.MaxLength = 1000000;
		status.Font = tiny;
		status.ForeColor = Blend(ForeColor, paper, 0.4f);
		status.BackColor = paper;
		status.AccessibleName = "Save status";
		ConfigureButton(add, "+", "New note (Ctrl+N)");
		ConfigureButton(close, "×", "Close notes (Esc)");
		add.Click += delegate
		{
			TryAction(NewNote);
		};
		close.Click += delegate
		{
			Close();
		};
		base.Controls.AddRange(new Control[5] { index, editor, status, add, close });
		index.DrawItem += DrawIndex;
		index.SelectedIndexChanged += delegate
		{
			if (!selecting && index.SelectedItem is Note note2 && note2 != current)
			{
				if (!TrySave())
				{
					selecting = true;
					index.SelectedItem = current;
					selecting = false;
				}
				else
				{
					LoadNote(note2);
					fold = (app.Config.ReducedMotion ? 1 : 0);
					StartAnimation();
				}
			}
		};
		editor.TextChanged += delegate
		{
			MarkDirty();
		};
		saveTimer.Tick += delegate
		{
			saveTimer.Stop();
			TrySave();
		};
		base.KeyDown += KeysPressed;
		AddMenu("Undo", delegate
		{
			editor.Undo();
		}, Keys.Z | Keys.Control);
		AddMenu("Redo", delegate
		{
			editor.Redo();
		}, Keys.Y | Keys.Control);
		editMenu.Items.Add(new ToolStripSeparator());
		AddMenu("Cut", delegate
		{
			editor.Cut();
		}, Keys.X | Keys.Control);
		AddMenu("Copy", delegate
		{
			editor.Copy();
		}, Keys.C | Keys.Control);
		AddMenu("Paste as text", delegate
		{
			editor.Paste(DataFormats.GetFormat(DataFormats.UnicodeText));
		}, Keys.V | Keys.Control);
		editMenu.Items.Add(new ToolStripSeparator());
		AddMenu("Bold", delegate
		{
			Format(FontStyle.Bold);
		}, Keys.B | Keys.Control);
		AddMenu("Italic", delegate
		{
			Format(FontStyle.Italic);
		}, Keys.I | Keys.Control);
		AddMenu("Underline", delegate
		{
			Format(FontStyle.Underline);
		}, Keys.U | Keys.Control);
		editMenu.Items.Add(new ToolStripSeparator());
		AddMenu("Schedule this line…", Schedule, Keys.Return | Keys.Control);
		editor.ContextMenuStrip = editMenu;
		animation.Tick += delegate
		{
			Animate();
		};
		base.Shown += delegate
		{
			PlaceNearPet();
			appearance = (app.Config.ReducedMotion ? 1 : 0);
			base.Opacity = Math.Max(0.01, appearance);
			StartAnimation();
			editor.Focus();
		};
		base.FormClosing += OnClosing;
		base.MouseDown += delegate(object? _, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left && e.Y < ScaleDip(34f))
			{
				Native.ReleaseCapture();
				SendMessage(base.Handle, 161, 2, 0);
			}
		};
		if (notes.Count == 0)
		{
			Note note = new Note();
			app.Documents.Save("notes", note.Id, note);
			notes.Add(note);
		}
		selecting = true;
		foreach (Note note3 in notes)
		{
			index.Items.Add(note3);
		}
		index.SelectedIndex = 0;
		selecting = false;
		LoadNote(notes[0]);
		if (app.Documents.RecoveryWarnings.Count > 0)
		{
			status.Text = "Recovered notes · originals retained";
		}
		app.Events.Publish("note.opened", app.Creature.Time);
		app.Creature.Notify();
	}

	private Font EditorFont(FontStyle style)
	{
		if (!fonts.TryGetValue(style, out Font value))
		{
			value = (fonts[style] = new Font("Segoe UI", 10.5f, style, GraphicsUnit.Point));
		}
		return value;
	}

	private void ConfigureButton(Button b, string text, string hint)
	{
		b.Text = text;
		b.FlatStyle = FlatStyle.Flat;
		b.FlatAppearance.BorderSize = 0;
		b.BackColor = paper;
		b.ForeColor = ForeColor;
		b.Font = EditorFont(FontStyle.Regular);
		b.TabStop = true;
		b.AccessibleName = hint;
		tips.SetToolTip(b, hint);
		b.FlatAppearance.MouseOverBackColor = Blend(paper, Color.FromArgb(177, 155, 108), 0.18f);
	}

	private void AddMenu(string text, Action action, Keys shortcut)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text)
		{
			ShortcutKeys = shortcut
		};
		toolStripMenuItem.Click += delegate
		{
			TryAction(action);
		};
		editMenu.Items.Add(toolStripMenuItem);
	}

	private int ScaleDip(float v)
	{
		return (int)Math.Round(v * (float)base.DeviceDpi / 96f);
	}

	protected override void OnLayout(LayoutEventArgs e)
	{
		base.OnLayout(e);
		if (index != null && editor != null)
		{
			int num = ScaleDip(14f);
			int num2 = ScaleDip(83f);
			int num3 = ScaleDip(39f);
			int num4 = ScaleDip(21f);
			add.SetBounds(ScaleDip(7f), ScaleDip(5f), ScaleDip(27f), ScaleDip(27f));
			close.SetBounds(base.ClientSize.Width - ScaleDip(34f), ScaleDip(5f), ScaleDip(27f), ScaleDip(27f));
			index.ItemHeight = ScaleDip(42f);
			index.SetBounds(ScaleDip(8f), num3, num2 - ScaleDip(8f), base.ClientSize.Height - num3 - num);
			editor.SetBounds(num2 + num, num3 + ScaleDip(3f), Math.Max(50, base.ClientSize.Width - num2 - num * 2), Math.Max(60, base.ClientSize.Height - num3 - num4 - num));
			status.SetBounds(num2 + num, base.ClientSize.Height - num4, base.ClientSize.Width - num2 - num * 2, ScaleDip(16f));
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		using TextureBrush brush = new TextureBrush(grain);
		e.Graphics.FillRectangle(brush, base.ClientRectangle);
		using SolidBrush brush2 = new SolidBrush(Blend(paper, Color.FromArgb(175, 143, 80), 0.1f));
		e.Graphics.FillRectangle(brush2, 0, 0, base.ClientSize.Width, ScaleDip(33f));
		using Pen pen = new Pen(Blend(paper, Color.FromArgb(127, 106, 62), 0.22f));
		e.Graphics.DrawLine(pen, ScaleDip(88f), ScaleDip(41f), ScaleDip(88f), base.ClientSize.Height - ScaleDip(16f));
		TextRenderer.DrawText(e.Graphics, "notes", tiny, new Rectangle(ScaleDip(43f), ScaleDip(10f), ScaleDip(100f), ScaleDip(18f)), ForeColor, TextFormatFlags.NoPadding);
		if (fold < 1f)
		{
			int num = editor.Left + (int)((float)editor.Width * fold);
			using SolidBrush brush3 = new SolidBrush(Blend(paper, Color.White, 0.24f));
			e.Graphics.FillPolygon(brush3, new Point[4]
			{
				new Point(num, ScaleDip(33f)),
				new Point(num + ScaleDip(10f), ScaleDip(39f)),
				new Point(num + ScaleDip(10f), base.ClientSize.Height - ScaleDip(19f)),
				new Point(num, base.ClientSize.Height - ScaleDip(12f))
			});
		}
		e.Graphics.DrawLine(pen, base.ClientSize.Width - 10, base.ClientSize.Height - 3, base.ClientSize.Width - 3, base.ClientSize.Height - 10);
	}

	private void DrawIndex(object? sender, DrawItemEventArgs e)
	{
		if (e.Index < 0)
		{
			return;
		}
		Note note = (Note)index.Items[e.Index];
		bool flag = (e.State & DrawItemState.Selected) != 0;
		using SolidBrush brush = new SolidBrush(flag ? Blend(paper, Color.FromArgb(170, 133, 63), 0.17f) : index.BackColor);
		e.Graphics.FillRectangle(brush, e.Bounds);
		string text = ((note.Title == "Untitled") ? NoteTitle(note.Markdown) : note.Title);
		Rectangle bounds = Rectangle.Inflate(e.Bounds, -ScaleDip(5f), -ScaleDip(5f));
		bounds.Height = ScaleDip(14f);
		TextRenderer.DrawText(e.Graphics, text, flag ? smallBold : tiny, bounds, ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		bounds.Y += ScaleDip(15f);
		TextRenderer.DrawText(e.Graphics, note.Markdown.Contains('\n') ? note.Markdown.Split('\n', 3)[1] : note.Modified.ToLocalTime().ToString("dd MMM"), tiny, bounds, Blend(ForeColor, paper, 0.38f), TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		if (index.Focused)
		{
			e.DrawFocusRectangle();
		}
	}

	private static string NoteTitle(string text)
	{
		string text2 = text.Split('\n', 2)[0].Trim();
		if (text2.Length != 0)
		{
			return text2.Substring(0, Math.Min(text2.Length, 80));
		}
		return "New note";
	}

	private void LoadNote(Note n)
	{
		loading = true;
		current = n;
		try
		{
			if (!string.IsNullOrEmpty(n.RichText))
			{
				editor.Rtf = n.RichText;
			}
			else
			{
				editor.Text = n.Markdown;
			}
		}
		catch (ArgumentException)
		{
			editor.Text = n.Markdown;
			Log.Event("notebook", "invalid-rich-text-plain-copy-loaded");
		}
		finally
		{
			loading = false;
			dirty = false;
			status.Text = "";
		}
		editor.SelectionStart = editor.TextLength;
		editor.SelectionLength = 0;
	}

	private void NewNote()
	{
		Save();
		Note note = new Note();
		app.Documents.Save("notes", note.Id, note);
		notes.Insert(0, note);
		selecting = true;
		index.Items.Insert(0, note);
		index.SelectedIndex = 0;
		selecting = false;
		LoadNote(note);
		editor.Focus();
		fold = 0f;
		StartAnimation();
	}

	private void MarkDirty()
	{
		if (!loading)
		{
			dirty = true;
			status.Text = "";
			saveTimer.Stop();
			saveTimer.Start();
		}
	}

	public void Save()
	{
		if (dirty && current != null)
		{
			Note note = current;
			Note note2 = new Note
			{
				Id = note.Id,
				Created = note.Created,
				Modified = DateTimeOffset.UtcNow,
				Title = NoteTitle(editor.Text),
				Markdown = editor.Text,
				RichText = editor.Rtf
			};
			note2.Validate();
			app.Documents.Save("notes", note2.Id, note2);
			int num = notes.IndexOf(note);
			if (num >= 0)
			{
				notes[num] = note2;
			}
			current = note2;
			selecting = true;
			int num2 = index.Items.IndexOf(note);
			if (num2 >= 0)
			{
				index.Items[num2] = note2;
				index.SelectedIndex = num2;
			}
			selecting = false;
			dirty = false;
			status.Text = "";
			app.Events.Publish("note.saved", app.Creature.Time);
		}
	}

	private bool TrySave()
	{
		try
		{
			Save();
			return true;
		}
		catch (Exception error)
		{
			Log.Error("note-save", error);
			status.Text = "Not saved — keep this note open";
			return false;
		}
	}

	private void Format(FontStyle style)
	{
		FontStyle fontStyle = editor.SelectionFont?.Style ?? FontStyle.Regular;
		editor.SelectionFont = EditorFont(fontStyle ^ style);
		MarkDirty();
		editor.Focus();
	}

	private void KeysPressed(object? sender, KeyEventArgs e)
	{
		Action action = e.KeyData switch
		{
			Keys.N | Keys.Control => NewNote, 
			Keys.S | Keys.Control => Save, 
			Keys.B | Keys.Control => delegate
			{
				Format(FontStyle.Bold);
			}, 
			Keys.I | Keys.Control => delegate
			{
				Format(FontStyle.Italic);
			}, 
			Keys.U | Keys.Control => delegate
			{
				Format(FontStyle.Underline);
			}, 
			Keys.Return | Keys.Control => Schedule, 
			Keys.Escape => base.Close, 
			Keys.V | Keys.Control => delegate
			{
				editor.Paste(DataFormats.GetFormat(DataFormats.UnicodeText));
			}, 
			_ => null, 
		};
		if (action != null)
		{
			e.SuppressKeyPress = true;
			e.Handled = true;
			TryAction(action);
		}
	}

	private void Schedule()
	{
		string text = editor.SelectedText.Trim();
		if (text.Length == 0)
		{
			text = editor.Lines.ElementAtOrDefault(editor.GetLineFromCharIndex(editor.SelectionStart)) ?? "";
		}
		CommandPreview commandPreview;
		try
		{
			commandPreview = NoteCommands.Parse(text, DateTimeOffset.Now, TimeZoneInfo.Local);
		}
		catch (FormatException ex)
		{
			MessageBox.Show(this, ex.Message, "Schedule a note");
			return;
		}
		if (MessageBox.Show(this, commandPreview.Explanation, "Schedule this?", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
		{
			Save();
			app.Reminders.Create(commandPreview, current?.Id);
			app.Creature.Notify();
			status.Text = "Reminder saved";
			if (app.Config.ClosedAppReminders && app.Reminders.SchedulerStatus.StartsWith("Windows blocked"))
			{
				MessageBox.Show(this, app.Reminders.SchedulerStatus, "Saved locally");
			}
		}
	}

	private void TryAction(Action action)
	{
		try
		{
			action();
		}
		catch (Exception error)
		{
			Log.Error("notebook-action", error);
			status.Text = "Couldn't finish — your text is still here";
		}
	}

	private void PlaceNearPet()
	{
		Rectangle workingArea = Screen.FromPoint(new Point((int)app.Creature.Position.X, (int)app.Creature.Position.Y)).WorkingArea;
		base.Location = new Point(Math.Clamp((int)app.Creature.Position.X - base.Width / 2, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - base.Width)), Math.Clamp((int)app.Creature.Position.Y - base.Height - 16, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - base.Height)));
	}

	private void StartAnimation()
	{
		if (app.Config.ReducedMotion)
		{
			appearance = (fold = 1f);
			base.Opacity = 1.0;
			Invalidate();
		}
		else
		{
			lastAnimation = Environment.TickCount64;
			animation.Start();
		}
	}

	private void Animate()
	{
		long tickCount = Environment.TickCount64;
		float num = Math.Min(0.1f, (float)(tickCount - lastAnimation) / 1000f);
		lastAnimation = tickCount;
		appearance = Math.Clamp(appearance + (float)((!closing) ? 1 : (-1)) * num / 0.14f, 0f, 1f);
		fold = Math.Clamp(fold + num / 0.16f, 0f, 1f);
		base.Opacity = Math.Max(0.01, appearance);
		Invalidate();
		if (closing && appearance <= 0f)
		{
			animation.Stop();
			closeAllowed = true;
			Close();
		}
		else if (appearance >= 1f && fold >= 1f)
		{
			animation.Stop();
		}
	}

	private void OnClosing(object? sender, FormClosingEventArgs e)
	{
		if (!TrySave())
		{
			e.Cancel = true;
		}
		else if (!closeAllowed && !app.Config.ReducedMotion && e.CloseReason == CloseReason.UserClosing)
		{
			e.Cancel = true;
			closing = true;
			StartAnimation();
		}
	}

	public void Shutdown()
	{
		Save();
		closeAllowed = true;
		Close();
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 132)
		{
			long num = ((IntPtr)m.LParam).ToInt64();
			Point point = PointToClient(new Point((short)(num & 0xFFFF), (short)((num >> 16) & 0xFFFF)));
			int num2 = ScaleDip(5f);
			bool flag = point.X < num2;
			bool flag2 = point.X >= base.ClientSize.Width - num2;
			bool num3 = point.Y < num2;
			bool flag3 = point.Y >= base.ClientSize.Height - num2;
			int num4 = ((!num3) ? ((!flag3) ? (flag ? 10 : (flag2 ? 11 : 0)) : (flag ? 16 : (flag2 ? 17 : 15))) : (flag ? 13 : (flag2 ? 14 : 12)));
			if (num4 != 0)
			{
				m.Result = new IntPtr(num4);
				return;
			}
		}
		base.WndProc(ref m);
	}

	private static Color Blend(Color a, Color b, float t)
	{
		return Color.FromArgb((int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
	}

	[DllImport("user32.dll")]
	private static extern nint SendMessage(nint h, int msg, nint wp, nint lp);

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			saveTimer.Dispose();
			animation.Dispose();
			tips.Dispose();
			editMenu.Dispose();
			grain.Dispose();
		}
		base.Dispose(disposing);
		if (!disposing)
		{
			return;
		}
		foreach (Font value in fonts.Values)
		{
			value.Dispose();
		}
		tiny.Dispose();
		smallBold.Dispose();
	}
}
