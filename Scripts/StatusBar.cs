using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElectroMods.Scripts
{
    internal class StatusBar
    {
        public static void Update(string text)
        {
            MainWindow.statusBar.Text = text;
        }
    }
}
