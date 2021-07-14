using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using Medtronic.NeuroStim.Olympus.DataTypes.Therapy;
using SCBS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;

namespace SCBS.ViewModels
{
    /// <summary>
    /// Researcher Tools
    /// </summary>
    public partial class MainViewModel : Caliburn.Micro.Screen
    {
        #region Variables
        //Display strings
        private string _stimLeftActiveDisplay, _stimLeftAmpDisplay, _stimLeftRateDisplay, _stimLeftPWDisplay, _stimLeftElectrode, _activeRechargeLeftStatus,
            _stimRightActiveDisplay, _stimRightAmpDisplay, _stimRightRateDisplay, _stimRightPWDisplay, _stimRightElectrode, _activeRechargeRightStatus;
        //Input Textboxes
        private string _stepAmpValueInputBox, _stimChangeAmpValueInput, _stepRateValueInputBox, _stimChangeRateInput, _stepPWValueInputBox, _stimChangePWInput;
        //Button Enables
        private bool _groupALeftButtonEnabled, _groupBLeftButtonEnabled, _groupCLeftButtonEnabled, _groupDLeftButtonEnabled,
            _groupARightButtonEnabled, _groupBRightButtonEnabled, _groupCRightButtonEnabled, _groupDRightButtonEnabled,
            _stimOnLeftButtonEnabled, _stimOffLeftButtonEnabled, _stimOnRightButtonEnabled, _stimOffRightButtonEnabled, 
            _stimSettingButtonsEnabled, _updateSenseButtonEnabled;
        private bool _senseFriendlyCheckbox = true;
        private int _pWLowerLimitLeft, _pWUpperLimitLeft, _pWLowerLimitRight, _pWUpperLimitRight;
        private double _rateLowerLimitLeft, _rateUpperLimitLeft, _ampLowerLimitLeft, _ampUpperLimitLeft,
            _rateLowerLimitRight, _rateUpperLimitRight, _ampLowerLimitRight, _ampUpperLimitRight;
        //Comboboxes
        private BindableCollection<string> _programOptionsRight = new BindableCollection<string>();
        private string _selectedProgramRight;
        private BindableCollection<string> _programOptionsLeft = new BindableCollection<string>();
        private string _selectedProgramLeft;
        private const string program0Option = "Program 0";
        private const string program1Option = "Program 1";
        private const string program2Option = "Program 2";
        private const string program3Option = "Program 3";
        private BindableCollection<string> _deviceOptions = new BindableCollection<string>();
        private string _selectedDevice;
        private const string leftUnilateralDeviceOption = "Left/Unilateral";
        private const string rightDeviceOption = "Right";
        private const string bothDeviceOption = "Both";
        private FFTVisualizerViewModel fftVisualizer;
        private Brush _therapyStatusBackgroundLeft, _therapyStatusBackgroundRight;
        //Stop watch vars for amp change
        private Stopwatch stopWatchChangeTimer = new Stopwatch();
        private DispatcherTimer dispatcherChangeTimer = new DispatcherTimer();
        private string _changeTimerText;
        #endregion

