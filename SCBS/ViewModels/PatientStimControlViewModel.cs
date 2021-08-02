using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using Medtronic.SummitAPI.Classes;
using SCBS.Models;
using SCBS.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace SCBS.ViewModels
{
    class PatientStimControlViewModel : Caliburn.Micro.Screen
    {
        #region Constant Vars
        private const string STIM_CONTROL_CONFIG_FILE_LOCATION_LEFT = @"C:\SCBS\Patient_Stim\unilateral_left_patient_stim_config.json";
        private const string STIM_CONTROL_CONFIG_FILE_LOCATION_RIGHT = @"C:\SCBS\Patient_Stim\right_patient_stim_config.json";
        private const int MAX_LENGTH_CUSTOM_TEXT = 15;
        private const string NONE_CARD_STIM_VALUE_TYPE = "X";
        private const string AMP_UNITS = "mA";
        private const string RATE_UNITS = "Hz";
        private const string PULSEWIDTH_UNITS = "μS";
        private const string NONE_UNITS = "N/A";
        #endregion
        private ILog _log;
        private SummitSystem theSummitSystem;
        private SenseModel senseModel;
        private PatientStimControlModel patientStimControlModel;
        //Used to store the cards. Only active cards are stored in this list
        public ObservableCollection<Card> ListOfCards { get; set; }
        //Used as count of number of active cards from config file
        private int numberOfActiveCards = 0;
        //Used to store each cards stim settings, based on 0-n cards where n is the number of active cards
        private Hashtable cardStimSettings = new Hashtable();
        //Vars for stim update
        private double currentUpdatedSelectedAmp, currentNotUpdatedSelectedAmp;
        private double currentUpdatedSelectedRate, currentNotUpdatedSelectedRate;
        private int currentUpdatedSelectedPW, currentNotUpdatedSelectedPW;

        #region UI Binding vars
        private Visibility _stimOnButtonVisibilty, _stimOffButtonVisibilty;
        private bool _isStimOnButtonEnabled, _isStimOffButtonEnabled, _isUpdateCardButtonEnabled, _isUpdateStimButtonEnabled;
        private int _selectedCardIndex = -1;
        private int activeCardIndex = -1;
        private Card _selectedCardItem, activeCard;
        //Progress Bar
        private Visibility _progressVisibility = Visibility.Collapsed;
        private int _currentProgress = 0;
        private string _progressText = "";
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="theSummitSystem">Summit System</param>
        /// <param name="isLeft">Is left side is true or right side is false. Used for getting correct config file</param>
        /// <param name="senseModel">sense Model config file</param>
        /// <param name="_log">Log</param>
        public PatientStimControlViewModel(SummitSystem theSummitSystem, bool isLeft, SenseModel senseModel, ILog _log)
        {
            if (theSummitSystem == null || theSummitSystem.IsDisposed)
            {
                return;
            }
            this.theSummitSystem = theSummitSystem;
            this.senseModel = senseModel;
            this._log = _log;
            var jSONService = new JSONService(_log);
            var fileLocation = STIM_CONTROL_CONFIG_FILE_LOCATION_LEFT;
            if (!isLeft)
            {
                fileLocation = STIM_CONTROL_CONFIG_FILE_LOCATION_RIGHT;
            }
            patientStimControlModel = jSONService?.GetPatientStimControlModelFromFile(fileLocation);
            //patientStimControlModel config is required. If user is missing it (hence null) then they can't move on
            if (patientStimControlModel == null)
            {
                return;
            }

            #region Summit API Calls
            SummitStimulationInfo summitStimulationInfo = new SummitStimulationInfo(_log);
            //Get from device if therapy is active or off and enable correct stim on/off buttons
            string therapyStatus = summitStimulationInfo.GetTherapyStatus(ref theSummitSystem);
            if (therapyStatus.Equals("TherapyActive"))
            {
                IsStimOnButtonEnabled = false;
                IsStimOffButtonEnabled = true;
            }
            else
            {
                IsStimOnButtonEnabled = true;
                IsStimOffButtonEnabled = false;
            }
            //Get stim settings for each card and put in set. This removes duplicates
            HashSet<Tuple<string, int>> groups = new HashSet<Tuple<string, int>>();
            if (!patientStimControlModel.Card1.HideCard)
            {
                groups.Add(new Tuple<string, int>("Group " + patientStimControlModel.Card1.Group, patientStimControlModel.Card1.StimControl.Program));
            }
            if (!patientStimControlModel.Card2.HideCard)
            {
                groups.Add(new Tuple<string, int>("Group " + patientStimControlModel.Card2.Group, patientStimControlModel.Card2.StimControl.Program));
            }
            if (!patientStimControlModel.Card3.HideCard)
            {
                groups.Add(new Tuple<string, int>("Group " + patientStimControlModel.Card3.Group, patientStimControlModel.Card3.StimControl.Program));
            }
            if (!patientStimControlModel.Card4.HideCard)
            {
                groups.Add(new Tuple<string, int>("Group " + patientStimControlModel.Card4.Group, patientStimControlModel.Card4.StimControl.Program));
            }
            //Add the group data to hashtable to get current stim info for each group configuration
            //Group is tuple with group and program, so (Group A, 1) for group A and program 1 settings
            //This makes it so we don't have to get settings for all groups and programs, just the ones we need
            Hashtable groupData = new Hashtable();
            foreach (Tuple<string, int> group in groups)
            {
                //TODO: Set to async task and await for data. Currently holds UI thread
                groupData.Add(group, summitStimulationInfo.GetStimParamsBasedOnGroup(theSummitSystem, group.Item1, group.Item2));
                CurrentProgress += 20;
            }
            #endregion

            #region Configure UI Settings
            //Stim on/off buttons
            _stimOnButtonVisibilty = patientStimControlModel.HideStimOnButton ? Visibility.Collapsed : Visibility.Visible;
            _stimOffButtonVisibilty = patientStimControlModel.HideStimOffButton ? Visibility.Collapsed : Visibility.Visible;

            ListOfCards = new ObservableCollection<Card>();

            #region Activate Cards and Card UI Settings
            /*
            Get each cards info that are active.No need to get inactive cards data.
            Add stim data to has cardStimSettings hashtable with index of card as key
            Set UI settings for each card based on config file
            */
            //Card 1
            if (!patientStimControlModel.Card1.HideCard)
            {
                StimParameterModel stim = (StimParameterModel)groupData[new Tuple<string, int>("Group " + patientStimControlModel.Card1.Group, patientStimControlModel.Card1.StimControl.Program)];
                cardStimSettings.Add(numberOfActiveCards, stim);
                numberOfActiveCards++;

                ListOfCards.Add(new Card(patientStimControlModel.Card1.DisplaySettings.HideGroupDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card1.DisplaySettings.HideSiteDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card1.DisplaySettings.HideAmpDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card1.DisplaySettings.HideRateDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card1.DisplaySettings.HidePulseWidthDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card1.StimControl.HideCurrentValue ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card1.StimControl.HideCurrentValueUnits ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card1.CustomText.LimitLength(MAX_LENGTH_CUSTOM_TEXT),
                    "Group " + patientStimControlModel.Card1.Group,
                    stim.StimElectrodes,
                    patientStimControlModel.Card1.StimControl.Amp.TargetAmp.ToString() + AMP_UNITS,
                    patientStimControlModel.Card1.StimControl.Rate.TargetRate.ToString() + RATE_UNITS,
                    patientStimControlModel.Card1.StimControl.PulseWidth.TargetPulseWidth.ToString() + PULSEWIDTH_UNITS,
                    GetTargetStimAndUnits(patientStimControlModel.Card1.StimControl.StimControlType, 0).Item1,
                    GetTargetStimAndUnits(patientStimControlModel.Card1.StimControl.StimControlType, 0).Item2,
                    false,
                    false,
                    patientStimControlModel.Card1.StimControl.Amp.TargetAmp,
                    patientStimControlModel.Card1.StimControl.Rate.TargetRate,
                    patientStimControlModel.Card1.StimControl.PulseWidth.TargetPulseWidth,
                    patientStimControlModel.Card1.StimControl.PulseWidth.PulseWidthValues,
                    patientStimControlModel.Card1.StimControl.Rate.RateValues,
                    patientStimControlModel.Card1.StimControl.Amp.AmpValues,
                    patientStimControlModel.Card1.StimControl.Rate.SenseFriendly,
                    (int)patientStimControlModel.Card1.StimControl.StimControlType,
                    patientStimControlModel.Card1.StimControl.Program
                    ));
            }

            //Card 2
            if (!patientStimControlModel.Card2.HideCard)
            {
                StimParameterModel stim = (StimParameterModel)groupData[new Tuple<string, int>("Group " + patientStimControlModel.Card2.Group, patientStimControlModel.Card2.StimControl.Program)];
                cardStimSettings.Add(numberOfActiveCards, stim);
                numberOfActiveCards++;

                ListOfCards.Add(new Card(patientStimControlModel.Card2.DisplaySettings.HideGroupDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card2.DisplaySettings.HideSiteDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card2.DisplaySettings.HideAmpDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card2.DisplaySettings.HideRateDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card2.DisplaySettings.HidePulseWidthDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card2.StimControl.HideCurrentValue ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card2.StimControl.HideCurrentValueUnits ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card2.CustomText.LimitLength(MAX_LENGTH_CUSTOM_TEXT),
                    "Group " + patientStimControlModel.Card2.Group,
                    stim.StimElectrodes,
                    patientStimControlModel.Card2.StimControl.Amp.TargetAmp.ToString() + AMP_UNITS,
                    patientStimControlModel.Card2.StimControl.Rate.TargetRate.ToString() + RATE_UNITS,
                    patientStimControlModel.Card2.StimControl.PulseWidth.TargetPulseWidth.ToString() + PULSEWIDTH_UNITS,
                    GetTargetStimAndUnits(patientStimControlModel.Card2.StimControl.StimControlType, 1).Item1,
                    GetTargetStimAndUnits(patientStimControlModel.Card2.StimControl.StimControlType, 1).Item2,
                    false,
                    false,
                    patientStimControlModel.Card2.StimControl.Amp.TargetAmp,
                    patientStimControlModel.Card2.StimControl.Rate.TargetRate,
                    patientStimControlModel.Card2.StimControl.PulseWidth.TargetPulseWidth,
                    patientStimControlModel.Card2.StimControl.PulseWidth.PulseWidthValues,
                    patientStimControlModel.Card2.StimControl.Rate.RateValues,
                    patientStimControlModel.Card2.StimControl.Amp.AmpValues,
                    patientStimControlModel.Card2.StimControl.Rate.SenseFriendly,
                    (int)patientStimControlModel.Card2.StimControl.StimControlType,
                    patientStimControlModel.Card2.StimControl.Program
                    ));
            }

            //Card 3
            if (!patientStimControlModel.Card3.HideCard)
            {
                StimParameterModel stim = (StimParameterModel)groupData[new Tuple<string, int>("Group " + patientStimControlModel.Card3.Group, patientStimControlModel.Card3.StimControl.Program)];
                cardStimSettings.Add(numberOfActiveCards, stim);
                numberOfActiveCards++;

                ListOfCards.Add(new Card(patientStimControlModel.Card3.DisplaySettings.HideGroupDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card3.DisplaySettings.HideSiteDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card3.DisplaySettings.HideAmpDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card3.DisplaySettings.HideRateDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card3.DisplaySettings.HidePulseWidthDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card3.StimControl.HideCurrentValue ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card3.StimControl.HideCurrentValueUnits ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card3.CustomText.LimitLength(MAX_LENGTH_CUSTOM_TEXT),
                    "Group " + patientStimControlModel.Card3.Group,
                    stim.StimElectrodes,
                    patientStimControlModel.Card3.StimControl.Amp.TargetAmp.ToString() + AMP_UNITS,
                    patientStimControlModel.Card3.StimControl.Rate.TargetRate.ToString() + RATE_UNITS,
                    patientStimControlModel.Card3.StimControl.PulseWidth.TargetPulseWidth.ToString() + PULSEWIDTH_UNITS,
                    GetTargetStimAndUnits(patientStimControlModel.Card3.StimControl.StimControlType, 2).Item1,
                    GetTargetStimAndUnits(patientStimControlModel.Card3.StimControl.StimControlType, 2).Item2,
                    false,
                    false,
                    patientStimControlModel.Card3.StimControl.Amp.TargetAmp,
                    patientStimControlModel.Card3.StimControl.Rate.TargetRate,
                    patientStimControlModel.Card3.StimControl.PulseWidth.TargetPulseWidth,
                    patientStimControlModel.Card3.StimControl.PulseWidth.PulseWidthValues,
                    patientStimControlModel.Card3.StimControl.Rate.RateValues,
                    patientStimControlModel.Card3.StimControl.Amp.AmpValues,
                    patientStimControlModel.Card3.StimControl.Rate.SenseFriendly,
                    (int)patientStimControlModel.Card3.StimControl.StimControlType,
                    patientStimControlModel.Card3.StimControl.Program
                    ));
            }

            //Card 4
            if (!patientStimControlModel.Card4.HideCard)
            {
                StimParameterModel stim = (StimParameterModel)groupData[new Tuple<string, int>("Group " + patientStimControlModel.Card4.Group, patientStimControlModel.Card4.StimControl.Program)];
                cardStimSettings.Add(numberOfActiveCards, stim);
                numberOfActiveCards++;

                ListOfCards.Add(new Card(patientStimControlModel.Card4.DisplaySettings.HideGroupDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card4.DisplaySettings.HideSiteDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card4.DisplaySettings.HideAmpDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card4.DisplaySettings.HideRateDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card4.DisplaySettings.HidePulseWidthDisplay ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card4.StimControl.HideCurrentValue ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card4.StimControl.HideCurrentValueUnits ? Visibility.Hidden : Visibility.Visible,
                    patientStimControlModel.Card4.CustomText.LimitLength(MAX_LENGTH_CUSTOM_TEXT),
                    "Group " + patientStimControlModel.Card4.Group,
                    stim.StimElectrodes,
                    patientStimControlModel.Card4.StimControl.Amp.TargetAmp.ToString() + AMP_UNITS,
                    patientStimControlModel.Card4.StimControl.Rate.TargetRate.ToString() + RATE_UNITS,
                    patientStimControlModel.Card4.StimControl.PulseWidth.TargetPulseWidth.ToString() + PULSEWIDTH_UNITS,
                    GetTargetStimAndUnits(patientStimControlModel.Card4.StimControl.StimControlType, 3).Item1,
                    GetTargetStimAndUnits(patientStimControlModel.Card4.StimControl.StimControlType, 3).Item2,
                    false,
                    false,
                    patientStimControlModel.Card4.StimControl.Amp.TargetAmp,
                    patientStimControlModel.Card4.StimControl.Rate.TargetRate,
                    patientStimControlModel.Card4.StimControl.PulseWidth.TargetPulseWidth,
                    patientStimControlModel.Card4.StimControl.PulseWidth.PulseWidthValues,
                    patientStimControlModel.Card4.StimControl.Rate.RateValues,
                    patientStimControlModel.Card4.StimControl.Amp.AmpValues,
                    patientStimControlModel.Card4.StimControl.Rate.SenseFriendly,
                    (int)patientStimControlModel.Card4.StimControl.StimControlType,
                    patientStimControlModel.Card4.StimControl.Program
                    ));
            }
            #endregion
            #endregion
        }

        #region Button Clicks
        /// <summary>
        /// Left Stim On Button Click
        /// </summary>
        public async Task StimOnButtonClick()
        {
            var result = System.Windows.Forms.AutoClosingMessageBox.Show(
                text: "You are about to turn stim therapy on. Do you want to proceed?",
                caption: "Please Confirm",
                timeout: 10000,
                buttons: MessageBoxButtons.YesNo,
                defaultResult: DialogResult.No);
            if (result == DialogResult.No)
            {
                return;
            }

            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeStimTherapyON(theSummitSystem);
            if (valueReturn.Item1)
            {
                IsStimOnButtonEnabled = false;
                IsStimOffButtonEnabled = true;
                HelperFunctions.WriteEventLog(theSummitSystem, "Patient Stim Control", "Card: " + (activeCardIndex + 1) + ", Change: Stim, | TherapyActive | ", "Could not create event log for: Stim On", _log);
            }
            else
            {
                ShowMessageBox.Show(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Left Stim Off Button Click
        /// </summary>
        public async Task StimOffButtonClick()
        {
            var result = System.Windows.Forms.AutoClosingMessageBox.Show(
                text: "You are about to turn stim therapy off. Do you want to proceed?",
                caption: "Please Confirm",
                timeout: 10000,
                buttons: MessageBoxButtons.YesNo,
                defaultResult: DialogResult.No);
            if (result == DialogResult.No)
            {
                return;
            }
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeStimTherapyOFF(theSummitSystem);
            if (valueReturn.Item1)
            {
                IsStimOnButtonEnabled = true;
                IsStimOffButtonEnabled = false;
                HelperFunctions.WriteEventLog(theSummitSystem, "Patient Stim Control", "Card: " + (activeCardIndex + 1) + ", Change: Stim, | TherapyOff | ", "Could not create event log for: Stim Off", _log);
            }
            else
            {
                ShowMessageBox.Show(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Move to new Card and Update Settings
        /// </summary>
        /// <returns>async Task</returns>
        public async Task UpdateCardButtonClick()
        {
            var result = System.Windows.Forms.AutoClosingMessageBox.Show(
                    text: "You are about to move to a different card. Do you want to proceed?",
                    caption: "Please Confirm",
                    timeout: 10000,
                    buttons: MessageBoxButtons.YesNo,
                    defaultResult: DialogResult.No);
            if (result == DialogResult.Yes)
            {
                activeCard = SelectedCardItem;
                activeCardIndex = SelectedCardIndex;
                await UpdateSettings(ListOfCards[activeCardIndex]);
                IsUpdateStimButtonEnabled = false;
                switch (SelectedCardIndex)
                {
                    case 0:
                        SetCardUISettings(0);
                        switch (ListOfCards[activeCardIndex].StimControlType)
                        {
                            case 0:
                                SetCardDecrementIncrementButtonUISettingsForAmp(ListOfCards[activeCardIndex]);
                                break;
                            case 1:
                                SetCardDecrementIncrementButtonUISettingsForRate(ListOfCards[activeCardIndex]);
                                break;
                            case 2:
                                SetCardDecrementIncrementButtonUISettingsForPW(ListOfCards[activeCardIndex]);
                                break;
                            default:
                                return;
                        }
                        break;
                    case 1:
                        SetCardUISettings(1);
                        switch (ListOfCards[activeCardIndex].StimControlType)
                        {
                            case 0:
                                SetCardDecrementIncrementButtonUISettingsForAmp(ListOfCards[activeCardIndex]);
                                break;
                            case 1:
                                SetCardDecrementIncrementButtonUISettingsForRate(ListOfCards[activeCardIndex]);
                                break;
                            case 2:
                                SetCardDecrementIncrementButtonUISettingsForPW(ListOfCards[activeCardIndex]);
                                break;
                            default:
                                return;
                        }
                        break;
                    case 2:
                        SetCardUISettings(2);
                        switch (ListOfCards[activeCardIndex].StimControlType)
                        {
                            case 0:
                                SetCardDecrementIncrementButtonUISettingsForAmp(ListOfCards[activeCardIndex]);
                                break;
                            case 1:
                                SetCardDecrementIncrementButtonUISettingsForRate(ListOfCards[activeCardIndex]);
                                break;
                            case 2:
                                SetCardDecrementIncrementButtonUISettingsForPW(ListOfCards[activeCardIndex]);
                                break;
                            default:
                                return;
                        }
                        break;
                    case 3:
                        SetCardUISettings(3);
                        switch (ListOfCards[activeCardIndex].StimControlType)
                        {
                            case 0:
                                SetCardDecrementIncrementButtonUISettingsForAmp(ListOfCards[activeCardIndex]);
                                break;
                            case 1:
                                SetCardDecrementIncrementButtonUISettingsForRate(ListOfCards[activeCardIndex]);
                                break;
                            case 2:
                                SetCardDecrementIncrementButtonUISettingsForPW(ListOfCards[activeCardIndex]);
                                break;
                            default:
                                return;
                        }
                        break;
                    default:
                        SetCardUISettings(-1);
                        break;
                }
            }
            if (result == DialogResult.No)
            {
                SelectedCardItem = activeCard;
                SelectedCardIndex = activeCardIndex;
            }
               
            IsUpdateCardButtonEnabled = false;
        }
        /// <summary>
        /// Move to new Stim Settings
        /// </summary>
        /// <returns>async Task</returns>
        public async Task UpdateStimButtonClick()
        {
            var result = System.Windows.Forms.AutoClosingMessageBox.Show(
                    text: "You are about to change stim settings. Do you want to proceed?",
                    caption: "Please Confirm",
                    timeout: 10000,
                    buttons: MessageBoxButtons.YesNo,
                    defaultResult: DialogResult.No);
            if (result == DialogResult.Yes)
            {
                SummitStim summitStim = new SummitStim(_log);
                StimParameterModel stimParameterModel = (StimParameterModel)cardStimSettings[activeCardIndex];
                //Based on stim control type, change the settings for the card of that type
                switch (ListOfCards[activeCardIndex].StimControlType)
                {
                    case 0:
                        bool success = await Task.Run(() => ChangeAmpOnDevice(stimParameterModel, currentNotUpdatedSelectedAmp, (byte)ListOfCards[activeCardIndex].ProgramNumber));
                        if (success)
                        {
                            currentUpdatedSelectedAmp = currentNotUpdatedSelectedAmp;
                            IsUpdateStimButtonEnabled = false;
                        }
                        break;
                    case 1:
                        success = await Task.Run(() => ChangeRateOnDevice(stimParameterModel, currentNotUpdatedSelectedRate, ListOfCards[activeCardIndex].IsSenseFriendly));
                        if (success)
                        {
                            currentUpdatedSelectedRate = currentNotUpdatedSelectedRate;
                            IsUpdateStimButtonEnabled = false;
                        }
                        break;
                    case 2:
                        success = await Task.Run(() => ChangePWOnDevice(stimParameterModel, currentNotUpdatedSelectedPW, (byte)ListOfCards[activeCardIndex].ProgramNumber));
                        if (success)
                        {
                            currentUpdatedSelectedPW = currentNotUpdatedSelectedPW;
                            IsUpdateStimButtonEnabled = false;
                        }
                        break;
                    default:
                        return;
                }
            }
        }
        /// <summary>
        /// Decrements the Stim Change Value. This doesn't change stim until the Stim Update button has been clicked
        /// </summary>
        public void CardDecrementButtonClick()
        {
            //Based on Stim control type, access the list of the current card. Get the value of the current index minus 1.
            //If the index is the lowest, then disable decrement button
            //If the user has gone back to the value already set on device, then disable stim update button since no need to update
            switch (ListOfCards[activeCardIndex].StimControlType)
            {
                case 0:
                    int ampIndex = ListOfCards[activeCardIndex].AmpValues.IndexOf(currentNotUpdatedSelectedAmp);
                    if(ampIndex > 0)
                    {
                        currentNotUpdatedSelectedAmp = ListOfCards[activeCardIndex].AmpValues[ampIndex - 1];
                        ListOfCards[activeCardIndex].CardTargetStimDisplay = currentNotUpdatedSelectedAmp.ToString();
                        ListOfCards[activeCardIndex].CardIncrementButtonEnabled = true;
                        if (ampIndex - 1 == 0)
                        {
                            ListOfCards[activeCardIndex].CardDecrementButtonEnabled = false;
                        }
                    }
                    else
                    {
                        ListOfCards[activeCardIndex].CardDecrementButtonEnabled = false;
                    }
                    if (currentNotUpdatedSelectedAmp == currentUpdatedSelectedAmp)
                    {
                        IsUpdateStimButtonEnabled = false;
                    }
                    else
                    {
                        IsUpdateStimButtonEnabled = true;
                    }
                    break;
                case 1:
                    int rateIndex = ListOfCards[activeCardIndex].RateValues.IndexOf(currentNotUpdatedSelectedRate);
                    if (rateIndex > 0)
                    {
                        currentNotUpdatedSelectedRate = ListOfCards[activeCardIndex].RateValues[rateIndex - 1];
                        ListOfCards[activeCardIndex].CardTargetStimDisplay = currentNotUpdatedSelectedRate.ToString();
                        ListOfCards[activeCardIndex].CardIncrementButtonEnabled = true;
                        if (rateIndex - 1 == 0)
                        {
                            ListOfCards[activeCardIndex].CardDecrementButtonEnabled = false;
                        }
                    }
                    else
                    {
                        ListOfCards[activeCardIndex].CardDecrementButtonEnabled = false;
                    }
                    if (currentNotUpdatedSelectedRate == currentUpdatedSelectedRate)
                    {
                        IsUpdateStimButtonEnabled = false;
                    }
                    else
                    {
                        IsUpdateStimButtonEnabled = true;
                    }
                    break;
                case 2:
                    int pwIndex = ListOfCards[activeCardIndex].PWValues.IndexOf(currentNotUpdatedSelectedPW);
                    if (pwIndex > 0)
                    {
                        currentNotUpdatedSelectedPW = ListOfCards[activeCardIndex].PWValues[pwIndex - 1];
                        ListOfCards[activeCardIndex].CardTargetStimDisplay = currentNotUpdatedSelectedPW.ToString();
                        ListOfCards[activeCardIndex].CardIncrementButtonEnabled = true;
                        if (pwIndex - 1 == 0)
                        {
                            ListOfCards[activeCardIndex].CardDecrementButtonEnabled = false;
                        }
                    }
                    else
                    {
                        ListOfCards[activeCardIndex].CardDecrementButtonEnabled = false;
                    }
                    if (currentNotUpdatedSelectedPW == currentUpdatedSelectedPW)
                    {
                        IsUpdateStimButtonEnabled = false;
                    }
                    else
                    {
                        IsUpdateStimButtonEnabled = true;
                    }
                    break;
                default:
                    return;
            }
        }
        /// <summary>
        /// Increments the Stim Change Value. This doesn't change stim until the Stim Update button has been clicked
        /// </summary>
        public void CardIncrementButtonClick()
        {
            //Same as Decrement button click, but with the plus 1 case.
            switch (ListOfCards[activeCardIndex].StimControlType)
            {
                case 0:
                    int ampIndex = ListOfCards[activeCardIndex].AmpValues.IndexOf(currentNotUpdatedSelectedAmp);
                    if (ampIndex < ListOfCards[activeCardIndex].AmpValues.Count-1)
                    {
                        currentNotUpdatedSelectedAmp = ListOfCards[activeCardIndex].AmpValues[ampIndex + 1];
                        ListOfCards[activeCardIndex].CardTargetStimDisplay = currentNotUpdatedSelectedAmp.ToString();
                        ListOfCards[activeCardIndex].CardDecrementButtonEnabled = true;
                        if (ampIndex + 1 == ListOfCards[activeCardIndex].AmpValues.Count-1)
                        {
                            ListOfCards[activeCardIndex].CardIncrementButtonEnabled = false;
                        }
                    }
                    else
                    {
                        ListOfCards[activeCardIndex].CardIncrementButtonEnabled = false;
                    }
                    if(currentNotUpdatedSelectedAmp == currentUpdatedSelectedAmp)
                    {
                        IsUpdateStimButtonEnabled = false;
                    }
                    else
                    {
                        IsUpdateStimButtonEnabled = true;
                    }
                    break;
                case 1:
                    int rateIndex = ListOfCards[activeCardIndex].RateValues.IndexOf(currentNotUpdatedSelectedRate);
                    if (rateIndex < ListOfCards[activeCardIndex].RateValues.Count - 1)
                    {
                        currentNotUpdatedSelectedRate = ListOfCards[activeCardIndex].RateValues[rateIndex + 1];
                        ListOfCards[activeCardIndex].CardTargetStimDisplay = currentNotUpdatedSelectedRate.ToString();
                        ListOfCards[activeCardIndex].CardDecrementButtonEnabled = true;
                        if (rateIndex + 1 == ListOfCards[activeCardIndex].RateValues.Count - 1)
                        {
                            ListOfCards[activeCardIndex].CardIncrementButtonEnabled = false;
                        }
                    }
                    else
                    {
                        ListOfCards[activeCardIndex].CardIncrementButtonEnabled = false;
                    }
                    if (currentNotUpdatedSelectedRate == currentUpdatedSelectedRate)
                    {
                        IsUpdateStimButtonEnabled = false;
                    }
                    else
                    {
                        IsUpdateStimButtonEnabled = true;
                    }
                    break;
                case 2:
                    int pWIndex = ListOfCards[activeCardIndex].PWValues.IndexOf(currentNotUpdatedSelectedPW);
                    if (pWIndex < ListOfCards[activeCardIndex].PWValues.Count - 1)
                    {
                        currentNotUpdatedSelectedPW = ListOfCards[activeCardIndex].PWValues[pWIndex + 1];
                        ListOfCards[activeCardIndex].CardTargetStimDisplay = currentNotUpdatedSelectedPW.ToString();
                        ListOfCards[activeCardIndex].CardDecrementButtonEnabled = true;
                        if (pWIndex + 1 == ListOfCards[activeCardIndex].PWValues.Count - 1)
                        {
                            ListOfCards[activeCardIndex].CardIncrementButtonEnabled = false;
                        }
                    }
                    else
                    {
                        ListOfCards[activeCardIndex].CardIncrementButtonEnabled = false;
                    }
                    if (currentNotUpdatedSelectedPW == currentUpdatedSelectedPW)
                    {
                        IsUpdateStimButtonEnabled = false;
                    }
                    else
                    {
                        IsUpdateStimButtonEnabled = true;
                    }
                    break;
                default:
                    return;
            }
        }
        /// <summary>
        /// Exits window. Asks user first in case of accidental press
        /// </summary>
        public void ExitButtonClick()
        {
            var result = System.Windows.Forms.AutoClosingMessageBox.Show(
                    text: "You are about to exit. Do you want to proceed?",
                    caption: "Please Confirm",
                    timeout: 10000,
                    buttons: MessageBoxButtons.YesNo,
                    defaultResult: DialogResult.No);
            if (result == DialogResult.Yes)
            {
                TryClose();
            }
        }
        #endregion

        #region UI Bindings
        /// <summary>
        /// This is the selected card in the list of cards
        /// </summary>
        public Card SelectedCardItem
        {
            get { return _selectedCardItem; }
            set
            {
                _selectedCardItem = value;
                NotifyOfPropertyChange(() => SelectedCardItem);
                if (activeCard != SelectedCardItem)
                {
                    IsUpdateCardButtonEnabled = true;
                }
                else
                {
                    IsUpdateCardButtonEnabled = false;
                }

            }
        }
        /// <summary>
        /// Selected card index
        /// </summary>
        public int SelectedCardIndex
        {
            get { return _selectedCardIndex; }
            set
            {
                _selectedCardIndex = value;
                NotifyOfPropertyChange(() => SelectedCardIndex);
            }
        }
        /// <summary>
        /// Button enables for stim on button
        /// </summary>
        public bool IsStimOnButtonEnabled
        {
            get { return _isStimOnButtonEnabled; }
            set
            {
                _isStimOnButtonEnabled = value;
                NotifyOfPropertyChange(() => IsStimOnButtonEnabled);
            }
        }
        /// <summary>
        /// Button enables for stim off button
        /// </summary>
        public bool IsStimOffButtonEnabled
        {
            get { return _isStimOffButtonEnabled; }
            set
            {
                _isStimOffButtonEnabled = value;
                NotifyOfPropertyChange(() => IsStimOffButtonEnabled);
            }
        }
        /// <summary>
        /// Sets visiblity for the StimOnButtonVisibilty
        /// </summary>
        public Visibility StimOnButtonVisibilty
        {
            get { return _stimOnButtonVisibilty; }
            set
            {
                _stimOnButtonVisibilty = value;
                NotifyOfPropertyChange(() => StimOnButtonVisibilty);
            }
        }
        /// <summary>
        /// Sets visiblity for the StimOffButtonVisibilty
        /// </summary>
        public Visibility StimOffButtonVisibilty
        {
            get { return _stimOffButtonVisibilty; }
            set
            {
                _stimOffButtonVisibilty = value;
                NotifyOfPropertyChange(() => StimOffButtonVisibilty);
            }
        }

        /// <summary>
        /// Button enables for update card button
        /// </summary>
        public bool IsUpdateCardButtonEnabled
        {
            get { return _isUpdateCardButtonEnabled; }
            set
            {
                _isUpdateCardButtonEnabled = value;
                NotifyOfPropertyChange(() => IsUpdateCardButtonEnabled);
            }
        }

        /// <summary>
        /// Button enables for update stim button
        /// </summary>
        public bool IsUpdateStimButtonEnabled
        {
            get { return _isUpdateStimButtonEnabled; }
            set
            {
                _isUpdateStimButtonEnabled = value;
                NotifyOfPropertyChange(() => IsUpdateStimButtonEnabled);
            }
        }

        /// <summary>
        /// True displays the progress bar and false hides it
        /// </summary>
        public Visibility ProgressVisibility
        {
            get { return _progressVisibility; }
            set
            {
                _progressVisibility = value;
                NotifyOfPropertyChange(() => ProgressVisibility);
            }
        }
        /// <summary>
        /// Value for the progress for the progress bar
        /// </summary>
        public int CurrentProgress
        {
            get { return _currentProgress; }
            set
            {
                _currentProgress = value;
                NotifyOfPropertyChange(() => CurrentProgress);
            }
        }
        /// <summary>
        /// Displays the text on the progress bar
        /// </summary>
        public string ProgressText
        {
            get { return _progressText; }
            set
            {
                _progressText = value;
                NotifyOfPropertyChange(() => ProgressText);
            }
        }
        #endregion

        #region Helper functions
        /// <summary>
        /// Updates the settings when card changes
        /// Move group and update stim settings and reset the stim value to move to
        /// </summary>
        /// <param name="card">New card</param>
        /// <returns>async</returns>
        private async Task UpdateSettings(Card card)
        {
            SummitStim summitStim = new SummitStim(_log);
            StimParameterModel stimParameterModel = (StimParameterModel)cardStimSettings[activeCardIndex];

            //Move Group
            Tuple<bool, string> valueReturnGroup = await summitStim.ChangeActiveGroup(theSummitSystem, GetActiveGroupFromGroupString(card.CardGroupDisplay), senseModel);
            if (valueReturnGroup.Item1)
            {
                HelperFunctions.WriteEventLog(theSummitSystem, "Patient Stim Control", "Card: " + (activeCardIndex + 1) + ", Change: Group | " + card.CardGroupDisplay + " | , Current Settings (amp, rate, pw): " + stimParameterModel.StimAmp + ", " + stimParameterModel.StimRate + ", " + stimParameterModel.PulseWidth, "Could not create event log for: " + card.CardGroupDisplay, _log);
            }
            else
            {
                ShowMessageBox.Show(valueReturnGroup.Item2, "Error");
            }

            //Set Amp
            if (card.TargetAmp != stimParameterModel.StimAmpValue)
            {
                await ChangeAmpOnDevice(stimParameterModel, card.TargetAmp, (byte)card.ProgramNumber);
            }

            //Set Rate
            if (card.TargetRate != stimParameterModel.StimRateValue)
            {
                await ChangeRateOnDevice(stimParameterModel, card.TargetRate, card.IsSenseFriendly);
            }

            //Set Pulse Width
            if (card.TargetPulseWidth != stimParameterModel.PulseWidthValue)
            {
                await ChangePWOnDevice(stimParameterModel, card.TargetPulseWidth, (byte)card.ProgramNumber);
            }

            //Set values in stim box. Stim box is the stim settings box that the user can move to with increment/decrement buttons
            switch (card.StimControlType)
            {
                case 0:
                    currentUpdatedSelectedAmp = currentNotUpdatedSelectedAmp = card.TargetAmp;
                    break;
                case 1:
                    currentUpdatedSelectedRate = currentNotUpdatedSelectedRate = card.TargetRate;
                    break;
                case 2:
                    currentUpdatedSelectedPW = currentNotUpdatedSelectedPW = card.TargetPulseWidth;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Changes the amp
        /// </summary>
        /// <param name="stimParameterModel">Stim settings for current</param>
        /// <param name="valueToMoveTo">Value to move to</param>
        /// <param name="programNumber">program number</param>
        /// <returns>bool</returns>
        private async Task<bool> ChangeAmpOnDevice(StimParameterModel stimParameterModel, double valueToMoveTo, byte programNumber)
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, double?, string> valueReturnAmp;
            if (valueToMoveTo != stimParameterModel.StimAmpValue)
            {
                valueReturnAmp = await summitStim.ChangeStimAmpToValue(theSummitSystem, programNumber, valueToMoveTo, stimParameterModel.StimAmpValue);
                if (valueReturnAmp.Item1)
                {
                    //Change values in cardStimSettings to new values
                    stimParameterModel.StimAmpValue = (double)valueReturnAmp.Item2;
                    stimParameterModel.StimAmp = valueReturnAmp.Item2.ToString();
                    cardStimSettings[activeCardIndex] = stimParameterModel;
                    ListOfCards[activeCardIndex].CardAmpDisplay = stimParameterModel.StimAmp + AMP_UNITS;

                    HelperFunctions.WriteEventLog(theSummitSystem, "Patient Stim Control", "Card: " + (activeCardIndex + 1) + ", Change: Amp, | " + stimParameterModel.StimAmp + " | mA", "Could not create event log for: set amp", _log);
                    return true;
                }
                else
                {
                    ShowMessageBox.Show(valueReturnAmp.Item3, "Error");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Changes the rate
        /// </summary>
        /// <param name="stimParameterModel">Stim settings for current</param>
        /// <param name="rateToMoveTo">rate to move to</param>
        /// <param name="senseFriendly">sense friendly on or off</param>
        /// <returns>bool</returns>
        private async Task<bool> ChangeRateOnDevice(StimParameterModel stimParameterModel, double rateToMoveTo, bool senseFriendly)
        {
            SummitStim summitStim = new SummitStim(_log);
            //Set Rate
            Tuple<bool, double?, string> valueReturnRate;
            if (rateToMoveTo != stimParameterModel.StimRateValue)
            {
                valueReturnRate = await summitStim.ChangeStimRateToValue(theSummitSystem, senseFriendly, rateToMoveTo, stimParameterModel.StimRateValue);
                if (valueReturnRate.Item1)
                {
                    //Update UI if sensefriendly option changed
                    ListOfCards[activeCardIndex].CardRateDisplay = valueReturnRate.Item2.ToString();

                    //Change values in cardStimSettings to new values
                    stimParameterModel.StimRateValue = (double)valueReturnRate.Item2;
                    stimParameterModel.StimRate = valueReturnRate.Item2.ToString();
                    cardStimSettings[activeCardIndex] = stimParameterModel;
                    ListOfCards[activeCardIndex].CardRateDisplay = stimParameterModel.StimRate + RATE_UNITS;
                    HelperFunctions.WriteEventLog(theSummitSystem, "Patient Stim Control", "Card: " + (activeCardIndex + 1) + ", Change: Rate, | " + stimParameterModel.StimRate + " | Hz", "Could not create event log for: set rate", _log);
                    return true;
                }
                else
                {
                    ShowMessageBox.Show(valueReturnRate.Item3, "Error");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Changes the pw
        /// </summary>
        /// <param name="stimParameterModel">Stim settings for current</param>
        /// <param name="pwToMoveTo">Value to move to</param>
        /// <param name="programNumber">program number</param>
        /// <returns>bool</returns>
        private async Task<bool> ChangePWOnDevice(StimParameterModel stimParameterModel, int pwToMoveTo, byte programNumber)
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, int?, string> valueReturnPW;
            if (pwToMoveTo != stimParameterModel.PulseWidthValue)
            {
                //Set Pulse Width
                valueReturnPW = await summitStim.ChangeStimPulseWidthToValue(theSummitSystem, programNumber, pwToMoveTo, stimParameterModel.PulseWidthValue);
                if (valueReturnPW.Item1)
                {
                    //Change values in cardStimSettings to new values
                    stimParameterModel.PulseWidthValue = (int)valueReturnPW.Item2;
                    stimParameterModel.PulseWidth = valueReturnPW.Item2.ToString();
                    cardStimSettings[activeCardIndex] = stimParameterModel;
                    ListOfCards[activeCardIndex].CardPWDisplay = stimParameterModel.PulseWidth + PULSEWIDTH_UNITS;
                    HelperFunctions.WriteEventLog(theSummitSystem, "Patient Stim Control", "Card: " + (activeCardIndex + 1) + ", Change: PulseWidth, | " + stimParameterModel.PulseWidth + " | µS", "Could not create event log for: set pulse width", _log);
                    return true;
                }
                else
                {
                    ShowMessageBox.Show(valueReturnPW.Item3, "Error");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Sets all increment decrement buttons to false besides the current card
        /// </summary>
        /// <param name="CardIndex">Card index of current card</param>
        private void SetCardUISettings(int CardIndex)
        {
            if(CardIndex == -1)
            {
                for (int i = 0; i < numberOfActiveCards; i++)
                {
                    ListOfCards[i].CardDecrementButtonEnabled = false;
                    ListOfCards[i].CardIncrementButtonEnabled = false;
                    ListOfCards[i].BackgroundCardColor = Brushes.LightGray;
                }
                return;
            }
            for (int i = 0; i < numberOfActiveCards; i++)
            {
                if (i == CardIndex)
                {
                    if (ListOfCards[i].CardTargetStimDisplay.Equals(NONE_CARD_STIM_VALUE_TYPE))
                    {
                        ListOfCards[i].CardDecrementButtonEnabled = false;
                        ListOfCards[i].CardIncrementButtonEnabled = false;
                    }
                    else
                    {
                        ListOfCards[i].CardDecrementButtonEnabled = true;
                        ListOfCards[i].CardIncrementButtonEnabled = true;
                    }
                    ListOfCards[i].BackgroundCardColor = Brushes.LightBlue;
                }
                else
                {
                    ListOfCards[i].CardDecrementButtonEnabled = false;
                    ListOfCards[i].CardIncrementButtonEnabled = false;
                    ListOfCards[i].BackgroundCardColor = Brushes.LightGray;
                }
            }
        }
        /// <summary>
        /// Amp settings
        /// Sets the increment/decrement buttons based on if the list is at max value or min value
        /// Max value the increment will disable, min value decrement will disable
        /// </summary>
        /// <param name="card">current card</param>
        private void SetCardDecrementIncrementButtonUISettingsForAmp(Card card)
        {
            if (ListOfCards[activeCardIndex].AmpValues.Any())
            {
                var maxVal = ListOfCards[activeCardIndex].AmpValues.Max<double>();
                var minVal = ListOfCards[activeCardIndex].AmpValues.Min<double>();
                if (maxVal <= card.TargetAmp)
                {
                    ListOfCards[SelectedCardIndex].CardIncrementButtonEnabled = false;
                }
                if (minVal >= card.TargetAmp)
                {
                    ListOfCards[SelectedCardIndex].CardDecrementButtonEnabled = false;
                }
            }
            else
            {
                ListOfCards[SelectedCardIndex].CardIncrementButtonEnabled = false;
                ListOfCards[SelectedCardIndex].CardDecrementButtonEnabled = false;
            }
        }
        /// <summary>
        /// Rate settings
        /// Sets the increment/decrement buttons based on if the list is at max value or min value
        /// Max value the increment will disable, min value decrement will disable
        /// </summary>
        /// <param name="card">current card</param>
        private void SetCardDecrementIncrementButtonUISettingsForRate(Card card)
        {
            if (ListOfCards[activeCardIndex].RateValues.Any())
            {
                double maxVal = ListOfCards[activeCardIndex].RateValues.Max<double>();
                double minVal = ListOfCards[activeCardIndex].RateValues.Min<double>();
                if (maxVal <= card.TargetRate)
                {
                    ListOfCards[SelectedCardIndex].CardIncrementButtonEnabled = false;
                }
                if (minVal >= card.TargetRate)
                {
                    ListOfCards[SelectedCardIndex].CardDecrementButtonEnabled = false;
                }
            }
            else
            {
                ListOfCards[SelectedCardIndex].CardIncrementButtonEnabled = false;
                ListOfCards[SelectedCardIndex].CardDecrementButtonEnabled = false;
            }
        }
        /// <summary>
        /// PW settings
        /// Sets the increment/decrement buttons based on if the list is at max value or min value
        /// Max value the increment will disable, min value decrement will disable
        /// </summary>
        /// <param name="card">current card</param>
        private void SetCardDecrementIncrementButtonUISettingsForPW(Card card)
        {
            if (ListOfCards[activeCardIndex].PWValues.Any())
            {
                int maxVal = ListOfCards[activeCardIndex].PWValues.Max<int>();
                int minVal = ListOfCards[activeCardIndex].PWValues.Min<int>();
                if (maxVal <= card.TargetPulseWidth)
                {
                    ListOfCards[SelectedCardIndex].CardIncrementButtonEnabled = false;
                }
                if (minVal >= card.TargetPulseWidth)
                {
                    ListOfCards[SelectedCardIndex].CardDecrementButtonEnabled = false;
                }
            }
            else
            {
                ListOfCards[SelectedCardIndex].CardIncrementButtonEnabled = false;
                ListOfCards[SelectedCardIndex].CardDecrementButtonEnabled = false;
            }
        }
        /// <summary>
        /// Gets the target stim and units from config file
        /// </summary>
        /// <param name="StimControlType">0-3 based on amp, rate, pw, or none, repectively</param>
        /// <param name="cardNumber">index of card</param>
        /// <returns>Tuple(target value, units)</returns>
        private Tuple<string, string> GetTargetStimAndUnits(uint StimControlType, int cardNumber)
        {
            switch (StimControlType)
            {
                case 0:
                    switch (cardNumber)
                    {
                        case 0:
                            return new Tuple<string, string>(patientStimControlModel.Card1.StimControl.Amp.TargetAmp.ToString(), AMP_UNITS);
                        case 1:
                            return new Tuple<string, string>(patientStimControlModel.Card2.StimControl.Amp.TargetAmp.ToString(), AMP_UNITS);
                        case 2:
                            return new Tuple<string, string>(patientStimControlModel.Card3.StimControl.Amp.TargetAmp.ToString(), AMP_UNITS);
                        case 3:
                            return new Tuple<string, string>(patientStimControlModel.Card4.StimControl.Amp.TargetAmp.ToString(), AMP_UNITS);
                        default:
                            break;
                    }
                    break;
                case 1:
                    switch (cardNumber)
                    {
                        case 0:
                            return new Tuple<string, string>(patientStimControlModel.Card1.StimControl.Rate.TargetRate.ToString(), RATE_UNITS);
                        case 1:
                            return new Tuple<string, string>(patientStimControlModel.Card2.StimControl.Rate.TargetRate.ToString(), RATE_UNITS);
                        case 2:
                            return new Tuple<string, string>(patientStimControlModel.Card3.StimControl.Rate.TargetRate.ToString(), RATE_UNITS);
                        case 3:
                            return new Tuple<string, string>(patientStimControlModel.Card4.StimControl.Rate.TargetRate.ToString(), RATE_UNITS);
                        default:
                            break;
                    }
                    break;
                case 2:
                    switch (cardNumber)
                    {
                        case 0:
                            return new Tuple<string, string>(patientStimControlModel.Card1.StimControl.PulseWidth.TargetPulseWidth.ToString(), PULSEWIDTH_UNITS);
                        case 1:
                            return new Tuple<string, string>(patientStimControlModel.Card2.StimControl.PulseWidth.TargetPulseWidth.ToString(), PULSEWIDTH_UNITS);
                        case 2:
                            return new Tuple<string, string>(patientStimControlModel.Card3.StimControl.PulseWidth.TargetPulseWidth.ToString(), PULSEWIDTH_UNITS);
                        case 3:
                            return new Tuple<string, string>(patientStimControlModel.Card4.StimControl.PulseWidth.TargetPulseWidth.ToString(), PULSEWIDTH_UNITS);
                        default:
                            break;
                    }
                    break;
                case 3:
                    return new Tuple<string, string>(NONE_CARD_STIM_VALUE_TYPE, NONE_UNITS);
                default:
                    return new Tuple<string, string>(NONE_CARD_STIM_VALUE_TYPE, NONE_UNITS);
            }
            return new Tuple<string, string>(NONE_CARD_STIM_VALUE_TYPE, NONE_UNITS);
        }
        /// <summary>
        /// Gets the active group in Medtronic enum
        /// </summary>
        /// <param name="group">Group A, Group B, etc</param>
        /// <returns>Medtronic enum of each group</returns>
        private ActiveGroup GetActiveGroupFromGroupString(string group)
        {
            switch (group)
            {
                case "Group A":
                    return ActiveGroup.Group0;
                case "Group B":
                    return ActiveGroup.Group1;
                case "Group C":
                    return ActiveGroup.Group2;
                case "Group D":
                    return ActiveGroup.Group3;
                default:
                    return ActiveGroup.Group0;
            }
        }
        #endregion
    }

    /// <summary>
    /// Card Object for ListBox
    /// </summary>
    public class Card : Caliburn.Micro.Screen
    {
        private Visibility _cardGroupDisplayVisibility, _cardSiteDisplayVisibility, _cardAmpDisplayVisibility, _cardRateDisplayVisibility, _cardPWDisplayVisibility,
            _cardTargetStimDisplayVisibility, _cardTargetStimUnitsDisplayVisibility;
        private string _cardCustomTextDisplay, _cardGroupDisplay, _cardSiteDisplay, _cardAmpDisplay, _cardRateDisplay, _cardPWDisplay,
            _cardTargetStimDisplay, _cardTargetStimUnitsDisplay;
        private bool _cardDecrementButtonEnabled, _cardIncrementButtonEnabled;
        private Brush _backgroundCardColor = Brushes.LightGray;
        private Brush _selectedCardColor = Brushes.Orange;
        private int _targetPulseWidth, _stimControlType, _programNumber;
        private double _targetRate, _targetAmp;
        private List<int> _pWValues;
        private List<double> _rateValues;
        private List<double> _ampValues;
        private bool _isSenseFriendly;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cardGroupDisplayVisibility"></param>
        /// <param name="cardSiteDisplayVisibility"></param>
        /// <param name="cardAmpDisplayVisibility"></param>
        /// <param name="cardRateDisplayVisibility"></param>
        /// <param name="cardPWDisplayVisibility"></param>
        /// <param name="cardTargetStimDisplayVisibility"></param>
        /// <param name="cardTargetStimUnitsDisplayVisibility"></param>
        /// <param name="cardCustomTextDisplay"></param>
        /// <param name="cardGroupDisplay"></param>
        /// <param name="cardSiteDisplay"></param>
        /// <param name="cardAmpDisplay"></param>
        /// <param name="cardRateDisplay"></param>
        /// <param name="cardPWDisplay"></param>
        /// <param name="cardTargetStimDisplay"></param>
        /// <param name="cardTargetStimUnitsDisplay"></param>
        /// <param name="cardDecrementButtonEnabled"></param>
        /// <param name="cardIncrementButtonEnabled"></param>
        /// <param name="targetAmp"></param>
        /// <param name="targetRate"></param>
        /// <param name="targetPulseWidth"></param>
        /// <param name="PulseWidthValues"></param>
        /// <param name="RateValues"></param>
        /// <param name="AmpValues"></param>
        /// <param name="isSenseFriendly"></param>
        /// <param name="stimControlType"></param>
        /// <param name="programNumber"></param>
        public Card(Visibility cardGroupDisplayVisibility, Visibility cardSiteDisplayVisibility, Visibility cardAmpDisplayVisibility, Visibility cardRateDisplayVisibility, Visibility cardPWDisplayVisibility, Visibility cardTargetStimDisplayVisibility, Visibility cardTargetStimUnitsDisplayVisibility, string cardCustomTextDisplay, string cardGroupDisplay, string cardSiteDisplay, string cardAmpDisplay, string cardRateDisplay, string cardPWDisplay, string cardTargetStimDisplay, string cardTargetStimUnitsDisplay, bool cardDecrementButtonEnabled, bool cardIncrementButtonEnabled, double targetAmp, double targetRate, int targetPulseWidth, List<int> PulseWidthValues, List<double> RateValues, List<double> AmpValues, bool isSenseFriendly, int stimControlType, int programNumber)
        {
            _cardGroupDisplayVisibility = cardGroupDisplayVisibility;
            _cardSiteDisplayVisibility = cardSiteDisplayVisibility;
            _cardAmpDisplayVisibility = cardAmpDisplayVisibility;
            _cardRateDisplayVisibility = cardRateDisplayVisibility;
            _cardPWDisplayVisibility = cardPWDisplayVisibility;
            _cardTargetStimDisplayVisibility = cardTargetStimDisplayVisibility;
            _cardTargetStimUnitsDisplayVisibility = cardTargetStimUnitsDisplayVisibility;
            _cardCustomTextDisplay = cardCustomTextDisplay;
            _cardGroupDisplay = cardGroupDisplay;
            _cardSiteDisplay = cardSiteDisplay;
            _cardAmpDisplay = cardAmpDisplay;
            _cardRateDisplay = cardRateDisplay;
            _cardPWDisplay = cardPWDisplay;
            _cardTargetStimDisplay = cardTargetStimDisplay;
            _cardTargetStimUnitsDisplay = cardTargetStimUnitsDisplay;
            _cardDecrementButtonEnabled = cardDecrementButtonEnabled;
            _cardIncrementButtonEnabled = cardIncrementButtonEnabled;
            _targetAmp = targetAmp;
            _targetRate = targetRate;
            _targetPulseWidth = targetPulseWidth;
            _pWValues = PulseWidthValues;
            _rateValues = RateValues;
            _ampValues = AmpValues;
            _isSenseFriendly = isSenseFriendly;
            _stimControlType = stimControlType;
            _programNumber = programNumber;
        }

        /// <summary>
        /// Gets the list of values the user can move to based on stim type
        /// </summary>
        /// <returns>IList</returns>
        public IList GetAllowedValuesBasedOnStimType()
        {
            switch (StimControlType)
            {
                case 0:
                    return AmpValues;
                case 1:
                    return RateValues;
                case 2:
                    return PWValues;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Changes the background color of the card
        /// </summary>
        public Brush BackgroundCardColor
        {
            get { return _backgroundCardColor; }
            set
            {
                _backgroundCardColor = value;
                NotifyOfPropertyChange(() => BackgroundCardColor);
            }
        }

        /// <summary>
        /// Display for the CardCustomTextDisplay
        /// </summary>
        public string CardCustomTextDisplay
        {
            get { return _cardCustomTextDisplay; }
            set
            {
                _cardCustomTextDisplay = value;
                NotifyOfPropertyChange(() => CardCustomTextDisplay);
            }
        }
        /// <summary>
        /// Display for the CardGroupDisplay
        /// </summary>
        public string CardGroupDisplay
        {
            get { return _cardGroupDisplay; }
            set
            {
                _cardGroupDisplay = value;
                NotifyOfPropertyChange(() => CardGroupDisplay);
            }
        }
        /// <summary>
        /// Display for the CardSiteDisplay
        /// </summary>
        public string CardSiteDisplay
        {
            get { return _cardSiteDisplay; }
            set
            {
                _cardSiteDisplay = value;
                NotifyOfPropertyChange(() => CardSiteDisplay);
            }
        }
        /// <summary>
        /// Display for the CardAmpDisplay
        /// </summary>
        public string CardAmpDisplay
        {
            get { return _cardAmpDisplay; }
            set
            {
                _cardAmpDisplay = value;
                NotifyOfPropertyChange(() => CardAmpDisplay);
            }
        }
        /// <summary>
        /// Display for the CardRateDisplay
        /// </summary>
        public string CardRateDisplay
        {
            get { return _cardRateDisplay; }
            set
            {
                _cardRateDisplay = value;
                NotifyOfPropertyChange(() => CardRateDisplay);
            }
        }
        /// <summary>
        /// Display for the CardPWDisplay
        /// </summary>
        public string CardPWDisplay
        {
            get { return _cardPWDisplay; }
            set
            {
                _cardPWDisplay = value;
                NotifyOfPropertyChange(() => CardPWDisplay);
            }
        }

        /// <summary>
        /// Sets visiblity for the CardGroupDisplayVisibility
        /// </summary>
        public Visibility CardGroupDisplayVisibility
        {
            get { return _cardGroupDisplayVisibility; }
            set
            {
                _cardGroupDisplayVisibility = value;
                NotifyOfPropertyChange(() => CardGroupDisplayVisibility);
            }
        }
        /// <summary>
        /// Sets visiblity for the CardSiteDisplayVisibility
        /// </summary>
        public Visibility CardSiteDisplayVisibility
        {
            get { return _cardSiteDisplayVisibility; }
            set
            {
                _cardSiteDisplayVisibility = value;
                NotifyOfPropertyChange(() => CardSiteDisplayVisibility);
            }
        }
        /// <summary>
        /// Sets visiblity for the CardAmpDisplayVisibility
        /// </summary>
        public Visibility CardAmpDisplayVisibility
        {
            get { return _cardAmpDisplayVisibility; }
            set
            {
                _cardAmpDisplayVisibility = value;
                NotifyOfPropertyChange(() => CardAmpDisplayVisibility);
            }
        }
        /// <summary>
        /// Sets visiblity for the CardRateDisplayVisibility
        /// </summary>
        public Visibility CardRateDisplayVisibility
        {
            get { return _cardRateDisplayVisibility; }
            set
            {
                _cardRateDisplayVisibility = value;
                NotifyOfPropertyChange(() => CardRateDisplayVisibility);
            }
        }
        /// <summary>
        /// Sets visiblity for the CardPWDisplayVisibility
        /// </summary>
        public Visibility CardPWDisplayVisibility
        {
            get { return _cardPWDisplayVisibility; }
            set
            {
                _cardPWDisplayVisibility = value;
                NotifyOfPropertyChange(() => CardPWDisplayVisibility);
            }
        }

        /// <summary>
        /// Display for the CardTargetStimDisplay
        /// </summary>
        public string CardTargetStimDisplay
        {
            get { return _cardTargetStimDisplay; }
            set
            {
                _cardTargetStimDisplay = value;
                NotifyOfPropertyChange(() => CardTargetStimDisplay);
            }
        }
        /// <summary>
        /// Sets visiblity for the CardTargetStimDisplayVisibility
        /// </summary>
        public Visibility CardTargetStimDisplayVisibility
        {
            get { return _cardTargetStimDisplayVisibility; }
            set
            {
                _cardTargetStimDisplayVisibility = value;
                NotifyOfPropertyChange(() => CardTargetStimDisplayVisibility);
            }
        }
        /// <summary>
        /// Display for the CardTargetStimUnitsDisplay
        /// </summary>
        public string CardTargetStimUnitsDisplay
        {
            get { return _cardTargetStimUnitsDisplay; }
            set
            {
                _cardTargetStimUnitsDisplay = value;
                NotifyOfPropertyChange(() => CardTargetStimUnitsDisplay);
            }
        }
        /// <summary>
        /// Sets visiblity for the CardTargetStimUnitsDisplayVisibility
        /// </summary>
        public Visibility CardTargetStimUnitsDisplayVisibility
        {
            get { return _cardTargetStimUnitsDisplayVisibility; }
            set
            {
                _cardTargetStimUnitsDisplayVisibility = value;
                NotifyOfPropertyChange(() => CardTargetStimUnitsDisplayVisibility);
            }
        }

        /// <summary>
        /// Sets enabled or disabled on Card decrement button
        /// </summary>
        public bool CardDecrementButtonEnabled
        {
            get { return _cardDecrementButtonEnabled; }
            set
            {
                _cardDecrementButtonEnabled = value;
                NotifyOfPropertyChange(() => CardDecrementButtonEnabled);
            }
        }
        /// <summary>
        /// Sets enabled or disabled on Card increment button
        /// </summary>
        public bool CardIncrementButtonEnabled
        {
            get { return _cardIncrementButtonEnabled; }
            set
            {
                _cardIncrementButtonEnabled = value;
                NotifyOfPropertyChange(() => CardIncrementButtonEnabled);
            }
        }

        /// <summary>
        /// Amp target to set to from config file
        /// </summary>
        public double TargetAmp
        {
            get { return _targetAmp; }
            set
            {
                _targetAmp = value;
                NotifyOfPropertyChange(() => TargetAmp);
            }
        }

        /// <summary>
        /// Rate target to set to from config file
        /// </summary>
        public double TargetRate
        {
            get { return _targetRate; }
            set
            {
                _targetRate = value;
                NotifyOfPropertyChange(() => TargetRate);
            }
        }

        /// <summary>
        /// Pulse Width target to set to from config file
        /// </summary>
        public int TargetPulseWidth
        {
            get { return _targetPulseWidth; }
            set
            {
                _targetPulseWidth = value;
                NotifyOfPropertyChange(() => TargetPulseWidth);
            }
        }

        /// <summary>
        /// Allowed values to move to
        /// </summary>
        public List<int> PWValues
        {
            get { return _pWValues; }
            set
            {
                _pWValues = value;
                NotifyOfPropertyChange(() => PWValues);
            }
        }

        /// <summary>
        /// Allowed values to move to
        /// </summary>
        public List<double> RateValues
        {
            get { return _rateValues; }
            set
            {
                _rateValues = value;
                NotifyOfPropertyChange(() => RateValues);
            }
        }

        /// <summary>
        /// Allowed values to move to
        /// </summary>
        public List<double> AmpValues
        {
            get { return _ampValues; }
            set
            {
                _ampValues = value;
                NotifyOfPropertyChange(() => AmpValues);
            }
        }

        /// <summary>
        /// true if sense friendly for rate or false if set to actual value
        /// </summary>
        public bool IsSenseFriendly
        {
            get { return _isSenseFriendly; }
            set
            {
                _isSenseFriendly = value;
                NotifyOfPropertyChange(() => IsSenseFriendly);
            }
        }

        /// <summary>
        /// Stim control type 0-amp, 1-rate, 2-pulse width or 3-None
        /// </summary>
        public int StimControlType
        {
            get { return _stimControlType; }
            set
            {
                _stimControlType = value;
                NotifyOfPropertyChange(() => StimControlType);
            }
        }

        /// <summary>
        /// Program 0-3
        /// </summary>
        public int ProgramNumber
        {
            get { return _programNumber; }
            set
            {
                _programNumber = value;
                NotifyOfPropertyChange(() => ProgramNumber);
            }
        }
    }
    /// <summary>
    /// Extend string class to return max length chars
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Method that limits the length of text to a defined length.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="maxLength">The maximum limit of the string to return.</param>
        public static string LimitLength(this string source, int maxLength)
        {
            if (source.Length <= maxLength)
            {
                return source;
            }

            return source.Substring(0, maxLength);
        }
    }
}
