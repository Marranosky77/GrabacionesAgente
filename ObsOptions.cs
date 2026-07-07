using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrabacionesAgente
{
	public class ObsOptions
	{
		public string Path { get; set; }
		public int Port { get; set; }
		public string Password { get; set; }
	}

    public class MonitorInfo
    {
        public string DeviceName { get; set; }

        public bool Primary { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }
}
