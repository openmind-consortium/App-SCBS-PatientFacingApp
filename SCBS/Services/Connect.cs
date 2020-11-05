using Caliburn.Micro;
using Medtronic.SummitAPI.Classes;
using Medtronic.TelemetryM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCBS.Models;

namespace SCBS.Services
{
    /// <summary>
    /// Parent class for connect
    /// </summary>
    public abstract class Connect
    {
        private bool skipDiscovery = false;
        /// <summary>
        /// Need to override to put code in to connect to CTM
        /// </summary>
        /// <returns></returns>
        public abstract bool ConnectCTM(SummitManager theSummitManager, ref SummitSystem theSummit, SenseModel senseModel, AppModel appModel, ILog _log);

        /// <summary>
        /// Connects to the INS
        /// </summary>
        /// <param name="theSummit">SummitSystem</param>
        /// /// <param name="_log">Caliburn Micro logger</param>
        /// <returns>True if connected or false if not connected</returns>
        public bool ConnectINS(ref SummitSystem theSummit, ILog _log)
        {
            ConnectReturn theWarnings;
            APIReturnInfo connectReturn;
            //Check if you can skip discovery, if successful, return immediately, if not proceed to discover
            //If it has already discovered the INS before then we can skip discovery. 
            //If battery taken out or new connection then we need to discover
            if (skipDiscovery)
            {
                try
                {
                    int count = 5;
                    do
                    {
                        connectReturn = theSummit.StartInsSession(null, out theWarnings, true);
                        _log.Info("Skip Discovery: Start INS session reject code == " + connectReturn.RejectCode + "\r\nReject Code Type: " + connectReturn.RejectCodeType.ToString());
                        if (connectReturn.RejectCode == 0)
                        {
                            return true;
                        }
                        count--;
                    } while (connectReturn.RejectCodeType != typeof(APIRejectCodes) && connectReturn.RejectCode != 12 && count >= 0);
                    skipDiscovery = false;
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }
            // Discovery INS with the connected CTM, loop until a device has been discovered
            List<DiscoveredDevice> discoveredDevices;
            int countForDiscoveredDevices = 5;
            try
            {
                do
                {
                    Thread.Sleep(100);
                    theSummit.OlympusDiscovery(out discoveredDevices);
                    _log.Info("Discovery INS with the connected CTM, loop until a device has been discovered");
                    countForDiscoveredDevices--;
                    if (countForDiscoveredDevices <= 0)
                    {
                        _log.Warn("count for discovered device ran out");
                        return false;
                    }
                } while ((discoveredDevices == null || discoveredDevices.Count == 0));

                _log.Info("Olympi found: Creating Summit Interface.");
                // Connect to a device
                int countToAvoidInfiniteLoop = 5;
                do
                {
                    Thread.Sleep(2000); //Add short delay here for connection problems
                    connectReturn = theSummit.StartInsSession(discoveredDevices[0], out theWarnings, true);
                    _log.Info("Discovery: Start INS session reject code == " + connectReturn.RejectCode.ToString() + "\r\nReject Code Type: " + connectReturn.RejectCodeType.ToString());
                    if (connectReturn.RejectCodeType == typeof(InstrumentReturnCode) && (InstrumentReturnCode)connectReturn.RejectCode == InstrumentReturnCode.InvalidDiscoveredCount)
                    {
                        _log.Info("Start INS Session");
                        break;
                    }
                    countToAvoidInfiniteLoop--;
                    if (countToAvoidInfiniteLoop <= 0)
                    {
                        _log.Warn("count for ins connect ran out");
                        return false;
                    }
                } while (theWarnings.HasFlag(ConnectReturn.InitializationError));
                
                // Write out the final result of the example
                if (connectReturn.RejectCode != 0)
                {
                    _log.Warn("Summit Initialization: INS failed to connect");
                    return false;
                }
                else
                {
                    // Write out the warnings if they exist
                    _log.Info("Summit Initialization: INS connected, warnings: " + theWarnings.ToString());
                    skipDiscovery = true;
                    return true;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }
        }
    }
}