        #region Bindings
        #region Left Display Settings
        /// <summary>
        /// Changes background to show if therapy is on or off. 
        /// </summary>
        public Brush TherapyStatusBackgroundLeft
        {
            get { return _therapyStatusBackgroundLeft ?? (_therapyStatusBackgroundLeft = Brushes.LightGray); }
            set
            {
                _therapyStatusBackgroundLeft = value;
                NotifyOfPropertyChange(() => TherapyStatusBackgroundLeft);
            }
        }
        /// <summary>
        /// Binding that Shows if stim is on or off to user
        /// </summary>
        public string StimLeftActiveDisplay
        {
            get { return _stimLeftActiveDisplay; }
            set
            {
                _stimLeftActiveDisplay = value;
                NotifyOfPropertyChange(() => StimLeftActiveDisplay);
            }
        }
        /// <summary>
        /// Binding that Shows stim amp to user
        /// </summary>
        public string StimLeftAmpDisplay
        {
            get { return _stimLeftAmpDisplay; }
            set
            {
                _stimLeftAmpDisplay = value;
                NotifyOfPropertyChange(() => StimLeftAmpDisplay);
            }
        }
        /// <summary>
        /// Binding that Shows stim rate to user
        /// </summary>
        public string StimLeftRateDisplay
        {
            get { return _stimLeftRateDisplay; }
            set
            {
                _stimLeftRateDisplay = value;
                NotifyOfPropertyChange(() => StimLeftRateDisplay);
            }
        }
        /// <summary>
        /// Binding that Shows stim pulse width to user
        /// </summary>
        public string StimLeftPWDisplay
        {
            get { return _stimLeftPWDisplay; }
            set
            {
                _stimLeftPWDisplay = value;
                NotifyOfPropertyChange(() => StimLeftPWDisplay);
            }
        }
        /// <summary>
        /// Binding that shows the electrodes that are stimming
        /// </summary>
        public string StimLeftElectrode
        {
            get { return _stimLeftElectrode; }
            set
            {
                _stimLeftElectrode = value;
                NotifyOfPropertyChange(() => StimLeftElectrode);
            }
        }
        /// <summary>
        /// Binding that shows active recharge status
        /// </summary>
        public string ActiveRechargeLeftStatus
        {
            get { return _activeRechargeLeftStatus; }
            set
            {
                _activeRechargeLeftStatus = value;
                NotifyOfPropertyChange(() => ActiveRechargeLeftStatus);
            }
        }
        /// <summary>
        /// Binding for the pulse width lower limit
        /// </summary>
        public int PWLowerLimitLeft
        {
            get { return _pWLowerLimitLeft; }
            set
            {
                _pWLowerLimitLeft = value;
                NotifyOfPropertyChange(() => PWLowerLimitLeft);
            }
        }
        /// <summary>
        /// Binding for the pulse width upper limit
        /// </summary>
        public int PWUpperLimitLeft
        {
            get { return _pWUpperLimitLeft; }
            set
            {
                _pWUpperLimitLeft = value;
                NotifyOfPropertyChange(() => PWUpperLimitLeft);
            }
        }
        /// <summary>
        /// Binding for the rate lower limit
        /// </summary>
        public double RateLowerLimitLeft
        {
            get { return _rateLowerLimitLeft; }
            set
            {
                _rateLowerLimitLeft = value;
                NotifyOfPropertyChange(() => RateLowerLimitLeft);
            }
        }
        /// <summary>
        /// Binding for the rate upper limit
        /// </summary>
        public double RateUpperLimitLeft
        {
            get { return _rateUpperLimitLeft; }
            set
            {
                _rateUpperLimitLeft = value;
                NotifyOfPropertyChange(() => RateUpperLimitLeft);
            }
        }
        /// <summary>
        /// Binding for the amp lower limit
        /// </summary>
        public double AmpLowerLimitLeft
        {
            get { return _ampLowerLimitLeft; }
            set
            {
                _ampLowerLimitLeft = value;
                NotifyOfPropertyChange(() => AmpLowerLimitLeft);
            }
        }
        /// <summary>
        /// Binding for the amp upper limit
        /// </summary>
        public double AmpUpperLimitLeft
        {
            get { return _ampUpperLimitLeft; }
            set
            {
                _ampUpperLimitLeft = value;
                NotifyOfPropertyChange(() => AmpUpperLimitLeft);
            }
        }
        #endregion
        #region Left Group Button Enabled Bindings
        /// <summary>
        /// Group A button enabled/disabled
        /// </summary>
        public bool GroupALeftButtonEnabled
        {
            get { return _groupALeftButtonEnabled; }
            set
            {
                _groupALeftButtonEnabled = value;
                NotifyOfPropertyChange(() => GroupALeftButtonEnabled);
            }
        }
        /// <summary>
        /// Group B button enabled/disabled
        /// </summary>
        public bool GroupBLeftButtonEnabled
        {
            get { return _groupBLeftButtonEnabled; }
            set
            {
                _groupBLeftButtonEnabled = value;
                NotifyOfPropertyChange(() => GroupBLeftButtonEnabled);
            }
        }
        /// <summary>
        /// Group C button enabled/disabled
        /// </summary>
        public bool GroupCLeftButtonEnabled
        {
            get { return _groupCLeftButtonEnabled; }
            set
            {
                _groupCLeftButtonEnabled = value;
                NotifyOfPropertyChange(() => GroupCLeftButtonEnabled);
            }
        }
        /// <summary>
        /// Group D button enabled/disabled
        /// </summary>
        public bool GroupDLeftButtonEnabled
        {
            get { return _groupDLeftButtonEnabled; }
            set
            {
                _groupDLeftButtonEnabled = value;
                NotifyOfPropertyChange(() => GroupDLeftButtonEnabled);
            }
        }
        #endregion
        #region Left/Right Stim On/Off Button Enabled Bindings
        /// <summary>
        /// Turns on/off stim settings buttons for increment, decrement and settings stim values
        /// </summary>
        public bool StimSettingButtonsEnabled
        {
            get { return _stimSettingButtonsEnabled; }
            set
            {
                _stimSettingButtonsEnabled = value;
                NotifyOfPropertyChange(() => StimSettingButtonsEnabled);
            }
        }
        /// <summary>
        /// Turns stim on
        /// </summary>
        public bool StimOnLeftButtonEnabled
        {
            get { return _stimOnLeftButtonEnabled; }
            set
            {
                _stimOnLeftButtonEnabled = value;
                NotifyOfPropertyChange(() => StimOnLeftButtonEnabled);
            }
        }
        /// <summary>
        /// Turns stim off
        /// </summary>
        public bool StimOffLeftButtonEnabled
        {
            get { return _stimOffLeftButtonEnabled; }
            set
            {
                _stimOffLeftButtonEnabled = value;
                NotifyOfPropertyChange(() => StimOffLeftButtonEnabled);
            }
        }
        /// <summary>
        /// Turns stim on
        /// </summary>
        public bool StimOnRightButtonEnabled
        {
            get { return _stimOnRightButtonEnabled; }
            set
            {
                _stimOnRightButtonEnabled = value;
                NotifyOfPropertyChange(() => StimOnRightButtonEnabled);
            }
        }
        /// <summary>
        /// Turns stim off
        /// </summary>
        public bool StimOffRightButtonEnabled
        {
            get { return _stimOffRightButtonEnabled; }
            set
            {
                _stimOffRightButtonEnabled = value;
                NotifyOfPropertyChange(() => StimOffRightButtonEnabled);
            }
        }
        #endregion
        #region Both Stim Change Textbox and Checkbox Bindings
        /// <summary>
        /// Timer that resets when there is a change in stim amp, rate, group or new session
        /// </summary>
        public string ChangeTimerText
        {
            get { return _changeTimerText; }
            set
            {
                _changeTimerText = value;
                NotifyOfPropertyChange(() => ChangeTimerText);
            }
        }
        /// <summary>
        /// Binding for the step value to change stim up or down
        /// </summary>
        public string StepAmpValueInputBox
        {
            get { return _stepAmpValueInputBox; }
            set
            {
                _stepAmpValueInputBox = value;
                NotifyOfPropertyChange(() => StepAmpValueInputBox);
            }
        }
        /// <summary>
        /// Binding for stim value to change to when clicking Go
        /// </summary>
        public string StimChangeAmpValueInput
        {
            get { return _stimChangeAmpValueInput; }
            set
            {
                _stimChangeAmpValueInput = value;
                NotifyOfPropertyChange(() => StimChangeAmpValueInput);
            }
        }
        /// <summary>
        /// Binding for stim rate value to change to when clicking Go
        /// </summary>
        public string StepRateValueInputBox
        {
            get { return _stepRateValueInputBox; }
            set
            {
                _stepRateValueInputBox = value;
                NotifyOfPropertyChange(() => StepRateValueInputBox);
            }
        }
        /// <summary>
        /// Binding for stim rate value to change to when clicking Go
        /// </summary>
        public string StimChangeRateInput
        {
            get { return _stimChangeRateInput; }
            set
            {
                _stimChangeRateInput = value;
                NotifyOfPropertyChange(() => StimChangeRateInput);
            }
        }
        /// <summary>
        /// If true, then connect with selected CTM in combobox
        /// </summary>
        public bool SenseFriendlyCheckbox
        {
            get { return _senseFriendlyCheckbox; }
            set
            {
                _senseFriendlyCheckbox = value;
                NotifyOfPropertyChange(() => SenseFriendlyCheckbox);
            }
        }
        /// <summary>
        /// Binding for the step value to change stim pulse width up or down
        /// </summary>
        public string StepPWValueInputBox
        {
            get { return _stepPWValueInputBox; }
            set
            {
                _stepPWValueInputBox = value;
                NotifyOfPropertyChange(() => StepPWValueInputBox);
            }
        }
        /// <summary>
        /// Binding for stim value to change pulse width to when clicking Go
        /// </summary>
        public string StimChangePWInput
        {
            get { return _stimChangePWInput; }
            set
            {
                _stimChangePWInput = value;
                NotifyOfPropertyChange(() => StimChangePWInput);
            }
        }
        #endregion
        #region Both ComboBox and Button Binding
        /// <summary>
        /// Turns on/off update sense button
        /// </summary>
        public bool UpdateSenseButtonEnabled
        {
            get { return _updateSenseButtonEnabled; }
            set
            {
                _updateSenseButtonEnabled = value;
                NotifyOfPropertyChange(() => UpdateSenseButtonEnabled);
            }
        }
        /// <summary>
        /// Combo box drop down list for the medtronic program options 0-3
        /// </summary>
        public BindableCollection<string> ProgramOptionsLeft
        {
            get { return _programOptionsLeft; }
            set
            {
                _programOptionsLeft = value;
                NotifyOfPropertyChange(() => ProgramOptionsLeft);
            }
        }

