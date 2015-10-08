using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace LumiSoft.SIP.UA
{
    /// <summary>
    /// Application main class.
    /// </summary>
    static class Program
    {
        #region static method Main

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender,UnhandledExceptionEventArgs e){
                MessageBox.Show("Error: " + ((Exception)e.ExceptionObject).Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
            };
            Application.ThreadException += delegate(object sender,System.Threading.ThreadExceptionEventArgs e){
                MessageBox.Show("Error: " + e.Exception.Message,"Error:",MessageBoxButtons.OK,MessageBoxIcon.Error);
            };
        
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new wfrm_Main(false,"sip:john.doe@domain.com",new string[]{"sip:echo@iptel.org","sip:music@iptel.org","sip:4151595@services.sip.is"}));
        }

        #endregion
    }
}
