using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using Medtronic.NeuroStim.Olympus.DataTypes.Therapy;
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
    /// <summary>
    /// Class that handles getting the simulation data from the INS
    /// This data is used to report status to user in display
    /// </summary>
    class SummitStimulationInfo
    {
        #region Variables
        private ILog _log;
        //APIReturnInfo for checking return values from api
        APIReturnInfo bufferInfo = new APIReturnInfo();
        //stim status variables set to empty in case could not get data
        //This will just set it with empty string instead of null
        private string activeGroup = "";
        private string pulseWidth = "";
        private string stimAmp = "";
        private string stimState = "";
        private string stimRate = "";
        private string stimElectrode = "";
        TherapyElectrodes electrodes = null;
        #endregion

        public SummitStimulationInfo(ILog _log)
        {
            this._log = _log;
        }

        #region Get Active Group and Stim Therapy Status from API
        /// <summary>
        /// Gets the Active group from the api
        /// </summary>
        /// <param name="theSummit">SummitSystem to make api calls to INS</param>
        /// <returns>The active group in the format Group A instead of the format returned from medtonic such as Group0</returns>
        public string GetActiveGroup(ref SummitSystem theSummit)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                return "";
            }
            GeneralInterrogateData insGeneralInfo = null;
            try
            {
                //Get the group from the api call
                bufferInfo = theSummit.ReadGeneralInfo(out insGeneralInfo);
                if (insGeneralInfo != null)
                    activeGroup = insGeneralInfo.TherapyStatusData.ActiveGroup.ToString();
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            //This returns the converted group from something like Group0 to Group A
            return ConvertActiveGroupToReadableForm(activeGroup);
        }

        /// <summary>
        /// Gets the Stim Therapy Status from the API
        /// </summary>
        /// <param name="theSummit">SummitSystem for making the call to the API to the INS</param>
        /// <returns>String showing Therapy Active or Therapy Inactive</returns>
        public string GetTherapyStatus(ref SummitSystem theSummit)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                return "";
            }
            GeneralInterrogateData insGeneralInfo = null;
            try
            {
                //Add a sleep in there to allow status of therapy to get pass Therapy Transitioning state
                //Allows it to either be Active or InActive and not inbetween
                Thread.Sleep(200);
                //Get data from api
                bufferInfo = theSummit.ReadGeneralInfo(out insGeneralInfo);
                //parse insGeneralInfo to get stim therapy status
                if (insGeneralInfo != null)
                    stimState = insGeneralInfo.TherapyStatusData.TherapyStatus.ToString();
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            return stimState;
        }
        #endregion

        #region Gets Stim parameters based on group. This is called from outside the class
        /// <summary>
        /// Gets the Stim parameters for Group A
        /// </summary>
        /// <param name="theSummit">SummitSystem for making the api call to INS</param>
        /// <returns>StimParameterModel that contains stim amp, stim rate and pulse width</returns>
        public StimParameterModel GetStimParameterModelGroupA(ref SummitSystem theSummit)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                return null;
            }
            // Read the stimulation settings from the device
            StimParameterModel StimParameterModel = getStimParameterModel(ref theSummit, GroupNumber.Group0);
            _log.Info("STIM PARAMS GROUP A: pulse width = " + StimParameterModel.PulseWidth + ", stim rate = " + StimParameterModel.StimRate + ", stim amp = " + StimParameterModel.StimAmp);
            return StimParameterModel;
        }

        /// <summary>
        /// Gets the Stim parameters for Group B
        /// </summary>
        /// <param name="theSummit">SummitSystem for making the api call to INS</param>
        /// <returns>StimParameterModel that contains stim amp, stim rate and pulse width</returns>
        public StimParameterModel GetStimParameterModelGroupB(ref SummitSystem theSummit)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                return null;
            }
            // Read the stimulation settings from the device
            StimParameterModel StimParameterModel = getStimParameterModel(ref theSummit, GroupNumber.Group1);
            _log.Info("STIM PARAMS GROUP B: pulse width = " + StimParameterModel.PulseWidth + ", stim rate = " + StimParameterModel.StimRate + ", stim amp = " + StimParameterModel.StimAmp);
            return StimParameterModel;
        }

        /// <summary>
        /// Gets the Stim parameters for Group C
        /// </summary>
        /// <param name="theSummit">SummitSystem for making the api call to INS</param>
        /// <returns>StimParameterModel that contains stim amp, stim rate and pulse width</returns>
        public StimParameterModel GetStimParameterModelGroupC(ref SummitSystem theSummit)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                return null;
            }
            // Read the stimulation settings from the device
            StimParameterModel StimParameterModel = getStimParameterModel(ref theSummit, GroupNumber.Group2);
            _log.Info("STIM PARAMS GROUP C: pulse width = " + StimParameterModel.PulseWidth + ", stim rate = " + StimParameterModel.StimRate + ", stim amp = " + StimParameterModel.StimAmp);
            return StimParameterModel;
        }

        /// <summary>
        /// Gets the Stim parameters for Group D
        /// </summary>
        /// <param name="theSummit">SummitSystem for making the api call to INS</param>
        /// <returns>StimParameterModel that contains stim amp, stim rate and pulse width</returns>
        public StimParameterModel GetStimParameterModelGroupD(ref SummitSystem theSummit)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                return null;
            }
            // Read the stimulation settings from the device
            StimParameterModel StimParameterModel = getStimParameterModel(ref theSummit, GroupNumber.Group3);
            _log.Info("STIM PARAMS GROUP D: pulse width = " + StimParameterModel.PulseWidth + ", stim rate = " + StimParameterModel.StimRate + ", stim amp = " + StimParameterModel.StimAmp);
            return StimParameterModel;
        }
        #endregion

        #region Helper Functions for Converting Group Format and Getting Stim Parameters from API
        /// <summary>
        /// Gets the stim parameters based on group from the actual API
        /// </summary>
        /// <param name="theSummit">SummitSystem for making the API call to INS</param>
        /// <param name="groupNumber">Group number corresponding to which group we want to get stim parameters from such as Group0, Group1, etc</param>
        /// <returns>StimParameterModel that contains stim amp, stim rate and pulse width</returns>
        private StimParameterModel getStimParameterModel(ref SummitSystem theSummit, GroupNumber groupNumber)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                return null;
            }
            TherapyGroup insStateGroup = null;
            try
            {
                //Get the data from the api
                bufferInfo = theSummit.ReadStimGroup(groupNumber, out insStateGroup);
                if(bufferInfo.RejectCode != 0)
                {
                    _log.Warn("Could not read stim group from Medtronic api call");
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            try
            {
                //parse the data to get the pulsewidth
                if (insStateGroup != null)
                {
                    pulseWidth = insStateGroup.Programs[0].PulseWidthInMicroseconds.ToString() + " μS";
                    stimRate = insStateGroup.RateInHz.ToString() + " Hz";
                    stimAmp = insStateGroup.Programs[0].AmplitudeInMilliamps.ToString() + " mA";
                    stimElectrode = FindStimElectrodes(insStateGroup);
                    electrodes = insStateGroup?.Programs[0]?.Electrodes;
                }   
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            //Set the Model with these values and return model
            StimParameterModel StimParameterModel = new StimParameterModel(pulseWidth, stimRate, stimAmp, stimElectrode, electrodes);
            return StimParameterModel;
        }

        /// <summary>
        /// Finds the electrodes that are stimming
        /// </summary>
        /// <param name="therapyGroup">therapy group from the ins call</param>
        /// <returns>Returns the contacts that are stimming along with anode or cathode</returns>
        private string FindStimElectrodes(TherapyGroup therapyGroup)
        {
            string electrodesStimming = "";
            if (therapyGroup.Valid)
            {
                if (therapyGroup.Programs[0].Valid)
                {
                    for (int i = 0; i < 17; i++)
                    {
                        if (!therapyGroup.Programs[0].Electrodes[i].IsOff)
                        {
                            electrodesStimming += i.ToString();
                            // What type of electrode is it?
                            switch (therapyGroup.Programs[0].Electrodes[i].ElectrodeType)
                            {
                                case ElectrodeTypes.Cathode:
                                    electrodesStimming += "-";
                                    break;
                                case ElectrodeTypes.Anode:
                                    electrodesStimming += "+";
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            return electrodesStimming;
        }

        /// <summary>
        /// This methods converts the groups that are read from the device and converts them to a readable form for displaying to user
        /// </summary>
        /// <param name="group">Medtronic API call format such as Group0, Group1, Group2 or Group3</param>
        /// <returns>Group A, Group B, Group C or Group D</returns>
        private string ConvertActiveGroupToReadableForm(string group)
        {
            string tempGroup = "";
            if (string.IsNullOrEmpty(group))
            {
                return tempGroup;
            }
            switch (group)
            {
                case "Group0":
                    tempGroup = "Group A";
                    break;
                case "Group1":
                    tempGroup = "Group B";
                    break;
                case "Group2":
                    tempGroup = "Group C";
                    break;
                case "Group3":
                    tempGroup = "Group D";
                    break;
                default:
                    tempGroup = "";
                    break;
            }
            return tempGroup;
        }
        #endregion

        /// <summary>
        /// This maybe should go into a different class like Stimulation.cs, but it's here for now. 
        ///It gets the group stim params based on the group that was read from the device.
        ///if Group b was read from the device, then it gets the params for that specific group.
        /// </summary>
        /// <param name="theSummit">Summit System</param>
        /// <param name="group">Active Group after being converted: Group A, Group B, Group C, Group D</param>
        /// <returns>StimParameterModel filled with data</returns>
        public StimParameterModel GetStimParamsBasedOnGroup(SummitSystem theSummit, string group)
        {
            StimParameterModel stimParam = new StimParameterModel("", "", "", "", null);
            if (string.IsNullOrEmpty(group))
            {
                return stimParam;
            }
            switch (group)
            {
                case "Group A":
                    stimParam = GetStimParameterModelGroupA(ref theSummit);
                    break;
                case "Group B":
                    stimParam = GetStimParameterModelGroupB(ref theSummit);
                    break;
                case "Group C":
                    stimParam = GetStimParameterModelGroupC(ref theSummit);
                    break;
                case "Group D":
                    stimParam = GetStimParameterModelGroupD(ref theSummit);
                    break;
                default:
                    break;
            }
            return stimParam;
        }
    }
}