        /// <summary>
        /// Binding for the actual option selected in the drop down menu for ProgramOptions
        /// </summary>
        public string SelectedProgramLeft
        {
            get { return _selectedProgramLeft; }
            set
            {
                _selectedProgramLeft = value;
                NotifyOfPropertyChange(() => SelectedProgramLeft);
                if (isLeftConnected)
                {
                    StimLeftPWDisplay = stimParameterModelLeft.TherapyGroup.Programs[ProgramOptionsLeft.IndexOf(SelectedProgramLeft)].PulseWidthInMicroseconds.ToString() + " µS";
                    StimRateLeft = stimParameterModelLeft.TherapyGroup.RateInHz.ToString() + " Hz"; ;
                    StimAmpLeft = stimParameterModelLeft.TherapyGroup.Programs[ProgramOptionsLeft.IndexOf(SelectedProgramLeft)].AmplitudeInMilliamps.ToString() + " mA";
                    StimElectrodeLeft = stimInfoLeft.FindStimElectrodes(stimParameterModelLeft.TherapyGroup, ProgramOptionsLeft.IndexOf(SelectedProgramLeft));
                    ActiveRechargeLeftStatus = stimParameterModelLeft.TherapyGroup.Programs[ProgramOptionsLeft.IndexOf(SelectedProgramLeft)].MiscSettings.ActiveRechargeRatio.Equals(ActiveRechargeRatios.PassiveOnly) ? "Passive Recharge" : "Active Recharge";
                    UpdateLeftUpperLowerStimAmp();
                }
            }
        }
        /// <summary>
        /// Combo box drop down list for the medtronic program options 0-3
        /// </summary>
        public BindableCollection<string> ProgramOptionsRight
        {
            get { return _programOptionsRight; }
            set
            {
                _programOptionsRight = value;
                NotifyOfPropertyChange(() => ProgramOptionsRight);
            }
        }

        /// <summary>
        /// Binding for the actual option selected in the drop down menu for ProgramOptions
        /// </summary>
        public string SelectedProgramRight
        {
            get { return _selectedProgramRight; }
            set
            {
                _selectedProgramRight = value;
                NotifyOfPropertyChange(() => SelectedProgramRight);
                if (isRightConnected)
                {
                    StimRightPWDisplay = stimParameterModelRight.TherapyGroup.Programs[ProgramOptionsRight.IndexOf(SelectedProgramRight)].PulseWidthInMicroseconds.ToString() + " µS";
                    StimRateRight = stimParameterModelRight.TherapyGroup.RateInHz.ToString() + " Hz";
                    StimAmpRight = stimParameterModelRight.TherapyGroup.Programs[ProgramOptionsRight.IndexOf(SelectedProgramRight)].AmplitudeInMilliamps.ToString() + " mA";
                    StimElectrodeRight = stimInfoRight.FindStimElectrodes(stimParameterModelRight.TherapyGroup, ProgramOptionsRight.IndexOf(SelectedProgramRight));
                    ActiveRechargeRightStatus = stimParameterModelRight.TherapyGroup.Programs[ProgramOptionsRight.IndexOf(SelectedProgramRight)].MiscSettings.ActiveRechargeRatio.Equals(ActiveRechargeRatios.PassiveOnly) ? "Passive Recharge" : "Active Recharge";
                    UpdateRightUpperLowerStimAmp();
                }
            }
        }
        /// <summary>
        /// Combo box drop down list for choosing device side
        /// </summary>
        public BindableCollection<string> DeviceOptions
        {
            get { return _deviceOptions; }
            set
            {
                _deviceOptions = value;
                NotifyOfPropertyChange(() => DeviceOptions);
            }
        }

