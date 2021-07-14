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
using SciChart.Charting.Visuals;

namespace SCBS
{
    public class Bootstrapper : BootstrapperBase
    {
        private string sciChartLicenseFileLocation = @"C:\SCBS\sciChartLicense.txt";
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
            //Get the scichart license from file
            string sciChartLicense = null;
            try
            {
                using (System.IO.StreamReader sr = new StreamReader(sciChartLicenseFileLocation))
                {
                    sciChartLicense = sr.ReadToEnd();
                }
                SciChartSurface.SetRuntimeLicenseKey(sciChartLicense);
            }
            catch
            {
                //MessageBox.Show(@"Error Importing SciChart License. Charts will not work without it. Please be sure it is located in the directory C:\SCBS\sciChartLicense.txt. Proceed if you don't need to use the charts.", "Warning", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
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
            MainViewModel window = new MainViewModel();
            window.ExitButtonClick();
        }
    }
}
