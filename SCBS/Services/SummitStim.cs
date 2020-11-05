using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using Medtronic.SummitAPI.Classes;
using SCBS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SCBS.Services
{
    class SummitStim
    {
        private ILog _log;
        private SummitSensing summitSensing;
        public SummitStim(ILog _log)
        {
            this._log = _log;
            summitSensing = new SummitSensing(_log);
        }
        /// <summary>
        /// Changes the active group to the group specified.
        /// </summary>
        /// <param name="localSummit">Summit System</param>
        /// <param name="groupToChangeTo">Medtronic Active Group Format to change to</param>
        /// <param name="localSenseModel">Sense Model that uses the streaming parameters for turning stream on and off</param>
        /// <returns>Tuple with bool true if success or false if not. string give error message</returns>
        public Tuple<bool, string> ChangeActiveGroup(SummitSystem localSummit, ActiveGroup groupToChangeTo, SenseModel localSenseModel)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, "Error: Summit null or disposed.");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                int counter = 5;
                summitSensing.StopStreaming(localSummit, true);
                do
                {
                    bufferReturnInfo = localSummit.StimChangeActiveGroup(groupToChangeTo);
                    if (counter < 5)
                    {
                        Thread.Sleep(400);
                    }
                    counter--;
                } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                {
                    _log.Warn(":: Error: Medtronic API return error changing active group: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                    return Tuple.Create(false, "Error: Medtronic API return error changing active group: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                }
                //Start streaming
                summitSensing.StartStreaming(localSummit, localSenseModel, true);
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, "Error: Could not change groups");
            }
            return Tuple.Create(true, "Successfully changed groups");
        }

    }
}
