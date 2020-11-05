using Caliburn.Micro;
using SCBS.Services;
using SCBS.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AutoUpdaterDotNET;
using System.IO;

namespace SCBS
{
    public class Bootstrapper : BootstrapperBase
    {
        public Bootstrapper()
        {
            LogManager.GetLog = type => new Log4netLogger(type);
            Initialize();
        }

        /// <summary>
        /// Decides what happens on startup of program
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            //Get the file containing the url where the xml file is stored. 
            //Check xml file to see if the version has increased.  If so, download update and update application.
            string urlForAutoUpdateContainingXML = null;
            try
            {
                using (StreamReader fileContainingAutoUpdateURL = new StreamReader(@"C:\SCBS\url.txt"))
                {
                    urlForAutoUpdateContainingXML = fileContainingAutoUpdateURL.ReadToEnd();
                }
            }
            catch
            {
            }
            //make sure url is not null, not empty and in correct format. If it isn't, then skip the auto-update code and log.
            //otherwise start update download
            if (!string.IsNullOrEmpty(urlForAutoUpdateContainingXML))
            {
                if (Uri.IsWellFormedUriString(urlForAutoUpdateContainingXML, UriKind.Absolute))
                {
                    AutoUpdater.Mandatory = true;
                    AutoUpdater.UpdateMode = Mode.Forced;
                    try
                    {
                        AutoUpdater.Start(urlForAutoUpdateContainingXML);
                    }
                    catch
                    {
                    }
                }
            }

            DisplayRootViewFor<MainViewModel>();
        }

        /// <summary>
        /// Decides what happens on window closing of program
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnExit(object sender, EventArgs e)
        {

        }
    }
}