        /// <summary>
        /// Binding for the actual option selected in the drop down menu for choosing device
        /// </summary>
        public string SelectedDevice
        {
            get { return _selectedDevice; }
            set
            {
                _selectedDevice = value;
                NotifyOfPropertyChange(() => SelectedDevice);
            }
        }
        #endregion
        #region Right Display Settings
        /// <summary>
        /// Changes background to show if therapy is on or off. 
        /// </summary>
        public Brush TherapyStatusBackgroundRight
        {
            get { return _therapyStatusBackgroundRight ?? (_therapyStatusBackgroundRight = Brushes.LightGray); }
            set
            {
                _therapyStatusBackgroundRight = value;
                NotifyOfPropertyChange(() => TherapyStatusBackgroundRight);
            }
        }
        /// <summary>
        /// Binding that Shows if stim is on or off to user
        /// </summary>
        public string StimRightActiveDisplay
        {
            get { return _stimRightActiveDisplay; }
            set
            {
                _stimRightActiveDisplay = value;
                NotifyOfPropertyChange(() => StimRightActiveDisplay);
            }
        }
        /// <summary>
        /// Binding that Shows stim amp to user
        /// </summary>
        public string StimRightAmpDisplay
        {
            get { return _stimRightAmpDisplay; }
            set
            {
                _stimRightAmpDisplay = value;
                NotifyOfPropertyChange(() => StimRightAmpDisplay);
            }
        }
        /// <summary>
        /// Binding that Shows stim rate to user
        /// </summary>
        public string StimRightRateDisplay
        {
            get { return _stimRightRateDisplay; }
            set
            {
                _stimRightRateDisplay = value;
                NotifyOfPropertyChange(() => StimRightRateDisplay);
            }
        }
        /// <summary>
        /// Binding that Shows stim pulse width to user
        /// </summary>
        public string StimRightPWDisplay
        {
            get { return _stimRightPWDisplay; }
            set
            {
                _stimRightPWDisplay = value;
                NotifyOfPropertyChange(() => StimRightPWDisplay);
            }
        }
        /// <summary>
        /// Binding that shows the electrodes that are stimming
        /// </summary>
        public string StimRightElectrode
        {
            get { return _stimRightElectrode; }
            set
            {
                _stimRightElectrode = value;
                NotifyOfPropertyChange(() => StimRightElectrode);
            }
        }
        /// <summary>
        /// Binding that shows active recharge status
        /// </summary>
        public string ActiveRechargeRightStatus
        {
            get { return _activeRechargeRightStatus; }
            set
            {
                _activeRechargeRightStatus = value;
                NotifyOfPropertyChange(() => ActiveRechargeRightStatus);
            }
        }
        /// <summary>
        /// Binding for the pulse width lower limit
        /// </summary>
        public int PWLowerLimitRight
        {
            get { return _pWLowerLimitRight; }
            set
            {
                _pWLowerLimitRight = value;
                NotifyOfPropertyChange(() => PWLowerLimitRight);
            }
        }
        /// <summary>
        /// Binding for the pulse width upper limit
        /// </summary>
        public int PWUpperLimitRight
        {
            get { return _pWUpperLimitRight; }
            set
            {
                _pWUpperLimitRight = value;
                NotifyOfPropertyChange(() => PWUpperLimitRight);
            }
        }
        /// <summary>
        /// Binding for the rate lower limit
        /// </summary>
        public double RateLowerLimitRight
        {
            get { return _rateLowerLimitRight; }
            set
            {
                _rateLowerLimitRight = value;
                NotifyOfPropertyChange(() => RateLowerLimitRight);
            }
        }
        /// <summary>
        /// Binding for the rate upper limit
        /// </summary>
        public double RateUpperLimitRight
        {
            get { return _rateUpperLimitRight; }
            set
            {
                _rateUpperLimitRight = value;
                NotifyOfPropertyChange(() => RateUpperLimitRight);
            }
        }
        /// <summary>
        /// Binding for the amp lower limit
        /// </summary>
        public double AmpLowerLimitRight
        {
            get { return _ampLowerLimitRight; }
            set
            {
                _ampLowerLimitRight = value;
                NotifyOfPropertyChange(() => AmpLowerLimitRight);
            }
        }
        /// <summary>
        /// Binding for the amp upper limit
        /// </summary>
        public double AmpUpperLimitRight
        {
            get { return _ampUpperLimitRight; }
            set
            {
                _ampUpperLimitRight = value;
                NotifyOfPropertyChange(() => AmpUpperLimitRight);
            }
        }
        #endregion
        #region Right Group Button Enabled Bindings
        /// <summary>
        /// Group A button enabled/disabled
        /// </summary>
        public bool GroupARightButtonEnabled
        {
            get { return _groupARightButtonEnabled; }
            set
            {
                _groupARightButtonEnabled = value;
                NotifyOfPropertyChange(() => GroupARightButtonEnabled);
            }
        }
        /// <summary>
        /// Group B button enabled/disabled
        /// </summary>
        public bool GroupBRightButtonEnabled
        {
            get { return _groupBRightButtonEnabled; }
            set
            {
                _groupBRightButtonEnabled = value;
                NotifyOfPropertyChange(() => GroupBRightButtonEnabled);
            }
        }
        /// <summary>
        /// Group C button enabled/disabled
        /// </summary>
        public bool GroupCRightButtonEnabled
        {
            get { return _groupCRightButtonEnabled; }
            set
            {
                _groupCRightButtonEnabled = value;
                NotifyOfPropertyChange(() => GroupCRightButtonEnabled);
            }
        }
        /// <summary>
        /// Group D button enabled/disabled
        /// </summary>
        public bool GroupDRightButtonEnabled
        {
            get { return _groupDRightButtonEnabled; }
            set
            {
                _groupDRightButtonEnabled = value;
                NotifyOfPropertyChange(() => GroupDRightButtonEnabled);
            }
        }
        #endregion
        #endregion

