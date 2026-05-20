using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GrabacionesAgente
{


	public class ObsManager
	{
		private readonly string _obsPath;

		public ObsManager(IConfiguration config)
		{
			_obsPath = config["Obs:Path"];
		}

		public bool IsRunning()
		{
			return Process.GetProcessesByName("obs64").Any();
		}

		public void Start()
		{
			if (IsRunning())
			{
				Console.WriteLine("OBS ya está corriendo");
				return;
			}

			Console.WriteLine("Iniciando OBS...");

			Process.Start(new ProcessStartInfo
			{
				FileName = _obsPath,
				WorkingDirectory = @"C:\Program Files\obs-studio\bin\64bit",
				Arguments = "--disable-shutdown-check --minimize-to-tray",
				UseShellExecute = false
			});
		}

		public async Task WaitForObsAsync(int timeoutSeconds = 15)
		{
			var start = DateTime.Now;

			while ((DateTime.Now - start).TotalSeconds < timeoutSeconds)
			{
				if (IsRunning())
				{
					Console.WriteLine("OBS detectado");
					return;
				}

				await Task.Delay(500);
			}

			throw new Exception("OBS no inició a tiempo");
		}








	}
}
