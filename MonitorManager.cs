using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GrabacionesAgente
{
   
    public class MonitorManager
    {
        public List<MonitorInfo> GetMonitors()
        {
            var result = new List<MonitorInfo>();

            foreach (var screen in Screen.AllScreens)
            {
                result.Add(new MonitorInfo
                {
                    DeviceName = screen.DeviceName,
                    Primary = screen.Primary,
                    X = screen.Bounds.X,
                    Y = screen.Bounds.Y,
                    Width = screen.Bounds.Width,
                    Height = screen.Bounds.Height
                });
            }

            return result;
        }
    }
}
