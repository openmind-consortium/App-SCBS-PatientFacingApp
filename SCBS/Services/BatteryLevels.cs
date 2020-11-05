using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.PowerManagement;
using Medtronic.SummitAPI.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Services
{
    class BatteryLevels
    {
        private string INSBatteryLevel = "Not Connected";
        private string CTMBatteryLevel = "Not Connected";

        /// <summary>
        /// Gets the battery level for the INS
        /// </summary>
        /// <param name="theSummit">Summit System</param>
        /// <param name="_log">Caliburn Micro Logger</param>
        /// <returns>String that tells the battery status of the INS</returns>
        public string GetINSBatteryLevel(ref SummitSystem theSummit, ILog _log)
        {
            BatteryStatusResult outputBuffer = null;
            APIReturnInfo commandInfo = new APIReturnInfo();

            //Return Not Connected if summit is null
            if (theSummit == null)
                return INSBatteryLevel;

            try
            {
                commandInfo = theSummit.ReadBatteryLevel(out outputBuffer);
            }
            catch(Exception e)
            {
                _log.Error(e);
            }

            // Ensure the command was successful before using the result
            try
            {
                //Check if command was successful
                if (commandInfo.RejectCode == 0)
                {
                    // Retrieve the battery level from the output buffer
                    if (outputBuffer != null)
                        INSBatteryLevel = outputBuffer.BatteryLevelPercent.ToString();
                }
                else
                {
                    INSBatteryLevel = "";
                }
            }
            catch (Exception e)
            {
                INSBatteryLevel = "";
                _log.Error(e);
            }
            //Return either battery level, empty string or Not Connected
            return INSBatteryLevel;
        }

        /// <summary>
        /// Gets the battery level for the CTM
        /// </summary>
        /// <param name="theSummit">Summit System</param>
        /// <param name="_log">Caliburn Micro logger</param>
        /// <returns>String that tells the battery status of the CTM</returns>
        public string GetCTMBatteryLevel(ref SummitSystem theSummit, ILog _log)
        {
            TelemetryModuleInfo telem_info = null;
            APIReturnInfo ctm_return_info = new APIReturnInfo();

            if (theSummit == null)
                return CTMBatteryLevel;

            try
            {
                //Get info from summit
                ctm_return_info = theSummit.ReadTelemetryModuleInfo(out telem_info);
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            try
            {
                //Make sure reject code was successful and write string to CTMBatteryLevel string
                if (ctm_return_info.RejectCode == 0)
                {
                    if (telem_info != null)
                        CTMBatteryLevel = Convert.ToString(telem_info.BatteryLevel);
                }
                else
                {
                    CTMBatteryLevel = "";
                }
            }
            catch (Exception e)
            {
                CTMBatteryLevel = "";
                _log.Error(e);
            }
            return CTMBatteryLevel;
        }
    }
}
