using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Moss.Windows;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		bool createdNew;
		using Mutex mutex = new Mutex(initiallyOwned: true, "Local\\Moss.Desktop.SingleCreature", out createdNew);
		if (!createdNew)
		{
			if (Environment.GetCommandLineArgs().Contains("--reminder"))
			{
				return;
			}
			try
			{
				if (EventWaitHandle.TryOpenExisting("Local\\Moss.Desktop.Show", out EventWaitHandle result))
				{
					using (result)
					{
						result.Set();
						return;
					}
				}
			}
			catch (Exception error)
			{
				Log.Error("instance-activation", error);
			}
			MessageBox.Show("An older Moss instance is still running. Right-click its tray icon → Exit Moss, then launch this updated Moss.exe.", "Moss update");
			return;
		}
		using EventWaitHandle signal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\Moss.Desktop.Show");
		Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
		Application.ThreadException += delegate(object _, ThreadExceptionEventArgs e)
		{
			Log.Error("ui", e.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject is Exception error3)
			{
				Log.Error("fatal", error3);
			}
		};
		try
		{
			using PetApplication context = new PetApplication(signal);
			Application.Run(context);
		}
		catch (Exception error2)
		{
			Log.Error("startup", error2);
			MessageBox.Show("Moss couldn't start. Extract the complete archive into a writable local folder and try again. Local logs are in %LOCALAPPDATA%\\Moss.", "Moss", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		finally
		{
			mutex.ReleaseMutex();
		}
	}
}
