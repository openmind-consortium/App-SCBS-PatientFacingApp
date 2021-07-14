using Caliburn.Micro;
using Medtronic.SummitAPI.Classes;
using Medtronic.SummitAPI.Flash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SCBS.Services
{
    class INSDataInfo
    {
        private ILog _log;
        private string patientID;
        private string leadLocationOne = "";
        private string leadLocationTwo = "";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_log">Caliburn Micro Logger</param>
        /// <param name="theSummit">Summit System</param>
        public INSDataInfo(ILog _log, SummitSystem theSummit)
        {
            this._log = _log;
            GetSubjectInfo(theSummit);
        }
        /// <summary>
        /// Gets the patient ID from the API
        /// </summary>
        /// <returns>async Task</returns>
        public string GetPatientID()
        {
            return patientID;
        }
        /// <summary>
        /// Gets the lead location
        /// </summary>
        /// <returns>Returns that first lead location</returns>
        public string GetLeadLocationOne()
        {
            return leadLocationOne;
        }
        /// <summary>
        /// Gets the lead location
        /// </summary>
        /// <returns>Returns the second lead location</returns>
        public string GetLeadLocationTwo()
        {
            return leadLocationTwo;
        }

        /// <summary>
        /// Gets the Device ID from the API
        /// </summary>
        /// <returns>Device ID or null if couldn't find it</returns>
        public string GetDeviceID(SummitSystem theSummit)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                _log.Warn("Summit null or disposed when trying to get subject id");
            }
            _log.Info("Getting Device ID");
            string deviceID = null;
            int counter = 10;
            while (deviceID == null && counter > 0)
            {
                try
                {
                    deviceID = theSummit.DeviceID;
                    _log.Info("Device ID: " + deviceID);
                }
                catch (Exception e)
                {
                    //do nothing until I have device ID
                    _log.Error(e);
                }
                counter--;
            }
            _log.Info("Retrieved Device ID");
            return deviceID;
        }

        private void GetSubjectInfo(SummitSystem theSummit)
        {
            if(theSummit == null || theSummit.IsDisposed)
            {
                _log.Warn("Summit null or disposed when trying to get subject id");
                return;
            }
            //This loops until it gets the subjectInfo. Sometimes it comes back null so need to be sure I actually get it.
            _log.Info("Getting Subject info...");
            SubjectInfo subjectInfo = null;
            int counter = 10;
            while (subjectInfo == null && counter > 0)
            {
                try
                { 
                    APIReturnInfo bufferReturnInfo;
                    bufferReturnInfo = theSummit.FlashReadSubjectInfo(out subjectInfo);
                    patientID = subjectInfo.ID;
                    leadLocationOne = subjectInfo.LeadTargets[0].ToString();
                    leadLocationTwo = subjectInfo.LeadTargets[2].ToString();

                    _log.Info("Patient ID: " + patientID);
                    _log.Info("Lead Location 1: " + leadLocationOne);
                    _log.Info("Lead Location 2: " + leadLocationTwo);
                }
                catch (Exception e)
                {
                    //do nothing. Just keep looping until we actually get the patient id
                    _log.Error(e);
                }
                if(counter < 10)
                {
                    Thread.Sleep(300);
                }
                _log.Info("Attempt to get subject info: " + (10 - counter));
                counter--;                
            }
            if(subjectInfo == null && counter == 0)
            {
                _log.Warn("Could not get subject info from medtronic");
            }
        }
    }
}
