using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.Commands;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using Medtronic.NeuroStim.Olympus.DataTypes.PowerManagement;
using Medtronic.NeuroStim.Olympus.DataTypes.Therapy;
using Medtronic.SummitAPI.Classes;
using SCBS.Models;
using SCBS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SCBS.ViewModels
{
    /// <summary>
    /// Stim sweep view model allows user to move stim settings on a specific group
    /// </summary>
    public class StimSweepViewModel : Screen
    {
        private readonly string STIMSWEEP_FILEPATH = @"C:\SCBS\Stim_Sweep\stim_sweep_config.json";
        private ILog _log;
        private SummitSystem theSummitLeft, theSummitRight;
        //Views
        private BindableCollection<string> _message = new BindableCollection<string>();
        private int _currentProgress = 0;
        private Visibility _progressVisibility = Visibility.Collapsed;
        private string _progressText = "";
        private string _exitCancelButtonText = "Cancel";
        //Models
        private AppModel appModel = null;
        private StimSweepModel stimSweepModel = null;
        private SenseModel leftSenseModel;
        private SenseModel rightSenseModel;
        private StimSweepGroupStimSettingsModel stimSettingsModelLeft;
        private StimSweepGroupStimSettingsModel stimSettingsModelRight;
        //Services
        private JSONService jSONService;
        private SummitSensing summitSensing;
        //Other
        private bool isBilateral;
        private bool _isStimSweepButtonEnabled = true;
        private string originalLeftGroup, originalRightGroup;
        private volatile int currentIndex = 0;
        private int totalRunsForTitration = 0;
        private uint totalTimeForEntireTitration = 0;
        private uint totalTimeLeftForEntireTitration = 0;
        private int timeIndicatingWhenToWriteEvent = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="theSummitLeft"></param>
        /// <param name="theSummitRight"></param>
        /// <param name="isBilateral"></param>
        /// <param name="leftSenseModel"></param>
        /// <param name="rightSenseModel"></param>
        /// <param name="_log"></param>
        /// <param name="appModel"></param>
        public StimSweepViewModel(SummitSystem theSummitLeft, SummitSystem theSummitRight, bool isBilateral, SenseModel leftSenseModel, SenseModel rightSenseModel, ILog _log, AppModel appModel)
        {
            //if ((theSummitRight == null && isBilateral) || theSummitLeft == null)
            //{
            //    Messages.Insert(0, DateTime.Now + " :: Error occurred in Summit System... Please click Cancel and try again");
            //    IsStimSweepButtonEnabled = false;
            //}
            this.theSummitLeft = theSummitLeft;
            this.theSummitRight = theSummitRight;
            this.isBilateral = isBilateral;
            this._log = _log;
            this.appModel = appModel;
            this.leftSenseModel = leftSenseModel;
            this.rightSenseModel = rightSenseModel;
            jSONService = new JSONService(_log);
            summitSensing = new SummitSensing(_log);
            stimSettingsModelLeft = new StimSweepGroupStimSettingsModel();
            if (isBilateral)
            {
                stimSettingsModelRight = new StimSweepGroupStimSettingsModel();
            }

            //Load stim sweep config file
            stimSweepModel = jSONService.GetStimSweepModelFromFile(STIMSWEEP_FILEPATH);
            if (stimSweepModel == null)
            {
                MessageBox.Show("The stim sweep config file could not be read from the file. Please check that it exists and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Warn("Stim Sweep Config file could not be loaded");
                IsStimSweepButtonEnabled = false;
            }

            try
            {
                //Check to make sure lists in config file are same size. If not then they need to fix and try again.
                if (stimSweepModel.LeftINSOrUnilateral.RateInHz.Count() != stimSweepModel.TimeToRunInMilliSeconds.Count() ||
                    stimSweepModel.LeftINSOrUnilateral.Program.Count() != stimSweepModel.TimeToRunInMilliSeconds.Count() ||
                    stimSweepModel.LeftINSOrUnilateral.AmpInmA.Count() != stimSweepModel.TimeToRunInMilliSeconds.Count() ||
                    stimSweepModel.LeftINSOrUnilateral.PulseWidthInMicroSeconds.Count() != stimSweepModel.TimeToRunInMilliSeconds.Count())
                {
                    Messages.Insert(0, DateTime.Now + ":: Stim Sweep Config arrays are not the same size.  Please fix array sizes in config file and try again.");
                    _log.Warn("Stim Sweep Config arrays are not the same size");
                    IsStimSweepButtonEnabled = false;
                }
                if (isBilateral)
                {
                    if (stimSweepModel.RightINS.RateInHz.Count() != stimSweepModel.TimeToRunInMilliSeconds.Count() ||
                    stimSweepModel.RightINS.Program.Count() != stimSweepModel.TimeToRunInMilliSeconds.Count() ||
                    stimSweepModel.RightINS.AmpInmA.Count() != stimSweepModel.TimeToRunInMilliSeconds.Count() ||
                    stimSweepModel.RightINS.PulseWidthInMicroSeconds.Count() != stimSweepModel.TimeToRunInMilliSeconds.Count())
                    {
                        Messages.Insert(0, DateTime.Now + ":: Stim Sweep Config arrays are not the same size.  Please fix array sizes in config file and try again.");
                        _log.Warn("Stim Sweep Config arrays are not the same size");
                        IsStimSweepButtonEnabled = false;
                    }
                }
            }
            catch (Exception e)
            {
                Messages.Insert(0, DateTime.Now + ":: Error reading config file.  Please fix config file and try again.");
                IsStimSweepButtonEnabled = false;
                _log.Error(e);
            }


            //Patient Instructions
            Messages.Add("\n\nDirections:\nClick the Start button if you would like to run the next iteration of the stim titration.");
            Messages.Add("Please note that your stim therapy will change throughout the process.");
            Messages.Add("You will need to check your settings with the PTM after every stim titration to ensure your settings are correct");
            Messages.Add("Cancel button exits without running the stim titration.\n\n\n");
        }

        #region UI bindings
        /// <summary>
        /// Text for the cancel/exit button.
        /// </summary>
        public string ExitCancelButtonText
        {
            get { return _exitCancelButtonText; }
            set
            {
                _exitCancelButtonText = value;
                NotifyOfPropertyChange(() => ExitCancelButtonText);
            }
        }
        /// <summary>
        /// Enables or disables Start Button
        /// </summary>
        public bool IsStimSweepButtonEnabled
        {
            get { return _isStimSweepButtonEnabled; }
            set
            {
                _isStimSweepButtonEnabled = value;
                NotifyOfPropertyChange(() => IsStimSweepButtonEnabled);
            }
        }

        /// <summary>
        /// Used to display messages to user
        /// </summary>
        public BindableCollection<string> Messages
        {
            get { return _message; }
            set { _message = value; }
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

        #region Button Clicks
        /// <summary>
        /// Stim Sweep button click
        /// </summary>
        public async void StimSweepButtonClick()
        {
            IsStimSweepButtonEnabled = false;
            Messages.Insert(0, DateTime.Now + ":: Running... Please wait until process finishes before closing window.");
            _log.Info("Running Stim Sweep: " + DateTime.Now);
            //Start new task
            Task.Run(() => StimSweepAsync());
        }
        /// <summary>
        /// Cancel button to exit out of window
        /// </summary>
        /// <returns>async task</returns>
        public void CancelButtonClick()
        {
            _log.Info("Exit Button Clicked in Stim Sweep");
            //Change groups to original groups
            if (!String.IsNullOrEmpty(originalLeftGroup))
            {
                if (CheckGroupIsCorrectFormat(originalLeftGroup) && !ChangeActiveGroup(theSummitLeft, ConvertStimModelGroupToAPIGroup(originalLeftGroup), leftSenseModel))
                {
                    MessageBox.Show("Could not change group for left/Unilateral INS to original group. Please exit program and switch back to original group with PTM.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            if (isBilateral)
            {
                if (!String.IsNullOrEmpty(originalRightGroup))
                {
                    if (CheckGroupIsCorrectFormat(originalRightGroup) && !ChangeActiveGroup(theSummitRight, ConvertStimModelGroupToAPIGroup(originalRightGroup), rightSenseModel))
                    {
                        MessageBox.Show("Could not change group for right INS to original group. Please exit program and switch back to original group with PTM.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            TryClose();
        }
        #endregion

        private async Task StimSweepAsync()
        {
            _log.Info("Stim Sweep - Start stim sweep task");
            //Turn on progress bar and let user know stim therapy is about to change
            ProgressVisibility = Visibility.Visible;
            ProgressText = "Configure Settings...";
            try
            {
                currentIndex = stimSweepModel.CurrentIndex;
                totalRunsForTitration = stimSweepModel.TimeToRunInMilliSeconds.Count();
                timeIndicatingWhenToWriteEvent = stimSweepModel.EventMarkerDelayTimeInMilliSeconds;
                totalTimeForEntireTitration = GetTotalTimeForStimSweep(stimSweepModel, currentIndex);
                totalTimeLeftForEntireTitration = GetTotalTimeForStimSweep(stimSweepModel, currentIndex);
            }
            catch(Exception e)
            {
                _log.Error(e);
                ErrorMessageAndLogging("Could not calculate current index and/or total runs and/or EventMarkerTime for stim titration from config file. Please fix try again", "Error", true);
                return;
            }

            //Set original groups
            originalLeftGroup = GetCurrentGroup(theSummitLeft);
            _log.Info("Stim Sweep - Orginal Group Left: " + originalLeftGroup);
            if (!CheckGroupIsCorrectFormat(originalLeftGroup))
            {
                ErrorMessageAndLogging("Could not get original group for left/Unilateral INS. Please try again", "Error", true);
                return;
            }
            if (isBilateral)
            {
                originalRightGroup = GetCurrentGroup(theSummitRight);
                _log.Info("Stim Sweep - Orginal Group Right: " + originalRightGroup);
                if (!CheckGroupIsCorrectFormat(originalRightGroup))
                {
                    ErrorMessageAndLogging("Could not get original group for right INS. Please try again", "Error", true);
                    return;
                }
            }
            _log.Info("Stim Sweep - Finished Saving Original Group");
            //Change groups to specified in config file
            if (CheckGroupIsCorrectFormat(stimSweepModel?.LeftINSOrUnilateral?.GroupToRunStimSweep) && !ChangeActiveGroup(theSummitLeft, ConvertStimModelGroupToAPIGroup(stimSweepModel?.LeftINSOrUnilateral?.GroupToRunStimSweep), leftSenseModel))
            {
                ErrorMessageAndLogging("Could not change group for left/Unilateral INS. Please try again", "Error", true);
                return;
            }
            if (isBilateral)
            {
                if (CheckGroupIsCorrectFormat(stimSweepModel?.RightINS?.GroupToRunStimSweep) && !ChangeActiveGroup(theSummitRight, ConvertStimModelGroupToAPIGroup(stimSweepModel?.RightINS?.GroupToRunStimSweep), rightSenseModel))
                {
                    ErrorMessageAndLogging("Could not change group for right INS. Please try again", "Error", true);
                    return;
                }
            }
            _log.Info("Stim Sweep - Finished Moving groups");
            ProgressText = "Reading Stim Settings";
            //Get all settings from current group
            if (!ReadStimGroup(theSummitLeft, stimSweepModel?.LeftINSOrUnilateral?.GroupToRunStimSweep, true))
            {
                ErrorMessageAndLogging("Could not read stim settings for left/Unilateral side. Please try again", "Error", false);
                return;
            }
            if (isBilateral)
            {
                if (!ReadStimGroup(theSummitRight, stimSweepModel?.RightINS?.GroupToRunStimSweep, false))
                {
                    ErrorMessageAndLogging("Could not read stim settings for right side. Please try again", "Error", true);
                    return;
                }
            }
            _log.Info("Stim Sweep - Finished Read stim group");
            //Turn stim on if not already on
            if (!TurnStimTherapyOn(theSummitLeft))
            {
                ErrorMessageAndLogging("Could not turn stim therapy on left/Unilateral side. Please try again", "Error", true);
                return;
            }
            if (isBilateral)
            {
                if (!TurnStimTherapyOn(theSummitRight))
                {
                    ErrorMessageAndLogging("Could not turn stim therapy on Right side. Please try again", "Error", true);
                    return;
                }
            }
            _log.Info("Stim Sweep - Finished Turn stim on");
            ProgressText = "Changing Stim Therapy...";
            Thread.Sleep(1000);
            //Log that stim titration has begun
            if (!LogEventEntry(theSummitLeft, "Begin Stim Titration", stimSettingsModelLeft, stimSweepModel.LeftINSOrUnilateral.Program[currentIndex]))
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep start.");
            }
            if (isBilateral)
            {
                if (!LogEventEntry(theSummitRight, "Begin Stim Titration", stimSettingsModelRight, stimSweepModel.RightINS.Program[currentIndex]))
                {
                    Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep start.");
                }
            }
            _log.Info("Stim Sweep - Finished Logging start of stim titration");

            //Start stim titration algorithm to run through array of rate/amp/pw/program for amount of time set in config file
            for (/*currentIndex has already been set above*/; currentIndex < totalRunsForTitration; currentIndex++)
            {
                //Set amp/program to current index.
                if (!SetStimAmp(theSummitLeft, stimSweepModel.LeftINSOrUnilateral.AmpInmA[currentIndex], stimSweepModel.LeftINSOrUnilateral.Program[currentIndex], stimSettingsModelLeft, true))
                {
                    ErrorMessageAndLogging("Could not change stim amp for left/Unilateral side. Please cancel and retry stim titration", "Error", false);
                    return;
                }
                if (isBilateral)
                {
                    if (!SetStimAmp(theSummitRight, stimSweepModel.RightINS.AmpInmA[currentIndex], stimSweepModel.RightINS.Program[currentIndex], stimSettingsModelRight, false))
                    {
                        ErrorMessageAndLogging("Could not change stim amp for Right side. Please cancel and retry stim titration", "Error", false);
                        return;
                    }
                }
                //Set pw/program to current index
                if (!SetStimPulseWidth(theSummitLeft, stimSweepModel.LeftINSOrUnilateral.PulseWidthInMicroSeconds[currentIndex], stimSweepModel.LeftINSOrUnilateral.Program[currentIndex], stimSettingsModelLeft, true))
                {
                    ErrorMessageAndLogging("Could not change stim pulse width for left/Unilateral side. Please cancel and retry stim titration", "Error", false);
                    return;
                }
                if (isBilateral)
                {
                    if (!SetStimPulseWidth(theSummitRight, stimSweepModel.RightINS.PulseWidthInMicroSeconds[currentIndex], stimSweepModel.RightINS.Program[currentIndex], stimSettingsModelRight, false))
                    {
                        ErrorMessageAndLogging("Could not change stim pulse width for Right side. Please cancel and retry stim titration", "Error", false);
                        return;
                    }
                }
                //Set rate to current index
                if (!SetStimRate(theSummitLeft, stimSweepModel.LeftINSOrUnilateral.RateInHz[currentIndex], stimSettingsModelLeft, true))
                {
                    ErrorMessageAndLogging("Could not change stim rate for left/Unilateral side. Please cancel and retry stim titration", "Error", false);
                    return;
                }
                if (isBilateral)
                {
                    if (!SetStimRate(theSummitRight, stimSweepModel.RightINS.RateInHz[currentIndex], stimSettingsModelRight, false))
                    {
                        ErrorMessageAndLogging("Could not change stim rate for Right side. Please cancel and retry stim titration", "Error", false);
                        return;
                    }
                }
                Messages.Insert(0, "\nStim Titration Run Number " + (currentIndex+1) + " of " + totalRunsForTitration);
                //Set timer to run for timeToRun amount
                uint? timeToRunForCurrentStimSweepIndex = stimSweepModel?.TimeToRunInMilliSeconds[currentIndex];
                int? timeMarkerToAddEvent = (int)timeToRunForCurrentStimSweepIndex - timeIndicatingWhenToWriteEvent;
                bool startTimeHasRunLeft = false;
                bool stopTimeHasRunLeft = false;
                bool startTimeHasRunRight = false;
                bool stopTimeHasRunRight = false;
                if (timeToRunForCurrentStimSweepIndex == null || timeMarkerToAddEvent == null)
                {
                    ErrorMessageAndLogging("Could not calculate time for stim sweep. Please fix config file and retry stim titration", "Error", false);
                    return;
                }
                while(timeToRunForCurrentStimSweepIndex > 0)
                {
                    //Log the start time in the event log when the time has been passed based on value from configfile
                    if (timeToRunForCurrentStimSweepIndex <= timeMarkerToAddEvent && !startTimeHasRunLeft)
                    {
                        try
                        {
                            //Log that stim titration has begun for left
                            if (!LogEventEntry(theSummitLeft, "Start-StimSweep at index: " + currentIndex, stimSettingsModelLeft, stimSweepModel.LeftINSOrUnilateral.Program[currentIndex]))
                            {
                                Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep start.");
                            }
                            else
                            {
                                startTimeHasRunLeft = true;
                            }
                        }
                        catch (Exception e)
                        {
                            Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep start.");
                            _log.Error(e);
                        }
                    }
                    if (isBilateral)
                    {
                        if (timeToRunForCurrentStimSweepIndex <= timeMarkerToAddEvent && !startTimeHasRunRight)
                        {
                            try
                            {
                                //Log that stim titration has begun
                                if (!LogEventEntry(theSummitRight, "Start-StimSweep at index: " + currentIndex, stimSettingsModelRight, stimSweepModel.RightINS.Program[currentIndex]))
                                {
                                    Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep start.");
                                }
                                else
                                {
                                    startTimeHasRunRight = true;
                                }
                            }
                            catch (Exception e)
                            {
                                Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep start.");
                                _log.Error(e);
                            }
                        }
                    }
                    //Log the end time in the event log when the time has been passed based on value from configfile
                    if (timeToRunForCurrentStimSweepIndex <= timeIndicatingWhenToWriteEvent && !stopTimeHasRunLeft)
                    {
                        try
                        {
                            //Log that stim titration has begun for left
                            if (!LogEventEntry(theSummitLeft, "Stop-StimSweep at index: " + currentIndex, stimSettingsModelLeft, stimSweepModel.LeftINSOrUnilateral.Program[currentIndex]))
                            {
                                Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep stop.");
                            }
                            else
                            {
                                stopTimeHasRunLeft = true;
                            }
                        }
                        catch (Exception e)
                        {
                            Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep stop.");
                            _log.Error(e);
                        }
                    }
                    if (isBilateral)
                    {
                        if (timeToRunForCurrentStimSweepIndex <= timeIndicatingWhenToWriteEvent && !stopTimeHasRunRight)
                        {
                            try
                            {
                                //Log that stim titration has begun for left
                                if (!LogEventEntry(theSummitRight, "Stop-StimSweep at index: " + currentIndex, stimSettingsModelRight, stimSweepModel.RightINS.Program[currentIndex]))
                                {
                                    Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep stop.");
                                }
                                else
                                {
                                    stopTimeHasRunRight = true;
                                }
                            }
                            catch (Exception e)
                            {
                                Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep stop.");
                                _log.Error(e);
                            }
                        }
                    }
                    timeToRunForCurrentStimSweepIndex -= 100;
                    int precentageDoneForTotalStimSweep = 100 - (int)Math.Round((double)(100 * totalTimeLeftForEntireTitration) / totalTimeForEntireTitration);
                    totalTimeLeftForEntireTitration -= 100;
                    CurrentProgress = precentageDoneForTotalStimSweep;
                    TimeSpan time = TimeSpan.FromMilliseconds(totalTimeLeftForEntireTitration);
                    //here backslash is must to tell that colon is
                    //not the part of format, it just a character that we want in output
                    string str = time.ToString(@"hh\:mm\:ss");
                    ProgressText = str + " time left";
                    if(timeToRunForCurrentStimSweepIndex <= 1 && totalTimeLeftForEntireTitration > 1)
                    {
                        ProgressText = "Changing Stim Therapy...";
                    }
                    Thread.Sleep(100);
                } 
            }
            CurrentProgress = 100;
            ProgressText = "Finished Successfully";
            ExitCancelButtonText = "Exit";
            //Doing this for now to get working and debug
            try
            {
                theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Finish Stim Titration : " + DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"), null);
                if (isBilateral)
                {
                    theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Finish Stim Titration : " + DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"), null);

                }
            }catch(Exception e)
            {
                _log.Error(e);
            }
            _log.Info("Stim Sweep - Finished Logging Finish of stim titration");
            //Doesn't execute past this part
            //Log that stim titration has finished
            //if (!LogEventEntry(theSummitLeft, "Finish Stim Titration", stimSettingsModelLeft, stimSweepModel.LeftINSOrUnilateral.Program[currentIndex]))
            //{
            //    Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep finish.");
            //}
            //if (isBilateral)
            //{
            //    if (!LogEventEntry(theSummitRight, "Finish Stim Titration", stimSettingsModelRight, stimSweepModel.RightINS.Program[currentIndex]))
            //    {
            //        Messages.Insert(0, DateTime.Now + ":: Error: Could not log event entry for stim sweep finish.");
            //    }
            //}
        }

        #region Medtronic API Calls
        private bool LogEventEntry(SummitSystem localSummit, string eventEntry, StimSweepGroupStimSettingsModel localGroupSettingModel, int program)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Summit null or disposed.");
                return false;
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                int counter = 5;
                do
                {
                    bufferReturnInfo = localSummit.LogCustomEvent(DateTime.Now, DateTime.Now, eventEntry + " : " + DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"), "Stim amp Program (0,1,2,3): " + localGroupSettingModel.StimAmpProgram0.ToString("0.0") + "," + localGroupSettingModel.StimAmpProgram1.ToString("0.0") + "," +localGroupSettingModel.StimAmpProgram2.ToString("0.0") + "," + localGroupSettingModel.StimAmpProgram3.ToString("0.0") + "mA.Pulse Width Program (0,1,2,3): " + localGroupSettingModel.PulseWidthProgram0 + "," + localGroupSettingModel.PulseWidthProgram1 + "," + localGroupSettingModel.PulseWidthProgram2 + "," + localGroupSettingModel.PulseWidthProgram3 + "μs. Stim Rate: " + localGroupSettingModel.StimRate.ToString("0.0") + "Hz. " + " Program: " + program);
                    if (counter < 5)
                    {
                        Thread.Sleep(400);
                    }
                    counter--;
                } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                {
                    _log.Warn(":: Error: Medtronic API return error logging " + eventEntry + ": " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                    return false;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }
            return true;
        }
        private bool ReadStimGroup(SummitSystem localSummit, string activeGroup, bool isLeft)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                return false;
            }
            TherapyGroup insStateGroup = null;
            APIReturnInfo bufferReturnInfo = new APIReturnInfo();
            int counter = 5;
            try
            {
                if (CheckGroupIsCorrectFormat(activeGroup))
                {
                    do
                    {
                        switch (activeGroup)
                        {
                            case "A":
                                bufferReturnInfo = localSummit.ReadStimGroup(GroupNumber.Group0, out insStateGroup);
                                break;
                            case "B":
                                bufferReturnInfo = localSummit.ReadStimGroup(GroupNumber.Group1, out insStateGroup);
                                break;
                            case "C":
                                bufferReturnInfo = localSummit.ReadStimGroup(GroupNumber.Group2, out insStateGroup);
                                break;
                            case "D":
                                bufferReturnInfo = localSummit.ReadStimGroup(GroupNumber.Group3, out insStateGroup);
                                break;
                        }
                        if (counter < 5)
                        {
                            Thread.Sleep(400);
                        }
                        counter--;
                    } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                    if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                    {
                        Messages.Insert(0, DateTime.Now + ":: Error: Medtronic API return error reading stim group: " + bufferReturnInfo.Descriptor);
                        _log.Warn(":: Error: Medtronic API return error: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Reading Stim Group...  Values were not able to be retreived from the device.");
                _log.Error(e);
                return false;
            }
            try
            {
                if (insStateGroup != null)
                {
                    if (isLeft)
                    {
                        stimSettingsModelLeft.StimRate = insStateGroup.RateInHz;
                        stimSettingsModelLeft.StimAmpProgram0 = insStateGroup.Programs[0].AmplitudeInMilliamps;
                        stimSettingsModelLeft.StimAmpProgram1 = insStateGroup.Programs[1].AmplitudeInMilliamps;
                        stimSettingsModelLeft.StimAmpProgram2 = insStateGroup.Programs[2].AmplitudeInMilliamps;
                        stimSettingsModelLeft.StimAmpProgram3 = insStateGroup.Programs[3].AmplitudeInMilliamps;
                        stimSettingsModelLeft.PulseWidthProgram0 = insStateGroup.Programs[0].PulseWidthInMicroseconds;
                        stimSettingsModelLeft.PulseWidthProgram1 = insStateGroup.Programs[1].PulseWidthInMicroseconds;
                        stimSettingsModelLeft.PulseWidthProgram2 = insStateGroup.Programs[2].PulseWidthInMicroseconds;
                        stimSettingsModelLeft.PulseWidthProgram3 = insStateGroup.Programs[3].PulseWidthInMicroseconds;
                    }
                    else
                    {
                        stimSettingsModelRight.StimRate = insStateGroup.RateInHz;
                        stimSettingsModelRight.StimAmpProgram0 = insStateGroup.Programs[0].AmplitudeInMilliamps;
                        stimSettingsModelRight.StimAmpProgram1 = insStateGroup.Programs[1].AmplitudeInMilliamps;
                        stimSettingsModelRight.StimAmpProgram2 = insStateGroup.Programs[2].AmplitudeInMilliamps;
                        stimSettingsModelRight.StimAmpProgram3 = insStateGroup.Programs[3].AmplitudeInMilliamps;
                        stimSettingsModelRight.PulseWidthProgram0 = insStateGroup.Programs[0].PulseWidthInMicroseconds;
                        stimSettingsModelRight.PulseWidthProgram1 = insStateGroup.Programs[1].PulseWidthInMicroseconds;
                        stimSettingsModelRight.PulseWidthProgram2 = insStateGroup.Programs[2].PulseWidthInMicroseconds;
                        stimSettingsModelRight.PulseWidthProgram3 = insStateGroup.Programs[3].PulseWidthInMicroseconds;
                    }
                }
                else
                {
                    Messages.Insert(0, DateTime.Now + ":: Error: Please check that values are correct on this run.  Values were not able to be retreived and verified from the device.");
                    return false;
                }
            }
            catch (Exception e)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Could not read values returned by API.");
                _log.Error(e);
                return false;
            }
            return true;
        }
        private bool SetStimAmp(SummitSystem localSummit, double ampValueToSet, int program, StimSweepGroupStimSettingsModel localSettingsModel, bool isLeft)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Summit null or disposed.");
                return false;
            }
            bool success = false;
            double deltaChange;
            double? ampOutputFromDevice;
            double currentStimAmpOnDevice;
            APIReturnInfo bufferReturnInfo;

            //Find settings based on program
            switch (program)
            {
                case 0:
                    currentStimAmpOnDevice = localSettingsModel.StimAmpProgram0;
                    break;
                case 1:
                    currentStimAmpOnDevice = localSettingsModel.StimAmpProgram1;
                    break;
                case 2:
                    currentStimAmpOnDevice = localSettingsModel.StimAmpProgram2;
                    break;
                case 3:
                    currentStimAmpOnDevice = localSettingsModel.StimAmpProgram3;
                    break;
                default:
                    Messages.Insert(0, DateTime.Now + ":: Error: Could not read amp settings for current program.");
                    return false;
            }
            try
            {
                deltaChange = Math.Round(ampValueToSet + (-1 * currentStimAmpOnDevice), 1);
                if (deltaChange != 0)
                {
                    int counter = 5;
                    do
                    {
                        bufferReturnInfo = localSummit.StimChangeStepAmp((byte) program, deltaChange, out ampOutputFromDevice);
                        if (counter < 5)
                        {
                            Thread.Sleep(400);
                        }
                        counter--;
                    } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                    if ((bufferReturnInfo.RejectCode != 0) || counter == 0 || ampOutputFromDevice == null)
                    {
                        Messages.Insert(0, DateTime.Now + ":: Error: Medtronic API return error setting stim amp: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        _log.Warn(":: Error: Medtronic API return error setting stim amp: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        return false;
                    }
                    //Set output level from device
                    if (isLeft)
                    {
                        switch (program)
                        {
                            case 0:
                                stimSettingsModelLeft.StimAmpProgram0 = ampOutputFromDevice.Value;
                                break;
                            case 1:
                                stimSettingsModelLeft.StimAmpProgram1 = ampOutputFromDevice.Value;
                                break;
                            case 2:
                                stimSettingsModelLeft.StimAmpProgram2 = ampOutputFromDevice.Value;
                                break;
                            case 3:
                                stimSettingsModelLeft.StimAmpProgram3 = ampOutputFromDevice.Value;
                                break;
                        }
                        Messages.Insert(0, DateTime.Now + ":: Left/Unilateral stim amp: " + ampOutputFromDevice.Value + " Program: " + program);
                    }
                    else
                    {
                        switch (program)
                        {
                            case 0:
                                stimSettingsModelRight.StimAmpProgram0 = ampOutputFromDevice.Value;
                                break;
                            case 1:
                                stimSettingsModelRight.StimAmpProgram1 = ampOutputFromDevice.Value;
                                break;
                            case 2:
                                stimSettingsModelRight.StimAmpProgram2 = ampOutputFromDevice.Value;
                                break;
                            case 3:
                                stimSettingsModelRight.StimAmpProgram3 = ampOutputFromDevice.Value;
                                break;
                        }
                        Messages.Insert(0, DateTime.Now + ":: Right stim amp: " + ampOutputFromDevice.Value + " Program: " + program);
                    }
                }
                else
                {
                    Messages.Insert(0, DateTime.Now + ":: Stim amp: No change");
                }
                success = true;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Insert(0, DateTime.Now + ":: Error: Could not set stim amp.");
            }
            return success;
        }

        private bool SetStimPulseWidth(SummitSystem localSummit, int pwValueToSet, int program, StimSweepGroupStimSettingsModel localSettingsModel, bool isLeft)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Summit null or disposed.");
                return false;
            }
            bool success = false;
            int deltaChange;
            int? pwOutputFromDevice;
            int currentStimPWOnDevice;
            APIReturnInfo bufferReturnInfo;

            //Find settings based on program
            switch (program)
            {
                case 0:
                    currentStimPWOnDevice = localSettingsModel.PulseWidthProgram0;
                    break;
                case 1:
                    currentStimPWOnDevice = localSettingsModel.PulseWidthProgram1;
                    break;
                case 2:
                    currentStimPWOnDevice = localSettingsModel.PulseWidthProgram2;
                    break;
                case 3:
                    currentStimPWOnDevice = localSettingsModel.PulseWidthProgram3;
                    break;
                default:
                    Messages.Insert(0, DateTime.Now + ":: Error: Could not read pw settings for current program.");
                    return false;
            }
            try
            {
                deltaChange = pwValueToSet + (-1 * currentStimPWOnDevice);
                if (deltaChange != 0)
                {
                    int counter = 5;
                    do
                    {
                        bufferReturnInfo = localSummit.StimChangeStepPW((byte)program, deltaChange, out pwOutputFromDevice);
                        if (counter < 5)
                        {
                            Thread.Sleep(400);
                        }
                        counter--;
                    } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                    if ((bufferReturnInfo.RejectCode != 0) || counter == 0 || pwOutputFromDevice == null)
                    {
                        Messages.Insert(0, DateTime.Now + ":: Error: Medtronic API return error setting stim pulse width: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        _log.Warn(":: Error: Medtronic API return error setting stim pulse width: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        return false;
                    }
                    //Set output level from device
                    if (isLeft)
                    {
                        switch (program)
                        {
                            case 0:
                                stimSettingsModelLeft.PulseWidthProgram0 = pwOutputFromDevice.Value;
                                break;
                            case 1:
                                stimSettingsModelLeft.PulseWidthProgram1 = pwOutputFromDevice.Value;
                                break;
                            case 2:
                                stimSettingsModelLeft.PulseWidthProgram2 = pwOutputFromDevice.Value;
                                break;
                            case 3:
                                stimSettingsModelLeft.PulseWidthProgram3 = pwOutputFromDevice.Value;
                                break;
                        }
                        Messages.Insert(0, DateTime.Now + ":: Left/Unilateral Stim Pulse Width: " + pwOutputFromDevice.Value + " Program: " + program);
                    }
                    else
                    {
                        switch (program)
                        {
                            case 0:
                                stimSettingsModelRight.PulseWidthProgram0 = pwOutputFromDevice.Value;
                                break;
                            case 1:
                                stimSettingsModelRight.PulseWidthProgram1 = pwOutputFromDevice.Value;
                                break;
                            case 2:
                                stimSettingsModelRight.PulseWidthProgram2 = pwOutputFromDevice.Value;
                                break;
                            case 3:
                                stimSettingsModelRight.PulseWidthProgram3 = pwOutputFromDevice.Value;
                                break;
                        }
                        Messages.Insert(0, DateTime.Now + ":: Right Stim Pulse Width: " + pwOutputFromDevice.Value + " Program: " + program);
                    }
                }
                else
                {
                    Messages.Insert(0, DateTime.Now + ":: Stim Pulse Width: No change");
                }
                success = true;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Insert(0, DateTime.Now + ":: Error: Could not set stim Pulse Width.");
            }
            return success;
        }

        private bool SetStimRate(SummitSystem localSummit, double rateValueToSet, StimSweepGroupStimSettingsModel localSettingsModel, bool isLeft)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Summit null or disposed.");
                return false;
            }
            bool success = false;
            double deltaChange;
            double? rateOutputFromDevice;
            double currentStimRateOnDevice;
            APIReturnInfo bufferReturnInfo;

            //Find setting
            currentStimRateOnDevice = localSettingsModel.StimRate;
            try
            {
                deltaChange = Math.Round(rateValueToSet + (-1 * currentStimRateOnDevice), 1);
                if (deltaChange != 0)
                {
                    int counter = 5;
                    do
                    {
                        bufferReturnInfo = localSummit.StimChangeStepFrequency(deltaChange, true, out rateOutputFromDevice);
                        if (counter < 5)
                        {
                            Thread.Sleep(400);
                        }
                        counter--;
                    } while ((bufferReturnInfo.RejectCode != 0) && (bufferReturnInfo.RejectCode != 5) && counter > 0);
                    //If reject code of 5, then there is no change since there is no sense friendly value to change to from previous value
                    if(bufferReturnInfo.RejectCode == 5)
                    {
                        Messages.Insert(0, DateTime.Now + ":: Stim rate: No change (no valid sense friendly rate from previous value)");
                        return true;
                    }
                    if ((bufferReturnInfo.RejectCode != 0) || counter == 0 || rateOutputFromDevice == null)
                    {
                        Messages.Insert(0, DateTime.Now + ":: Error: Medtronic API return error setting stim rate: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        _log.Warn(":: Error: Medtronic API return error setting stim rate: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        return false;
                    }
                    //Set output level from device
                    if (isLeft)
                    {
                        stimSettingsModelLeft.StimRate = rateOutputFromDevice.Value;
                        Messages.Insert(0, DateTime.Now + ":: Left/Unilateral Stim rate: " + rateOutputFromDevice.Value);
                    }
                    else
                    {
                        stimSettingsModelRight.StimRate = rateOutputFromDevice.Value;
                        Messages.Insert(0, DateTime.Now + ":: Right Stim rate: " + rateOutputFromDevice.Value);
                    }
                }
                else
                {
                    Messages.Insert(0, DateTime.Now + ":: Stim rate: No change");
                }
                success = true;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Insert(0, DateTime.Now + ":: Error: Could not set stim rate.");
            }
            return success;
        }
        private bool TurnStimTherapyOn(SummitSystem localSummit)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Summit null or disposed.");
                return false;
            }
            APIReturnInfo bufferReturnInfo;
            //Turn stim therapy on. If it is already on, we will get a code of 8225 instead of 0. Keep 0 in case it successfully turn stim on.
            try
            {
                int counter = 5;
                do
                {
                    bufferReturnInfo = localSummit.StimChangeTherapyOn();
                    if (counter < 5)
                    {
                        Thread.Sleep(400);
                    }
                    // Reset POR if set
                    if (bufferReturnInfo.RejectCodeType == typeof(MasterRejectCode)
                        && (MasterRejectCode)bufferReturnInfo.RejectCode == MasterRejectCode.ChangeTherapyPor)
                    {
                        ResetPOR(localSummit);
                        _log.Info("Turn stim therapy on after resetPOR success in update DBS button click");
                        continue;
                    }
                    counter--;
                } while ((bufferReturnInfo.RejectCode != 0) && (bufferReturnInfo.RejectCode != 8225) && counter > 0);
                if ((bufferReturnInfo.RejectCode != 0) && (bufferReturnInfo.RejectCode != 8225) && counter == 0)
                {
                    _log.Warn(":: Error: Medtronic API return error turning stim amp on: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                    return false;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }
            return true;
        }
        private bool ChangeActiveGroup(SummitSystem localSummit, ActiveGroup groupToChangeTo, SenseModel localSenseModel)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Summit null or disposed.");
                return false;
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
                    return false;
                }
                //Start streaming
                summitSensing.StartStreaming(localSummit, localSenseModel, true);
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the current group from the INS
        /// </summary>
        /// <param name="localSummit">summit system</param>
        /// <returns>A single character for the group.</returns>
        private string GetCurrentGroup(SummitSystem localSummit)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                Messages.Insert(0, DateTime.Now + ":: Error: Summit null or disposed.");
                return null;
            }
            SummitStimulationInfo summitStimulationInfo = new SummitStimulationInfo(_log);
            string currentGroup;
            int counter = 5;
            do
            {
                currentGroup = summitStimulationInfo.GetActiveGroup(ref localSummit);
                if (counter < 5)
                {
                    Thread.Sleep(400);
                }
                counter--;
                _log.Info("Current group in stim sweep: " + currentGroup);
            } while (String.IsNullOrEmpty(currentGroup) && counter > 0);
            //remove the Group text before the acutal group
            return currentGroup.Remove(0, 6);
        }

        /// <summary>
        /// Resets the POR bit if it was set
        /// </summary>
        /// <param name="localSummit">SummitSystem for the api call</param>
        private void ResetPOR(SummitSystem localSummit)
        {
            APIReturnInfo bufferReturnInfo;
            _log.Info("POR was set, resetting...");
            Messages.Insert(0, DateTime.Now + ":: -POR was set, resetting...");
            try
            {
                // reset POR
                bufferReturnInfo = localSummit.ResetErrorFlags(Medtronic.NeuroStim.Olympus.DataTypes.Core.StatusBits.Por);
                if (!CheckForReturnError(bufferReturnInfo, "Reset POR", _log))
                {
                    return;
                }

                // check battery
                BatteryStatusResult theStatus;
                localSummit.ReadBatteryLevel(out theStatus);
                if (!CheckForReturnError(bufferReturnInfo, "Checking Battery Level for Reset POR", _log))
                {
                    return;
                }
                // perform interrogate command and check if therapy is enabled.s
                GeneralInterrogateData interrogateBuffer;
                localSummit.ReadGeneralInfo(out interrogateBuffer);
                if (interrogateBuffer.IsTherapyUnavailable)
                {
                    _log.Warn("Therapy still unavailable after POR reset");
                    return;
                }
            }
            catch (Exception e)
            {
                Messages.Insert(0, DateTime.Now + ":: --ERROR: Reset POR bit");
                _log.Error(e);
            }
        }
        #endregion

        #region Helper Methods
        private uint GetTotalTimeForStimSweep(StimSweepModel localModel, int indexFromCurentIndexOfConfig)
        {
            if(localModel == null || localModel.TimeToRunInMilliSeconds == null)
            {
                return 0;
            }
            if(indexFromCurentIndexOfConfig >= localModel.TimeToRunInMilliSeconds.Count())
            {
                return 0;
            }
            uint totalCount = 0;
            for(int i = indexFromCurentIndexOfConfig; i < localModel.TimeToRunInMilliSeconds.Count(); i++)
            {
                totalCount += localModel.TimeToRunInMilliSeconds[i];
            }
            return totalCount;
        }
        private bool CheckGroupIsCorrectFormat(string group)
        {
            if (String.IsNullOrEmpty(group))
            {
                return false;
            }
            return group == "A" || group == "B" || group == "C" || group == "D";
        }
        private ActiveGroup ConvertStimModelGroupToAPIGroup(string groupValue)
        {
            switch (groupValue)
            {
                case "A":
                    return ActiveGroup.Group0;
                case "B":
                    return ActiveGroup.Group1;
                case "C":
                    return ActiveGroup.Group2;
                case "D":
                    return ActiveGroup.Group3;
            }
            return ActiveGroup.Group0;
        }
        #endregion

        /// <summary>
        /// Checks for return error code from APIReturnInfo from Medtronic
        /// If there is an error, the method calls error handling method SetEmbeddedOffGroupAStimOnWhenErrorOccurs() to turn embedded off, change to group A and turn Stim ON
        /// The Error location and error descriptor from the returned API call are displayed to user in a message box.
        /// </summary>
        /// <param name="info">The APIReturnInfo value returned from the Medtronic API call</param>
        /// <param name="errorLocation">The location where the error is being check. Can be turning stim on, changing group, etc</param>
        /// <param name="log">caliburn micro log</param>
        /// <returns>True if there was an error or false if no error</returns>
        private bool CheckForReturnError(APIReturnInfo info, string errorLocation, ILog log)
        {
            if (info.RejectCode != 0)
            {
                log.Warn("Reject code: " + info.RejectCode + ". Reject description: " + info.Descriptor + ". Location: " + errorLocation);
                return false;
            }
            return true;
        }
        private void ErrorMessageAndLogging(string message, string title, bool enableStartButton)
        {
            try
            {
                Messages.Insert(0, "\n" +  title + ": " + message + "\n\n");
            }
            catch (Exception e)
            {
                _log.Warn("ErrorMessageAndLogging crashed while trying to let user know about error in stim sweep");
                _log.Error(e);
            }

            _log.Warn(message);
            IsStimSweepButtonEnabled = enableStartButton;
            if (!enableStartButton)
            {
                ProgressText = "Please Try Again";
            }
        }
        private void ErrorMessageAndLogging(string message, string title, Exception e, bool enableStartButton)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            _log.Error(e);
            IsStimSweepButtonEnabled = enableStartButton;
        }
    }
}
