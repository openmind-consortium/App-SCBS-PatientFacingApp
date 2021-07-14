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
        /// Gets the Stim parameters for a Group
        /// </summary>
        /// <param name="theSummit">SummitSystem for making the api call to INS</param>
        /// <param name="group">Group number to get the data</param>
        /// <returns>StimParameterModel that contains stim amp, stim rate and pulse width</returns>
        public Tuple<bool, string, TherapyGroup> GetTherapyDataForGroup(SummitSystem theSummit, GroupNumber group)
        {
            TherapyGroup insStateGroup = null;
            if (theSummit == null || theSummit.IsDisposed)
            {
                return Tuple.Create(false, "Summit Disposed", insStateGroup);
            }
            
            try
            {
                //Get the data from the api
                bufferInfo = theSummit.ReadStimGroup(group, out insStateGroup);
                if (bufferInfo.RejectCode != 0 || insStateGroup == null)
                {
                    _log.Warn("Could not read stim group from Medtronic api call");
                    return Tuple.Create(false, "Could not read stim group from Medtronic api call", insStateGroup);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, "Error reading stim group.", insStateGroup);
            }
            return Tuple.Create(true, "Success", insStateGroup);
        }
        #endregion

        #region Helper Functions for Converting Group Format and Getting Stim Parameters from API
        /// <summary>
        /// This maybe should go into a different class like Stimulation.cs, but it's here for now. 
        ///It gets the group stim params based on the group that was read from the device.
        ///if Group b was read from the device, then it gets the params for that specific group.
        /// </summary>
        /// <param name="theSummit">Summit System</param>
        /// <param name="group">Active Group after being converted: Group A, Group B, Group C, Group D</param>
        /// <param name="program">program 0-3</param>
        /// <returns>StimParameterModel filled with data</returns>
        public StimParameterModel GetStimParamsBasedOnGroup(SummitSystem theSummit, string group, int program)
        {
            StimParameterModel stimParam = new StimParameterModel("Error", "Error", "Error", "Error", null);

            if (string.IsNullOrEmpty(group))
            {
                return stimParam;
            }
            switch (group)
            {
                case "Group A":
                    stimParam = GetStimParameterModel(theSummit, GroupNumber.Group0, program);
                    break;
                case "Group B":
                    stimParam = GetStimParameterModel(theSummit, GroupNumber.Group1, program);
                    break;
                case "Group C":
                    stimParam = GetStimParameterModel(theSummit, GroupNumber.Group2, program);
                    break;
                case "Group D":
                    stimParam = GetStimParameterModel(theSummit, GroupNumber.Group3, program);
                    break;
                default:
                    break;
            }
            return stimParam;
        }
        /// <summary>
        /// Gets the stim parameters based on group from the actual API
        /// </summary>
        /// <param name="theSummit">SummitSystem for making the API call to INS</param>
        /// <param name="groupNumber">Group number corresponding to which group we want to get stim parameters from such as Group0, Group1, etc</param>
        /// <returns>StimParameterModel that contains stim amp, stim rate and pulse width</returns>
        private StimParameterModel GetStimParameterModel(SummitSystem theSummit, GroupNumber groupNumber, int program)
        {
            if (theSummit == null || theSummit.IsDisposed)
            {
                return null;
            }
            TherapyGroup insStateGroup = null;
            AmplitudeLimits ampLimits = null;
            try
            {
                int counter = 5;
                do
                {
                    bufferInfo = theSummit.ReadStimGroup(groupNumber, out insStateGroup);
                    counter--;
                } while ((insStateGroup == null || bufferInfo.RejectCode != 0) && counter > 0);
                if (bufferInfo.RejectCode != 0 && counter == 0)
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
                int counter = 5;
                do
                {
                    bufferInfo = theSummit.ReadStimAmplitudeLimits(groupNumber, out ampLimits);
                    counter--;
                } while ((insStateGroup == null || bufferInfo.RejectCode != 0) && counter > 0);
                if (bufferInfo.RejectCode != 0 && counter == 0)
                {
                    _log.Warn("Could not read amp limits from Medtronic api call");
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
                    pulseWidth = insStateGroup.Programs[program].PulseWidthInMicroseconds.ToString();
                    stimRate = insStateGroup.RateInHz.ToString();
                    stimAmp = insStateGroup.Programs[program].AmplitudeInMilliamps.ToString();
                    stimElectrode = FindStimElectrodes(insStateGroup, program);
                    electrodes = insStateGroup?.Programs[program]?.Electrodes;
                }   
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            //Set the Model with these values and return model
            StimParameterModel StimParameterModel = new StimParameterModel(pulseWidth, stimRate, stimAmp, stimElectrode, electrodes, insStateGroup, ampLimits);
            return StimParameterModel;
        }

        /// <summary>
        /// Finds the electrodes that are stimming
        /// </summary>
        /// <param name="therapyGroup">therapy group from the ins call</param>
        /// <param name="program">program number 0-3</param>
        /// <returns>Returns the contacts that are stimming along with anode or cathode</returns>
        public string FindStimElectrodes(TherapyGroup therapyGroup, int program)
        {
            string electrodesStimming = "";
            if (therapyGroup.Valid)
            {
                if (therapyGroup.Programs[program].Valid)
                {
                    for (int i = 0; i < 17; i++)
                    {
                        if (!therapyGroup.Programs[program].Electrodes[i].IsOff)
                        {
                            //Case is 16 so it gets a C. Otherwise give the number
                            if (i == 16)
                            {
                                electrodesStimming += "C";
                            }
                            else
                            {
                                electrodesStimming += i.ToString();
                            }
                            // What type of electrode is it?
                            switch (therapyGroup.Programs[program].Electrodes[i].ElectrodeType)
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
    }
}
