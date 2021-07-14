using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using Medtronic.SummitAPI.Classes;
using Medtronic.TelemetryM;
using Medtronic.TelemetryM.CtmProtocol.Commands;
using SCBS.Models;
using SCBS.ViewModels;

namespace SCBS.Services
{
    /// <summary>
    /// Child class of Connect. Adds CTM connection code for connecting to the Right CTM
    /// </summary>
    public class ConnectRight : Connect
    {
        private bool hasShownMessageToUser = true;
        /// <summary>
        /// Connects to CTM starting from the 0th value of the list of CTM's and works its way to the Nth.  This considered the right in bilateral
        /// </summary>
        /// <param name="theSummitManager">Summit manager</param>
        /// <param name="theSummit">Summit System</param>
        /// <param name="senseModel">Model for sense from config file</param>
        /// <param name="appModel">Model for application from config file</param>
        /// <param name="_log">Caliburn Micro Logger</param>
        /// <returns>true if connected or false if not connected</returns>
        public override bool ConnectCTM(SummitManager theSummitManager, ref SummitSystem theSummit, SenseModel senseModel, AppModel appModel, ILog _log)
        {
            _log.Info("Checking USB for unbonded CTMs. Please make sure they are powered on.");
            theSummitManager?.GetUsbTelemetry();

            // Retrieve a list of known and bonded telemetry
            List<InstrumentInfo> knownTelemetry = theSummitManager?.GetKnownTelemetry();

            // Check if any CTMs are currently bonded, poll the USB if not so that the user can be prompted to plug in a CTM over USB
            if (knownTelemetry?.Count == 0)
            {
                do
                {
                    // Inform user we will loop until a CTM is found on USBs
                    _log.Warn("No bonded CTMs found, please plug a CTM in via USB...");
                    Thread.Sleep(2000);

                    // Bond with any CTMs plugged in over USB
                    knownTelemetry = theSummitManager?.GetUsbTelemetry();
                } while (knownTelemetry?.Count == 0);
            }

            // Connect to the first CTM available, then try others if it fails
            SummitSystem tempSummit = null;
            try
            {
                for (int i = 0; i < theSummitManager.GetKnownTelemetry().Count; i++)
                {
                    ManagerConnectStatus connectReturn;
                    if(appModel?.CTMBeepEnables == null)
                    {
                        connectReturn = theSummitManager.CreateSummit(out tempSummit, theSummitManager.GetKnownTelemetry()[i], InstrumentPhysicalLayers.Any, senseModel.Mode, senseModel.Ratio, CtmBeepEnables.None);
                    }
                    else if (!appModel.CTMBeepEnables.DeviceDiscovered && !appModel.CTMBeepEnables.GeneralAlert && !appModel.CTMBeepEnables.NoDeviceDiscovered && !appModel.CTMBeepEnables.TelMCompleted && !appModel.CTMBeepEnables.TelMLost)
                    {
                        connectReturn = theSummitManager.CreateSummit(out tempSummit, theSummitManager.GetKnownTelemetry()[i], InstrumentPhysicalLayers.Any, senseModel.Mode, senseModel.Ratio, CtmBeepEnables.None);
                    }
                    else
                    {
                        connectReturn = theSummitManager.CreateSummit(out tempSummit, theSummitManager.GetKnownTelemetry()[i], InstrumentPhysicalLayers.Any, senseModel.Mode, senseModel.Ratio, ConfigConversions.BeepEnablesConvert(appModel));
                    }

                    // Write out the result
                    _log.Info("Create Summit Result: " + connectReturn.ToString());

                    // Break if it failed successful
                    if (tempSummit != null && connectReturn.HasFlag(ManagerConnectStatus.Success))
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            // Make sure telemetry was connected to, if not fail
            if (tempSummit == null)
            {
                // inform user that CTM was not successfully connected to
                if (hasShownMessageToUser)
                {
                    MainViewModel.ShowMessageBox("Could not connect to CTM. Check that CTM is placed correctly over INS on chest. If problem persists, please inform researcher of problem.", "Connection to CTM Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    hasShownMessageToUser = false;
                }
                return false;
            }
            else
            {
                theSummit = tempSummit;
                return true;
            }

        }
    }
}
