using Group_P3_LPR_381_Project;
using System;
using System.Windows.Forms;

namespace LinearProgrammingSolver
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