        #region Button Clicks
        #region Left Device
        /// <summary>
        /// Left Group A Button Click
        /// </summary>
        public async Task GroupALeftButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitLeft, ActiveGroup.Group0, senseLeftConfigModel);
            if (valueReturn.Item1)
            {
                UpdateStimStatusGroupLeft();
                WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: Group A", "Change: Group, | " + ActiveGroupLeft + " |");
                if (IsBilateral)
                {
                    WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: Group A", "Change: Group, | " + ActiveGroupLeft + " |");
                }
                stopWatchChangeTimer.Reset();
                stopWatchChangeTimer.Start();
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Left Group B Button Click
        /// </summary>
        public async Task GroupBLeftButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitLeft, ActiveGroup.Group1, senseLeftConfigModel);
            if (valueReturn.Item1)
            {
                UpdateStimStatusGroupLeft();
                WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: Group B", "Change: Group, | " + ActiveGroupLeft + " |");
                if (IsBilateral)
                {
                    WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: Group B", "Change: Group, | " + ActiveGroupLeft + " |");
                }
                stopWatchChangeTimer.Reset();
                stopWatchChangeTimer.Start();
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Left Group C Button Click
        /// </summary>
        public async Task GroupCLeftButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitLeft, ActiveGroup.Group2, senseLeftConfigModel);
            if (valueReturn.Item1)
            {
                UpdateStimStatusGroupLeft();
                WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: Group C", "Change: Group, | " + ActiveGroupLeft + " |");
                if (IsBilateral)
                {
                    WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: Group C", "Change: Group, | " + ActiveGroupLeft + " |");
                }
                stopWatchChangeTimer.Reset();
                stopWatchChangeTimer.Start();
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Left Group D Button Click
        /// </summary>
        public async Task GroupDLeftButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitLeft, ActiveGroup.Group3, senseLeftConfigModel);
            if (valueReturn.Item1)
            {
                UpdateStimStatusGroupLeft();
                WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: Group D", "Change: Group, | " + ActiveGroupLeft + " |");
                if (IsBilateral)
                {
                    WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: Group D", "Change: Group, | " + ActiveGroupLeft + " |");
                }
                stopWatchChangeTimer.Reset();
                stopWatchChangeTimer.Start();
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Left Stim On Button Click
        /// </summary>
        public async Task StimOnLeftButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeStimTherapyON(theSummitLeft);
            if (valueReturn.Item1)
            {
                StimOnLeftButtonEnabled = false;
                StimOffLeftButtonEnabled = true;
                TherapyStatusBackgroundLeft = Brushes.ForestGreen;
                StimStateLeft = "TherapyActive";
                WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: Stim On", "Change: Stim, | " + StimStateLeft + " |");
                if (IsBilateral)
                {
                    WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: Stim On", "Change: Stim, | " + StimStateLeft + " |");
                }
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Left Stim Off Button Click
        /// </summary>
        public async Task StimOffLeftButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeStimTherapyOFF(theSummitLeft);
            if (valueReturn.Item1)
            {
                StimOnLeftButtonEnabled = true;
                StimOffLeftButtonEnabled = false;
                TherapyStatusBackgroundLeft = Brushes.LightGray;
                StimStateLeft = "TherapyOff";
                WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: Stim Off", "Change: Stim, | " + StimStateLeft + " |");
                if (IsBilateral)
                {
                    WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: Stim Off", "Change: Stim, | " + StimStateLeft + " |");
                }
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        #endregion
        #region Right Device
        /// <summary>
        /// Right Group A Button Click
        /// </summary>
        public async Task GroupARightButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitRight, ActiveGroup.Group0, senseLeftConfigModel);
            if (valueReturn.Item1)
            {
                UpdateStimStatusGroupRight();
                WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: Group A", "Change: Group, | " + ActiveGroupRight + " |");
                WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: Group A", "Change: Group, | " + ActiveGroupRight + " |");
                stopWatchChangeTimer.Reset();
                stopWatchChangeTimer.Start();
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Right Group B Button Click
        /// </summary>
        public async Task GroupBRightButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitRight, ActiveGroup.Group1, senseLeftConfigModel);
            if (valueReturn.Item1)
            {
                UpdateStimStatusGroupRight();
                WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: Group B", "Change: Group, | " + ActiveGroupRight + " |");
                WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: Group B", "Change: Group, | " + ActiveGroupRight + " |");
                stopWatchChangeTimer.Reset();
                stopWatchChangeTimer.Start();
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Right Group C Button Click
        /// </summary>
        public async Task GroupCRightButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitRight, ActiveGroup.Group2, senseLeftConfigModel);
            if (valueReturn.Item1)
            {
                UpdateStimStatusGroupRight();
                WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: Group C", "Change: Group, | " + ActiveGroupRight + " |");
                WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: Group C", "Change: Group, | " + ActiveGroupRight + " |");
                stopWatchChangeTimer.Reset();
                stopWatchChangeTimer.Start();
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Right Group D Button Click
        /// </summary>
        public async Task GroupDRightButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitRight, ActiveGroup.Group3, senseLeftConfigModel);
            if (valueReturn.Item1)
            {
                UpdateStimStatusGroupRight();
                WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: Group D", "Change: Group, | " + ActiveGroupRight + " |");
                WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: Group D", "Change: Group, | " + ActiveGroupRight + " |");
                stopWatchChangeTimer.Reset();
                stopWatchChangeTimer.Start();
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Right Stim On Button Click
        /// </summary>
        public async Task StimOnRightButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeStimTherapyON(theSummitRight);
            if (valueReturn.Item1)
            {
                StimOnRightButtonEnabled = false;
                StimOffRightButtonEnabled = true;
                TherapyStatusBackgroundRight = Brushes.ForestGreen;
                StimStateRight = "TherapyActive";
                WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: Stim On", "Change: Stim, | " + StimStateRight + " |");
                WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: Stim On", "Change: Stim, | " + StimStateRight + " |");
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        /// <summary>
        /// Right Stim Off Button Click
        /// </summary>
        public async Task StimOffRightButtonClick()
        {
            SummitStim summitStim = new SummitStim(_log);
            Tuple<bool, string> valueReturn = await summitStim.ChangeStimTherapyOFF(theSummitRight);
            if (valueReturn.Item1)
            {
                StimOnRightButtonEnabled = true;
                StimOffRightButtonEnabled = false;
                TherapyStatusBackgroundRight = Brushes.LightGray;
                StimStateRight = "TherapyOff";
                WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: Stim Off", "Change: Stim, | " + StimStateRight + " |");
                WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: Stim Off", "Change: Stim, | " + StimStateRight + " |");
            }
            else
            {
                ShowMessageBox(valueReturn.Item2, "Error");
            }
        }
        #endregion
        #region Both Device Stim Change
        /// <summary>
        /// Runs the lead integrity test
        /// </summary>
        public async Task LeadIntegrityButtonClick()
        {
            var result = System.Windows.Forms.AutoClosingMessageBox.Show(
                text: "Would you like to run the lead integrity test? If nothing is clicked in 10 seconds, the test will NOT run.\nWarning: stimulation therapy will turn off very briefly (you may feel a slight sensation)",
                caption: "Lead Integrity Test",
                timeout: 10000,
                buttons: MessageBoxButtons.YesNo,
                defaultResult: DialogResult.No);
            if (result == DialogResult.Yes)
            {
                ProgressVisibility = Visibility.Visible;
                CurrentProgress = 5;
                ProgressText = "Running Lead Integrity...";
                summitSensing.StopSensing(theSummitLeft, false);
                CurrentProgress = 20;
                LeadIntegrityTest leadIntegrityTest = new LeadIntegrityTest(_log);
                await leadIntegrityTest.RunLeadIntegrityTest(theSummitLeft);
                CurrentProgress = 40;
                summitSensing.StartSensingAndStreaming(theSummitLeft, senseLeftConfigModel, false);
                if (IsBilateral)
                {
                    CurrentProgress = 50;
                    summitSensing.StopSensing(theSummitRight, false);
                    CurrentProgress = 70;
                    await leadIntegrityTest.RunLeadIntegrityTest(theSummitRight);
                    CurrentProgress = 90;
                    summitSensing.StartSensingAndStreaming(theSummitRight, senseRightConfigModel, false);
                }
                CurrentProgress = 100;
                ProgressVisibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// Opens the fft visualizer
        /// </summary>
        public void FFTVisualizerButtonClick()
        {
            WindowManager fFTWindow = new WindowManager();
            fftVisualizer = new FFTVisualizerViewModel(theSummitLeft, theSummitRight, senseLeftConfigModel, senseRightConfigModel, _log);
            fFTWindow.ShowWindow(fftVisualizer, null, null);
        }
        /// <summary>
        /// Reloads the sense files for left and right and updates the sense settings
        /// </summary>
        public async Task UpdateSenseButtonClick()
        {
            UpdateSenseButtonEnabled = false;
            await Task.Run(() => UpdateSenseButtonClickCode());
        }
        private async Task UpdateSenseButtonClickCode()
        {
            senseLeftConfigModel = jSONService?.GetSenseModelFromFile(senseLeftFileLocation);
            if (senseLeftConfigModel == null)
            {
                ShowMessageBox("Could not load Left/Unilateral sense file. Please try again", "Error");
                UpdateSenseButtonEnabled = true;
                return;
            }
            else
            {
                if (!CheckPacketLoss(senseLeftConfigModel))
                {
                    ShowMessageBox("ERROR: New Left/Unilateral sense config file loaded but not updated to device. Major packet loss will occur due to too much data over bandwidth.  Please fix senseLeft_config.json file and try updating sense to reload changes.");
                    UpdateSenseButtonEnabled = true;
                    return;
                }
                if (!summitSensing.StopStreaming(theSummitLeft, true))
                {
                    UpdateSenseButtonEnabled = true;
                    return;
                }
                if (!summitSensing.StopSensing(theSummitLeft, true))
                {
                    UpdateSenseButtonEnabled = true;
                    return;
                }
                if (!summitSensing.SummitConfigureSensing(theSummitLeft, senseLeftConfigModel, true))
                {
                    UpdateSenseButtonEnabled = true;
                    return;
                }
                if (!summitSensing.StartSensingAndStreaming(theSummitLeft, senseLeftConfigModel, true))
                {
                    UpdateSenseButtonEnabled = true;
                    return;
                }
            }
            if (appConfigModel.Bilateral)
            {
                senseRightConfigModel = jSONService?.GetSenseModelFromFile(senseRightFileLocation);
                if (senseRightConfigModel == null)
                {
                    ShowMessageBox("Could not load right sense file. Please try again", "Error");
                    UpdateSenseButtonEnabled = true;
                    return;
                }
                else
                {
                    if (!CheckPacketLoss(senseRightConfigModel))
                    {
                        ShowMessageBox("ERROR: New right sense config file loaded but not updated to device. Major packet loss will occur due to too much data over bandwidth.  Please fix senseLeft_config.json file and try updating sense to reload changes. Warning: New sense left config file already updated.");
                        UpdateSenseButtonEnabled = true;
                        return;
                    }
                    if (!summitSensing.StopStreaming(theSummitRight, true))
                    {
                        UpdateSenseButtonEnabled = true;
                        return;
                    }
                    if (!summitSensing.StopSensing(theSummitRight, true))
                    {
                        UpdateSenseButtonEnabled = true;
                        return;
                    }
                    if (!summitSensing.SummitConfigureSensing(theSummitRight, senseRightConfigModel, true))
                    {
                        UpdateSenseButtonEnabled = true;
                        return;
                    }
                    if (!summitSensing.StartSensingAndStreaming(theSummitRight, senseRightConfigModel, true))
                    {
                        UpdateSenseButtonEnabled = true;
                        return;
                    }
                }
            }
            if(fftVisualizer != null)
            {
                fftVisualizer.UpdateFFTSettings(senseLeftConfigModel, senseRightConfigModel);
            }

            UpdateSenseButtonEnabled = true;
        }
        /// <summary>
        /// Decrements stim amp
        /// </summary>
        /// <returns>async Task</returns>
        public async Task DecrementStimAmpButton()
        {
            if (String.IsNullOrWhiteSpace(StepAmpValueInputBox) || !Double.TryParse(StepAmpValueInputBox, out double nothing))
            {
                ShowMessageBox("Step amp value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StepAmpValueInputBox) && Double.TryParse(StepAmpValueInputBox, out double numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                numberValueToChangeTo = numberValueToChangeTo * -1.0;
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimAmpStep(theSummitLeft, (byte)ProgramOptionsLeft.IndexOf(SelectedProgramLeft), numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimAmpLeft = valueReturn.Item2 + " mA";
                        stimParameterModelLeft.StimAmp = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: decrement amp", "Change: Amp, | " + stimParameterModelLeft.StimAmp + " | mA");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: decrement amp", "Change: Amp, | " + stimParameterModelLeft.StimAmp + " | mA");
                        }
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimAmpStep(theSummitRight, (byte)ProgramOptionsRight.IndexOf(SelectedProgramRight), numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimAmpRight = valueReturn.Item2 + " mA";
                        stimParameterModelRight.StimAmp = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: decrement amp", "Change: Amp, | " + stimParameterModelRight.StimAmp + " | mA");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: decrement amp", "Change: Amp, | " + stimParameterModelRight.StimAmp + " | mA");
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        /// <summary>
        /// Increments stim amp
        /// </summary>
        /// <returns>async Task</returns>
        public async Task IncrementStimAmpButton()
        {
            if (String.IsNullOrWhiteSpace(StepAmpValueInputBox) || !Double.TryParse(StepAmpValueInputBox, out double nothing))
            {
                ShowMessageBox("Step amp value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StepAmpValueInputBox) && Double.TryParse(StepAmpValueInputBox, out double numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimAmpStep(theSummitLeft, (byte)ProgramOptionsLeft.IndexOf(SelectedProgramLeft), numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimAmpLeft = valueReturn.Item2 + " mA";
                        stimParameterModelLeft.StimAmp = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: increment amp", "Change: Amp, | " + stimParameterModelLeft.StimAmp + " | mA");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: increment amp", "Change: Amp, | " + stimParameterModelLeft.StimAmp + " | mA");
                        }
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimAmpStep(theSummitRight, (byte)ProgramOptionsRight.IndexOf(SelectedProgramRight), numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimAmpRight = valueReturn.Item2 + " mA";
                        stimParameterModelRight.StimAmp = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: decrement amp", "Change: Amp, | " + stimParameterModelRight.StimAmp + " | mA");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: decrement amp", "Change: Amp, | " + stimParameterModelRight.StimAmp + " | mA");
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        /// <summary>
        /// Decrements stim rate
        /// </summary>
        /// <returns>async Task</returns>
        public async Task DecrementStimRateButton()
        {
            if (String.IsNullOrWhiteSpace(StepRateValueInputBox) || !Double.TryParse(StepRateValueInputBox, out double nothing))
            {
                ShowMessageBox("Step rate value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StepRateValueInputBox) && Double.TryParse(StepRateValueInputBox, out double numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                numberValueToChangeTo = numberValueToChangeTo * -1.0;
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimRateStep(theSummitLeft, SenseFriendlyCheckbox, numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimRateLeft = valueReturn.Item2 + " Hz";
                        stimParameterModelLeft.StimRate = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: decrement rate", "Change: Rate, | " + stimParameterModelLeft.StimRate + " | Hz");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: decrement rate", "Change: Rate, | " + stimParameterModelLeft.StimRate + " | Hz");
                        }
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimRateStep(theSummitRight, SenseFriendlyCheckbox, numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimRateRight = valueReturn.Item2 + " Hz";
                        stimParameterModelRight.StimRate = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: decrement rate", "Change: Rate, | " + stimParameterModelRight.StimRate + " | Hz");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: decrement rate", "Change: Rate, | " + stimParameterModelRight.StimRate + " | Hz");
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        /// <summary>
        /// Increments stim pw
        /// </summary>
        /// <returns>async Task</returns>
        public async Task IncrementStimRateButton()
        {
            if (String.IsNullOrWhiteSpace(StepRateValueInputBox) || !Double.TryParse(StepRateValueInputBox, out double nothing))
            {
                ShowMessageBox("Step rate value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StepRateValueInputBox) && Double.TryParse(StepRateValueInputBox, out double numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimRateStep(theSummitLeft, SenseFriendlyCheckbox, numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimRateLeft = valueReturn.Item2 + " Hz";
                        stimParameterModelLeft.StimRate = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: increment rate", "Change: Rate, | " + stimParameterModelLeft.StimRate + " | Hz");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: increment rate", "Change: Rate, | " + stimParameterModelLeft.StimRate + " | Hz");
                        }
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimRateStep(theSummitRight, SenseFriendlyCheckbox, numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimRateRight = valueReturn.Item2 + " Hz";
                        stimParameterModelRight.StimRate = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: increment rate", "Change: Rate, | " + stimParameterModelRight.StimRate + " | Hz");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: increment rate", "Change: Rate, | " + stimParameterModelRight.StimRate + " | Hz");
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        /// <summary>
        /// Decrements stim pw
        /// </summary>
        /// <returns>async Task</returns>
        public async Task DecrementStimPWButton()
        {
            if (String.IsNullOrWhiteSpace(StepPWValueInputBox) || !Int32.TryParse(StepPWValueInputBox, out int nothing))
            {
                ShowMessageBox("Step pulse width value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StepPWValueInputBox) && Int32.TryParse(StepPWValueInputBox, out int numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                numberValueToChangeTo = numberValueToChangeTo * -1;
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, int?, string> valueReturn = await summitStim.ChangeStimPulseWidthStep(theSummitLeft, (byte)ProgramOptionsLeft.IndexOf(SelectedProgramLeft), numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimLeftPWDisplay = valueReturn.Item2 + " µS";
                        stimParameterModelLeft.PulseWidth = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: decrement pulse width", "Change: PulseWidth, | " + stimParameterModelLeft.PulseWidth + " | µS");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: decrement pulse width", "Change: PulseWidth, | " + stimParameterModelLeft.PulseWidth + " | µS");
                        }
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, int?, string> valueReturn = await summitStim.ChangeStimPulseWidthStep(theSummitRight, (byte)ProgramOptionsRight.IndexOf(SelectedProgramRight), numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimRightPWDisplay = valueReturn.Item2 + " µS";
                        stimParameterModelRight.PulseWidth = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: decrement pulse width", "Change: PulseWidth, | " + stimParameterModelRight.PulseWidth + " | µS");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: decrement pulse width", "Change: PulseWidth, | " + stimParameterModelRight.PulseWidth + " | µS");
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        /// <summary>
        /// Increments stim pw
        /// </summary>
        /// <returns>async Task</returns>
        public async Task IncrementStimPWButton()
        {
            if (String.IsNullOrWhiteSpace(StepPWValueInputBox) || !Int32.TryParse(StepPWValueInputBox, out int nothing))
            {
                ShowMessageBox("Step pulse width value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StepPWValueInputBox) && Int32.TryParse(StepPWValueInputBox, out int numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, int?, string> valueReturn = await summitStim.ChangeStimPulseWidthStep(theSummitLeft, (byte)ProgramOptionsLeft.IndexOf(SelectedProgramLeft), numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimLeftPWDisplay = valueReturn.Item2 + " µS";
                        stimParameterModelLeft.PulseWidth = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: increment pulse width", "Change: PulseWidth, | " + stimParameterModelLeft.PulseWidth + " | µS");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: increment pulse width", "Change: PulseWidth, | " + stimParameterModelLeft.PulseWidth + " | µS");
                        }
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Tuple<bool, int?, string> valueReturn = await summitStim.ChangeStimPulseWidthStep(theSummitRight, (byte)ProgramOptionsRight.IndexOf(SelectedProgramRight), numberValueToChangeTo);
                    if (valueReturn.Item1)
                    {
                        StimRightPWDisplay = valueReturn.Item2 + " µS";
                        stimParameterModelRight.PulseWidth = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: increment pulse width", "Change: PulseWidth, | " + stimParameterModelRight.PulseWidth + " | µS");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: increment pulse width", "Change: PulseWidth, | " + stimParameterModelRight.PulseWidth + " | µS");
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        /// <summary>
        /// Sets stim amp to value
        /// </summary>
        /// <returns>async Task</returns>
        public async Task SetStimAmpValueInputButton()
        {
            if (String.IsNullOrWhiteSpace(StimChangeAmpValueInput) || !Double.TryParse(StimChangeAmpValueInput, out double nothing))
            {
                ShowMessageBox("Stim amp value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StimChangeAmpValueInput) && Double.TryParse(StimChangeAmpValueInput, out double numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Double.TryParse(stimParameterModelLeft.StimAmp, out double currentAmpValue);
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimAmpToValue(theSummitLeft, (byte)ProgramOptionsLeft.IndexOf(SelectedProgramLeft), numberValueToChangeTo, currentAmpValue);
                    if (valueReturn.Item1)
                    {
                        StimAmpLeft = valueReturn.Item2 + " mA";
                        stimParameterModelLeft.StimAmp = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: set amp", "Change: Amp, | " + stimParameterModelLeft.StimAmp + " | mA");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: set amp", "Change: Amp, | " + stimParameterModelLeft.StimAmp + " | mA");
                        }
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Double.TryParse(stimParameterModelRight.StimAmp, out double currentAmpValue);
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimAmpToValue(theSummitRight, (byte)ProgramOptionsRight.IndexOf(SelectedProgramRight), numberValueToChangeTo, currentAmpValue);
                    if (valueReturn.Item1)
                    {
                        StimAmpRight = valueReturn.Item2 + " mA";
                        stimParameterModelRight.StimAmp = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: set amp", "Change: Amp, | " + stimParameterModelRight.StimAmp + " | mA");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: set amp", "Change: Amp, | " + stimParameterModelRight.StimAmp + " | mA");
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        /// <summary>
        /// Sets stim rate to value
        /// </summary>
        /// <returns>async Task</returns>
        public async Task SetStimRateValueInputButton()
        {
            if (String.IsNullOrWhiteSpace(StimChangeRateInput) || !Double.TryParse(StimChangeRateInput, out double nothing))
            {
                ShowMessageBox("Stim rate value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StimChangeRateInput) && Double.TryParse(StimChangeRateInput, out double numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Double.TryParse(stimParameterModelLeft.StimRate, out double currentRateValue);
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimRateToValue(theSummitLeft, SenseFriendlyCheckbox, numberValueToChangeTo, currentRateValue);
                    if (valueReturn.Item1)
                    {
                        StimRateLeft = valueReturn.Item2 + " Hz";
                        stimParameterModelLeft.StimRate = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: set rate", "Change: Rate, | " + stimParameterModelLeft.StimRate + " | Hz");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: set rate", "Change: Rate, | " + stimParameterModelLeft.StimRate + " | Hz");
                        }
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Double.TryParse(stimParameterModelRight.StimRate, out double currentRateValue);
                    Tuple<bool, double?, string> valueReturn = await summitStim.ChangeStimRateToValue(theSummitRight, SenseFriendlyCheckbox, numberValueToChangeTo, currentRateValue);
                    if (valueReturn.Item1)
                    {
                        StimRateRight = valueReturn.Item2 + " Hz";
                        stimParameterModelRight.StimRate = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: set rate", "Change: Rate, | " + stimParameterModelRight.StimRate + " | Hz");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: set rate", "Change: Rate, | " + stimParameterModelRight.StimRate + " | Hz");
                        stopWatchChangeTimer.Reset();
                        stopWatchChangeTimer.Start();
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        /// <summary>
        /// Sets stim pulse width to value
        /// </summary>
        /// <returns>async Task</returns>
        public async Task SetStimPWValueInputButton()
        {
            if (String.IsNullOrWhiteSpace(StimChangePWInput) || !Int32.TryParse(StimChangePWInput, out int nothing))
            {
                ShowMessageBox("Step pulse width value missing or incorrect format. Please fix and try again", "Error");
            }
            else if (!String.IsNullOrWhiteSpace(StimChangePWInput) && Int32.TryParse(StimChangePWInput, out int numberValueToChangeTo))
            {
                StimSettingButtonsEnabled = false;
                SummitStim summitStim = new SummitStim(_log);
                if (SelectedDevice.Equals(leftUnilateralDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Int32.TryParse(stimParameterModelLeft.PulseWidth, out int currentPWValue);
                    Tuple<bool, int?, string> valueReturn = await summitStim.ChangeStimPulseWidthToValue(theSummitLeft, (byte)ProgramOptionsLeft.IndexOf(SelectedProgramLeft), numberValueToChangeTo, currentPWValue);
                    if (valueReturn.Item1)
                    {
                        StimLeftPWDisplay = valueReturn.Item2 + " µS";
                        stimParameterModelLeft.PulseWidth = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Left", "Could not create event log for: set pulse width", "Change: PulseWidth, | " + stimParameterModelLeft.PulseWidth + " | µS");
                        if (IsBilateral)
                        {
                            WriteEventLog(theSummitRight, "SCBS Stim: Left", "Could not create event log for: set pulse width", "Change: PulseWidth, | " + stimParameterModelLeft.PulseWidth + " | µS");
                        }
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                if (SelectedDevice.Equals(rightDeviceOption) || SelectedDevice.Equals(bothDeviceOption))
                {
                    Int32.TryParse(stimParameterModelRight.PulseWidth, out int currentPWValue);
                    Tuple<bool, int?, string> valueReturn = await summitStim.ChangeStimPulseWidthToValue(theSummitRight, (byte)ProgramOptionsRight.IndexOf(SelectedProgramRight), numberValueToChangeTo, currentPWValue);
                    if (valueReturn.Item1)
                    {
                        StimRightPWDisplay = valueReturn.Item2 + " µS";
                        stimParameterModelRight.PulseWidth = valueReturn.Item2.ToString();
                        WriteEventLog(theSummitLeft, "SCBS Stim: Right", "Could not create event log for: set pulse width", "Change: PulseWidth, | " + stimParameterModelRight.PulseWidth + " | µS");
                        WriteEventLog(theSummitRight, "SCBS Stim: Right", "Could not create event log for: set pulse width", "Change: PulseWidth, | " + stimParameterModelRight.PulseWidth + " | µS");
                    }
                    else
                    {
                        ShowMessageBox(valueReturn.Item3, "Error");
                    }
                }
                StimSettingButtonsEnabled = true;
            }
        }
        #endregion
        #endregion

        private void AmpChangeTimer(object sender, EventArgs e)
        {
            if (stopWatchChangeTimer.IsRunning)
            {
                TimeSpan ts = stopWatchChangeTimer.Elapsed;
                ChangeTimerText = ts.ToString("hh\\:mm\\:ss");
            }
        }
    }
}
