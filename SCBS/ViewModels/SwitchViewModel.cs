using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.Commands;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using Medtronic.NeuroStim.Olympus.DataTypes.PowerManagement;
using Medtronic.NeuroStim.Olympus.DataTypes.Sensing;
using Medtronic.NeuroStim.Olympus.DataTypes.Therapy.Adaptive;
using Medtronic.SummitAPI.Classes;
using Medtronic.SummitAPI.Flash;
using SCBS.Models;
using SCBS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SCBS.ViewModels
{
    class SwitchViewModel : Screen
    {
        private readonly string switchLeftDefaultFileLocation = @"C:\SCBS\switch\left_default\switch_left_default.json";
        private readonly string switchRightFileLocation = @"C:\SCBS\switch\right\switch_right.json";
        private string leftPrefixFileLocation = @"C:\SCBS\switch\left_default\";
        private string rightPrefixFileLocation = @"C:\SCBS\switch\right\";
        private static readonly string senseLeftFileLocation = @"C:\SCBS\senseLeft_config.json";
        private static readonly string senseRightFileLocation = @"C:\SCBS\senseRight_config.json";
        private readonly string JSON_EXTENSION = ".json";
        private ILog _log;
        private JSONService jSONService;
        //This message collection is used to show messages to user. Message boxes appear in Main tab and Report tab
        private BindableCollection<string> _message = new BindableCollection<string>();
        private SummitSystem theSummitLeft, theSummitRight;
        private MasterSwitchModel masterSwitchModelLeftDefault = null;
        private MasterSwitchModel masterSwitchModelRight = null;
        private AppModel appModel = null;
        private static Thread workerThreadLeftDefault;
        private static Thread workerThreadRight;
        private static Thread finishingThread;
        private bool isBilateral;
        private bool _isSwitchEnabled = true;
        private int previousIndexLeft, previousIndexRight;
        //lower/upper BinActualValues are for storing the actual values from the power
        //User adds in the estimated values in config file and actual values are calculated based on these values
        //The actual values are stored in these arrays to display to user in Visualization Tab.  Implementation for storing in MainPageViewModel.cs.
        private double[] lowerPowerBinActualValues = new double[8];
        private double[] upperPowerBinActualValues = new double[8];
        private bool leftSideFinished = false;
        private bool rightSideFinished = false;
        private AdaptiveModel leftAdaptiveModel, rightAdaptiveModel;
        private SenseModel leftSenseModel, rightSenseModel;
        private string projectID;
        private volatile bool _stopBothThreads = false;
        private volatile bool _cancelButtonPressed = false;
        private string leftPatientID, rightPatientID, leftDeviceID, rightDeviceID;

        public SwitchViewModel(ref SummitSystem theSummitLeft, ref SummitSystem theSummitRight, bool isBilateral, string projectID, ILog _log, AppModel appModel)
        {
            if((theSummitRight == null && isBilateral) || theSummitLeft == null)
            {
                Messages.Add("Error occurred in Summit System... Please click Cancel");
                IsSwitchEnabled = false;
            }
            this.theSummitLeft = theSummitLeft;
            this.theSummitRight = theSummitRight;
            this.isBilateral = isBilateral;
            this.projectID = projectID;
            this._log = _log;
            this.appModel = appModel;

            //Load master config files and check to make sure they aren't null
            jSONService = new JSONService(_log);
            masterSwitchModelLeftDefault = jSONService?.GetMasterSwitchModelFromFile(switchLeftDefaultFileLocation);
            if(masterSwitchModelLeftDefault == null)
            {
                Messages.Add("Error occurred in loading " + switchLeftDefaultFileLocation + "... Please click Cancel");
                IsSwitchEnabled = false;
            }
            if (isBilateral)
            {
                masterSwitchModelRight = jSONService?.GetMasterSwitchModelFromFile(switchRightFileLocation);
                if (masterSwitchModelRight == null)
                {
                    Messages.Add("Error occurred in loading " + switchRightFileLocation + "... Please click Cancel");
                    IsSwitchEnabled = false;
                }
            }
            //Check to make sure if the WaitTimeIsEnabled then to check if the min amount of time has passed before running switch again.
            if (masterSwitchModelLeftDefault != null)
            {
                try
                {
                    if (masterSwitchModelLeftDefault.WaitTimeIsEnabled)
                    {
                        var start = DateTime.Parse(masterSwitchModelLeftDefault.DateTimeLastSwitch);
                        if (start.AddMinutes(masterSwitchModelLeftDefault.WaitTimeInMinutes) > DateTime.UtcNow)
                        {
                            IsSwitchEnabled = false;
                            DisplayMessageBox("You have not reached the minimum time until the next run. Please try again later.");
                            Messages.Add("You have not reached the minimum time until the next run. Please try again later.");
                            _log.Info("User tried to run switch before minimum time reached on left");
                            return;
                        }
                    }
                    if (isBilateral)
                    {
                        if(masterSwitchModelRight != null)
                        {
                            if (masterSwitchModelRight.WaitTimeIsEnabled)
                            {
                                var start = DateTime.Parse(masterSwitchModelRight.DateTimeLastSwitch);
                                if (start.AddMinutes(masterSwitchModelRight.WaitTimeInMinutes) > DateTime.UtcNow)
                                {
                                    IsSwitchEnabled = false;
                                    DisplayMessageBox("You have not reached the minimum time until the next run. Please try again later.");
                                    Messages.Add("You have not reached the minimum time until the next run. Please try again later.");
                                    _log.Info("User tried to run switch before minimum time reached on right");
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Messages.Add("An Error has occurred. Please run switch again.");
                    IsSwitchEnabled = false;
                    _log.Error(e);
                }
            }
            Messages.Add("Click the Switch button if you would like to run the next iteration of adaptive/sham therapy.");
            Messages.Add("Please note that your therapy will turn off while it sets up.");
            Messages.Add("Press the Cancel button to exit without running the next iteration");
        }

        /// <summary>
        /// Enables or disables Switch Button
        /// </summary>
        public bool IsSwitchEnabled
        {
            get { return _isSwitchEnabled; }
            set
            {
                _isSwitchEnabled = value;
                NotifyOfPropertyChange(() => IsSwitchEnabled);
            }
        }

        /// <summary>
        /// Switch button click
        /// </summary>
        /// <returns>Async Task</returns>
        public async Task SwitchButtonClickAsync()
        {
            IsSwitchEnabled = false;
            Messages.Add("Running... Please wait until process finishes before closing window.");
            _log.Info("Running Switch: " + DateTime.Now);
            await GetPatientID();
            await GetDeviceID();

            //If bilateral is true, check left INS doesn't end with R, if it does then switch L and R summit so that the correct INS gets updated.
            if (isBilateral)
            {
                //Checks to see if the INS' were mixed up. Don't want to run a switch on wrong INS with stim inteded for different INS
                if (leftPatientID[leftPatientID.Length - 1].Equals('R'))
                {
                    _log.Info("Bilateral: INS in incorrect order. Switching order");
                    SummitSystem tempSummit = theSummitLeft;
                    theSummitLeft = theSummitRight;
                    theSummitRight = tempSummit;
                }
                else if (!leftPatientID[leftPatientID.Length - 1].Equals('L'))
                {
                    _log.Warn("Bilateral: INS in incorrect order. Cannot switch order. Need to check that INS names are correct.");
                    Messages.Add("Error occurred finding INS name ending in either L or R. Please fix with RLP and name INS ending in either L or R... Closing window");
                    Thread.Sleep(5000);
                    TryClose();
                    return;
                }
            }
            
            //Start new thread
            workerThreadLeftDefault = new Thread(new ThreadStart(WorkerThreadLeftDefault));
            workerThreadLeftDefault.IsBackground = true;
            workerThreadLeftDefault.Name = "workerThreadLeftDefault";
            workerThreadLeftDefault.Start();
            if (isBilateral)
            {
                workerThreadRight = new Thread(new ThreadStart(WorkerThreadRight));
                workerThreadRight.IsBackground = true;
                workerThreadRight.Name = "workerThreadRight";
                workerThreadRight.Start();
            }
            finishingThread = new Thread(new ThreadStart(FinishingThreadCode));
            finishingThread.IsBackground = true;
            finishingThread.Name = "FinishingThread";
            finishingThread.Start();
        }

        /// <summary>
        /// Gets the patient ID from the API
        /// </summary>
        /// <returns>async Task</returns>
        private async Task GetPatientID()
        {
            Messages.Add("Retrieving Patient ID...");
            _log.Info("Retrieving Patient ID");
            int countdown = 10;
            while ((((leftPatientID == null || rightPatientID == null) && isBilateral) || (leftPatientID == null  && !isBilateral)) && countdown >= 0)
            {
                if(leftPatientID == null)
                {
                    try
                    {
                        SubjectInfo subjectInfo = new SubjectInfo();
                        APIReturnInfo bufferReturnInfo;
                        await Task.Run(() => bufferReturnInfo = theSummitLeft.FlashReadSubjectInfo(out subjectInfo));
                        leftPatientID = subjectInfo.ID;
                        _log.Info("Patient ID Left: " + leftPatientID);
                    }
                    catch (Exception e)
                    {
                        //do nothing. Just keep looping until we actually get the patient id
                        _log.Error(e);
                    }
                }
                if(rightPatientID == null && isBilateral)
                {
                    try
                    {
                        SubjectInfo subjectInfo = new SubjectInfo();
                        APIReturnInfo bufferReturnInfo;
                        await Task.Run(() => bufferReturnInfo = theSummitRight.FlashReadSubjectInfo(out subjectInfo));
                        rightPatientID = subjectInfo.ID;
                        _log.Info("Patient ID Right: " + rightPatientID);
                    }
                    catch (Exception e)
                    {
                        //do nothing. Just keep looping until we actually get the patient id
                        _log.Error(e);
                    }
                }
                countdown--;
                Messages.Add("...");
            }
            if (leftPatientID == null || (rightPatientID == null && isBilateral))
            {
                _log.Warn("Error retrieving Patient ID");
                Messages.Add("Error occurred getting patientID. Window closing... Please try Switch again");
                Thread.Sleep(5000);
                TryClose();
                return;
            }
            _log.Info("Retrieved Patient ID");
        }

        /// <summary>
        /// Gets the Device ID from the API
        /// </summary>
        /// <returns>async Task</returns>
        private async Task GetDeviceID()
        {
            _log.Info("Getting Device ID");
            int countdown = 10;
            while((leftDeviceID == null || (rightDeviceID == null && isBilateral)) && countdown >= 0)
            {
                if (leftDeviceID == null)
                {
                    try
                    {
                        await Task.Run(() => leftDeviceID = theSummitLeft.DeviceID);
                        _log.Info("Left Device ID: " + leftDeviceID);
                    }
                    catch (Exception e)
                    {
                        //do nothing until I have device ID
                        _log.Error(e);
                    }

                }
                if (rightDeviceID == null && isBilateral)
                {
                    try
                    {
                        await Task.Run(() => rightDeviceID = theSummitRight.DeviceID);
                        _log.Info("Right Device ID: " + rightDeviceID);
                    }
                    catch (Exception e)
                    {
                        //do nothing until I have device ID
                        _log.Error(e);
                    }

                }
                countdown--;
            }
            _log.Info("Getting Device ID");
        }

        /// <summary>
        /// Worker thread that runs the Switch for the left/default side
        /// </summary>
        private void WorkerThreadLeftDefault()
        {
            APIReturnInfo bufferReturnInfo;
            _log.Info("Starting Default Side Update");
            
            //Get the filename from the config list at the current index
            string filename;
            try
            {
                filename = masterSwitchModelLeftDefault.ConfigNames[masterSwitchModelLeftDefault.CurrentIndex];
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred reading current filename in left/default config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            if (!String.IsNullOrEmpty(filename))
            {
                //load adaptive config file at currentIndex
                string filePath = leftPrefixFileLocation + filename + JSON_EXTENSION;
                leftAdaptiveModel = jSONService?.GetAdaptiveModelFromFile(filePath);
            }
            else
            {
                _log.Warn("Error occurred reading current filename for config file");
                Messages.Add("Error occurred reading current filename for config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            if (leftAdaptiveModel == null)
            {
                _log.Warn("Error occurred loading adaptive config file");
                Messages.Add("Error occurred loading adaptive config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }
            
            //Load sense config file
            leftSenseModel = jSONService?.GetSenseModelFromFile(senseLeftFileLocation);
            if(leftSenseModel == null)
            {
                _log.Warn("Error occurred loading sense config file");
                Messages.Add("Error occurred loading sense config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            try
            {
                _log.Info("Turning stim off for left");
                //Stim therapy must be off to setup embedded adaptive DBS
                bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                Messages.Add("Turning Stim Therapy OFF");
                if (CheckForReturnError(bufferReturnInfo, theSummitLeft, "Turn Stim Therapy off", false))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred turning stim off... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            Thread.Sleep(300);
            try
            {
                _log.Info("Turn embedded off left");
                //Make sure embedded therapy is turned off while setting up parameters
                bufferReturnInfo = theSummitLeft.WriteAdaptiveMode(AdaptiveTherapyModes.Disabled);
                if (CheckForReturnError(bufferReturnInfo, theSummitLeft, "Disabling Embedded Therapy Mode", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred turning adaptive off... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            _log.Info("Stop sensing left");
            //Stop sensing
            if (!StopSensing(theSummitLeft, true))
            {
                Messages.Add("Error occurred stopping sensing...Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            _log.Info("Configure sensing left");
            //configure sensing 
            if (!SummitConfigureSensing(theSummitLeft, leftSenseModel, true))
            {
                Messages.Add("Could not configure sensing...Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            _log.Info("Configure LD0 detector left");
            //configure LD0 
            if (!WriteLD0DetectorConfiguration(leftAdaptiveModel, theSummitLeft, true))
            {
                Messages.Add("Could not configure detector for LD0");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            try
            {
                if (leftAdaptiveModel.Detection.LD1.IsEnabled)
                {
                    _log.Info("Configure LD1 detector left");
                    //configure LD1
                    if (!WriteLD1DetectorConfiguration(leftAdaptiveModel, theSummitLeft, true))
                    {
                        Messages.Add("Could not configure detector for LD1");
                        _stopBothThreads = true;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred checking if LD1 is enabled. Please check adaptive config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            
            //configure adaptive and update 
            // Clear settings
            try
            {
                _log.Info("Clear adaptive settings left");
                bufferReturnInfo = theSummitLeft.WriteAdaptiveClearSettings(AdaptiveClearTypes.All, 0);
                if (CheckForReturnError(bufferReturnInfo, theSummitLeft, "Clear Apative Therapy Settings", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred clearing adaptive settings... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            try
            {
                _log.Info("Write adaptive deltas left");
                // Deltas - 0.1mA/second
                AdaptiveDeltas[] embeddedDeltas = new AdaptiveDeltas[4];
                embeddedDeltas[0] = new AdaptiveDeltas(leftAdaptiveModel.Adaptive.Program0.RiseTimes, leftAdaptiveModel.Adaptive.Program0.FallTimes);
                embeddedDeltas[1] = new AdaptiveDeltas(0, 0);
                embeddedDeltas[2] = new AdaptiveDeltas(0, 0);
                embeddedDeltas[3] = new AdaptiveDeltas(0, 0);
                bufferReturnInfo = theSummitLeft.WriteAdaptiveDeltas(embeddedDeltas);
                if (CheckForReturnError(bufferReturnInfo, theSummitLeft, "Write Adaptive Deltas", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred writing adaptive deltas... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            _log.Info("Write adaptive states left");
            //Attempt to write adaptive states and settings
            if (!WriteAdaptiveStates(leftAdaptiveModel, theSummitLeft, true))
            {
                Messages.Add("Could not write adaptive states... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            _log.Info("Start sensing and streaming left");
            if (!StartSensingAndStreaming(true, theSummitLeft, leftSenseModel))
            {
                Messages.Add("Could not start sensing or streaming... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            
            // Set the Stimulation Mode to Adaptive
            try
            {
                _log.Info("Write embedded adaptive left");
                bufferReturnInfo = theSummitLeft.WriteAdaptiveMode(AdaptiveTherapyModes.Embedded);
                if (CheckForReturnError(bufferReturnInfo, theSummitLeft, "Turn Adaptive Therapy to Embedded", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred turning on embedded therapy mode... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            // Make Group D Active
            try
            {
                _log.Info("Move to group d left");
                bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group3);
                if (CheckForReturnError(bufferReturnInfo, theSummitLeft, "Change Stim Active Group to D", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred changing to group D... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            
            // Turn on Stim
            try
            {
                _log.Info("Turn stim therapy on left");
                bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                Messages.Add("Turning Stim Therapy ON");
                if(CheckForReturnError(bufferReturnInfo, theSummitLeft, "Turn stim therapy ON", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred turning therapy on... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            // Reset POR if set
            if (bufferReturnInfo.RejectCodeType == typeof(MasterRejectCode)
                && (MasterRejectCode)bufferReturnInfo.RejectCode == MasterRejectCode.ChangeTherapyPor)
            {
                _log.Info("Reset POR bit left");
                ResetPOR(theSummitLeft);
                try
                {
                    _log.Info("Turn therapy on left");
                    bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                    if (CheckForReturnError(bufferReturnInfo, theSummitLeft, "Turn Stim Therapy On", true))
                    {
                        _stopBothThreads = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    Messages.Add("Error occurred turning therapy on... Please try again...");
                    _stopBothThreads = true;
                    return;
                }
            }

            if (_stopBothThreads)
            {
                return;
            }
            
            //save currrent index as previous and change index of currentIndex in MasterSwitchModel to next value
            try
            {
                _log.Info("Saving index left");
                previousIndexLeft = masterSwitchModelLeftDefault.CurrentIndex;
                if ((masterSwitchModelLeftDefault.CurrentIndex + 1) >= masterSwitchModelLeftDefault.ConfigNames.Count())
                {
                    masterSwitchModelLeftDefault.CurrentIndex = 0;
                }
                else
                {
                    masterSwitchModelLeftDefault.CurrentIndex++;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred reading current index in default config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            try
            {
                _log.Info("Saving new DateTimeLastSwitch in left");
                masterSwitchModelLeftDefault.DateTimeLastSwitch = DateTime.UtcNow.ToString();
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred reading date and time since last switch in the config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }


            leftSideFinished = true;
            _log.Info("Default/left side updated");
        }

        /// <summary>
        /// Worker thread that runs the Switch for the right/bilateral side
        /// </summary>
        private void WorkerThreadRight()
        {
            APIReturnInfo bufferReturnInfo;
            _log.Info("Starting bilateral right side");

            //Get the filename from the config list at the current index
            string filename;
            try
            {
                filename = masterSwitchModelRight.ConfigNames[masterSwitchModelRight.CurrentIndex];
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred reading current filename in bilateral config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            if (!String.IsNullOrEmpty(filename))
            {
                //load adaptive config file at currentIndex
                string filePath = rightPrefixFileLocation + filename + JSON_EXTENSION;
                rightAdaptiveModel = jSONService?.GetAdaptiveModelFromFile(filePath);
            }
            else
            {
                _log.Warn("Error occurred reading current filename for config file");
                Messages.Add("Error occurred reading current filename for config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            if (rightAdaptiveModel == null)
            {
                _log.Warn("Error occurred loading adaptive config file");
                Messages.Add("Error occurred loading adaptive config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }
            
            //Load sense config file
            rightSenseModel = jSONService?.GetSenseModelFromFile(senseRightFileLocation);
            if (rightSenseModel == null)
            {
                _log.Warn("Error occurred loading sense config file");
                Messages.Add("Error occurred loading sense config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            try
            {
                _log.Info("Turnign stim therapy off");
                //Stim therapy must be off to setup embedded adaptive DBS
                bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                Messages.Add("Turning Stim Therapy OFF");
                if (CheckForReturnError(bufferReturnInfo, theSummitRight, "Turn Stim Therapy off", false))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred turning stim off... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            Thread.Sleep(300);
            try
            {
                _log.Info("Disable adaptive therapy right");
                //Make sure embedded therapy is turned off while setting up parameters
                bufferReturnInfo = theSummitRight.WriteAdaptiveMode(AdaptiveTherapyModes.Disabled);
                if (CheckForReturnError(bufferReturnInfo, theSummitRight, "Disabling Embedded Therapy Mode", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred turning adaptive off... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            _log.Info("Stop sensing right");
            //Stop sensing
            if (!StopSensing(theSummitRight, true))
            {
                Messages.Add("Error occurred stopping sensing...Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            _log.Info("Configure sensing right");
            //configure sensing 
            if (!SummitConfigureSensing(theSummitRight, rightSenseModel, true))
            {
                Messages.Add("Could not configure sensing...Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            _log.Info("Write LD0 detector right");
            //configure LD0 
            if (!WriteLD0DetectorConfiguration(rightAdaptiveModel, theSummitRight, true))
            {
                Messages.Add("Could not configure detector for LD0");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            try
            {
                if (rightAdaptiveModel.Detection.LD1.IsEnabled)
                {
                    _log.Info("Write LD1 detector right");
                    //configure LD1
                    if (!WriteLD1DetectorConfiguration(rightAdaptiveModel, theSummitRight, true))
                    {
                        Messages.Add("Could not configure detector for LD1");
                        _stopBothThreads = true;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred checking if LD1 is enabled. Please check adaptive config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            
            //configure adaptive and update 
            // Clear settings
            try
            {
                _log.Info("Clear adaptive settings right");
                bufferReturnInfo = theSummitRight.WriteAdaptiveClearSettings(AdaptiveClearTypes.All, 0);
                if (CheckForReturnError(bufferReturnInfo, theSummitRight, "Clear Apative Therapy Settings", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred clearing adaptive settings... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            try
            {
                _log.Info("Write deltas right");
                // Deltas - 0.1mA/second
                AdaptiveDeltas[] embeddedDeltas = new AdaptiveDeltas[4];
                embeddedDeltas[0] = new AdaptiveDeltas(rightAdaptiveModel.Adaptive.Program0.RiseTimes, rightAdaptiveModel.Adaptive.Program0.FallTimes);
                embeddedDeltas[1] = new AdaptiveDeltas(0, 0);
                embeddedDeltas[2] = new AdaptiveDeltas(0, 0);
                embeddedDeltas[3] = new AdaptiveDeltas(0, 0);
                bufferReturnInfo = theSummitRight.WriteAdaptiveDeltas(embeddedDeltas);
                if (CheckForReturnError(bufferReturnInfo, theSummitRight, "Write Adaptive Deltas", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred writing adaptive deltas... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            _log.Info("Write adaptive states right");
            //Attempt to write adaptive states and settings
            if (!WriteAdaptiveStates(rightAdaptiveModel, theSummitRight, true))
            {
                Messages.Add("--ERROR: Writing Adaptive States. Please check connection or that adaptive config file is correct--");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            _log.Info("Start sensing and streaming right");
            if (!StartSensingAndStreaming(true, theSummitRight, rightSenseModel))
            {
                Messages.Add("Could not start sensing or streaming... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            // Set the Stimulation Mode to Adaptive
            try
            {
                _log.Info("Write embedded adaptive right");
                bufferReturnInfo = theSummitRight.WriteAdaptiveMode(AdaptiveTherapyModes.Embedded);
                if (CheckForReturnError(bufferReturnInfo, theSummitRight, "Turn Adaptive Therapy to Embedded", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred turning on embedded therapy mode... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            // Make Group D Active
            try
            {
                _log.Info("Move to group D right");
                bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group3);
                if (CheckForReturnError(bufferReturnInfo, theSummitRight, "Change Stim Active Group to D", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred changing to group D... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            // Turn on Stim
            try
            {
                _log.Info("Turn Stim therapy On right");
                bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                Messages.Add("Turning Stim Therapy ON");
                if (CheckForReturnError(bufferReturnInfo, theSummitRight, "Turn stim therapy ON", true))
                {
                    _stopBothThreads = true;
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred turning therapy on... Please try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }
            
            // Reset POR if set
            if (bufferReturnInfo.RejectCodeType == typeof(MasterRejectCode)
                && (MasterRejectCode)bufferReturnInfo.RejectCode == MasterRejectCode.ChangeTherapyPor)
            {
                _log.Info("Reset POR bit right");
                ResetPOR(theSummitRight);
                try
                {
                    _log.Info("Turn stim therapy on right");
                    bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                    if (CheckForReturnError(bufferReturnInfo, theSummitRight, "Turn Stim Therapy On", true))
                    {
                        _stopBothThreads = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    Messages.Add("Error occurred turning therapy on... Please try again...");
                    _stopBothThreads = true;
                    return;
                }
            }

            if (_stopBothThreads)
            {
                return;
            }
            
            //save currrent index as previous and change index of currentIndex in MasterSwitchModel to next value
            try
            {
                _log.Info("Saving index right");
                previousIndexRight = masterSwitchModelRight.CurrentIndex;
                if ((masterSwitchModelRight.CurrentIndex + 1) >= masterSwitchModelRight.ConfigNames.Count())
                {
                    masterSwitchModelRight.CurrentIndex = 0;
                }
                else
                {
                    masterSwitchModelRight.CurrentIndex++;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred reading current index in bilateral config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            try
            {
                _log.Info("Saving new DateTimeLastSwitch in right");
                masterSwitchModelRight.DateTimeLastSwitch = DateTime.UtcNow.ToString();
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error occurred reading date and time since last switch in the config file... Please fix config file and try again...");
                _stopBothThreads = true;
                return;
            }

            if (_stopBothThreads)
            {
                return;
            }

            rightSideFinished = true;
            _log.Info("Bilateral Right Side Updated");
        }

        /// <summary>
        /// Finishing code to be done when switch is finished or canceled.
        /// </summary>
        private void FinishingThreadCode()
        {
            bool flagToQuitWhileLoop = false;
            while (!flagToQuitWhileLoop)
            {
                Thread.Sleep(3000);
                if (_stopBothThreads || _cancelButtonPressed)
                {
                    _stopBothThreads = true;
                    flagToQuitWhileLoop = true;
                    continue;
                }
                if (isBilateral && leftSideFinished && rightSideFinished)
                {
                    _log.Info("Left and right side finished... Writing files back to directories");
                    if (WriteLeftConfigFilesToDirectories() && WriteRightConfigFilesToDirecotries())
                    {

                        APIReturnInfo bufferReturnInfo;                        
                        //Report switch success in log for left
                        int counter = 5;
                        try
                        {
                            do
                            {
                                bufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Switch Successful", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                            if (counter == 0)
                            {
                                MessageBox.Show("Could not log switch success in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            //Report switch success in log for right
                            counter = 5;
                            do
                            {
                                bufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Switch Successful", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                            if (counter == 0)
                            {
                                MessageBox.Show("Could not log switch success in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        catch(Exception e)
                        {
                            _log.Error(e);
                            MessageBox.Show("Could not log switch success in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }

                        _log.Info("Switch for bilateral successful!");
                        DisplayMessageBox("SWITCH SUCCESSFUL");
                        //SendEmailToConfirm();
                        flagToQuitWhileLoop = true;
                        TryClose();
                        return;
                    }
                    else
                    {
                        WriteEventLog(theSummitLeft, "Switch Successful but files not written to directory", "Could not log switch success with file write error in event log. Please report error to clinician.");
                        WriteEventLog(theSummitRight, "Switch Successful but files not written to directory", "Could not log switch success with file write error in event log. Please report error to clinician.");
                    }
                    flagToQuitWhileLoop = true;
                }
                else if (!isBilateral && leftSideFinished)
                {
                    APIReturnInfo bufferReturnInfo;
                    //Report switch success in log for left
                    int counter = 5;
                    try
                    {
                        do
                        {
                            bufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Switch Successful", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                        if (counter == 0)
                        {
                            MessageBox.Show("Could not log switch success in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        MessageBox.Show("Could not log switch success in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    _log.Info("Left side finished... Writing files back to directories");
                    if (WriteLeftConfigFilesToDirectories())
                    {
                        _log.Info("Switch for left side successful!");
                        DisplayMessageBox("SWITCH SUCCESSFUL");
                        //SendEmailToConfirm();
                        flagToQuitWhileLoop = true;
                        TryClose();
                        return;
                    }
                    else
                    {
                        WriteEventLog(theSummitLeft, "Switch Successful but files not written to directory", "Could not log switch success with file write error in event log. Please report error to clinician.");
                    }
                    flagToQuitWhileLoop = true;
                }
            }
            if (!_cancelButtonPressed)
            {
                APIReturnInfo bufferReturnInfo;
                //Report switch success in log for left
                int counter = 5;
                try
                {
                    do
                    {
                        bufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Switch Failed", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                    } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                    if (counter == 0)
                    {
                        MessageBox.Show("Could not log switch success in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    if (isBilateral)
                    {
                        //Report switch success in log for right
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Switch Failed", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                        if (counter == 0)
                        {
                            MessageBox.Show("Could not log switch success in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    MessageBox.Show("Could not log switch failed in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _log.Warn("Error occurred and need to run switch again");
                DisplayMessageBox("An error occurred while updating.  Please run Switch again");
                TryClose();
                return;
            }
        }

        /// <summary>
        /// Closes the switch screen
        /// </summary>
        public void CancelButtonClick()
        {
            _cancelButtonPressed = true;
            _stopBothThreads = true;
            TryClose();
        }

        #region Medtronic API for Adaptive
        /// <summary>
        /// Write Detector Configuration for LD0
        /// </summary>
        /// <returns>true if successfully write LD detector settings or false if unsuccessful</returns>
        private bool WriteLD0DetectorConfiguration(AdaptiveModel adaptiveConfig, SummitSystem localSummit, bool turnErrorHandlingOn)
        {
            APIReturnInfo bufferReturnInfo;
            // ********************************** Detector Settings **********************************`

            // Create a LD configuration and write it down to the device for both LD.
            LinearDiscriminantConfiguration configLd = new LinearDiscriminantConfiguration();
            try
            {
                // Enable dual threshold mode - LD output can be high, in-range, or below the two thresholds defined in the bias term.
                // If DualThreshold is not enabled, set it to None.
                if (adaptiveConfig.Detection.LD0.DualThreshold && adaptiveConfig.Detection.LD0.BlankBothLD)
                {
                    configLd.DetectionEnable = DetectionEnables.DualThresholdEnabled | DetectionEnables.BlankBoth;
                }
                else if (adaptiveConfig.Detection.LD0.DualThreshold && !adaptiveConfig.Detection.LD0.BlankBothLD)
                {
                    configLd.DetectionEnable = DetectionEnables.DualThresholdEnabled;
                }
                else if (!adaptiveConfig.Detection.LD0.DualThreshold && adaptiveConfig.Detection.LD0.BlankBothLD)
                {
                    configLd.DetectionEnable = DetectionEnables.BlankBoth;
                }
                else
                {
                    configLd.DetectionEnable = DetectionEnables.None;
                }
                // Convert the Detection inputs based on the config file values
                configLd.DetectionInputs = ConfigConversions.DetectionInputsConvert(
                    adaptiveConfig.Detection.LD0.Inputs.Ch0Band0,
                    adaptiveConfig.Detection.LD0.Inputs.Ch0Band1,
                    adaptiveConfig.Detection.LD0.Inputs.Ch1Band0,
                    adaptiveConfig.Detection.LD0.Inputs.Ch1Band1,
                    adaptiveConfig.Detection.LD0.Inputs.Ch2Band0,
                    adaptiveConfig.Detection.LD0.Inputs.Ch2Band1,
                    adaptiveConfig.Detection.LD0.Inputs.Ch3Band0,
                    adaptiveConfig.Detection.LD0.Inputs.Ch3Band1);
                // Update LD state 
                configLd.UpdateRate = adaptiveConfig.Detection.LD0.UpdateRate;
                // Set other timing parameters
                configLd.OnsetDuration = adaptiveConfig.Detection.LD0.OnsetDuration;
                configLd.TerminationDuration = adaptiveConfig.Detection.LD0.TerminationDuration;
                configLd.HoldoffTime = adaptiveConfig.Detection.LD0.HoldOffOnStartupTime;
                configLd.BlankingDurationUponStateChange = adaptiveConfig.Detection.LD0.StateChangeBlankingUponStateChange;
                // Set the weight vectors for the power inputs, since only one channel is used rest can be zero.
                configLd.Features[0].WeightVector = adaptiveConfig.Detection.LD0.WeightVector[0];
                configLd.Features[1].WeightVector = adaptiveConfig.Detection.LD0.WeightVector[1]; 
                configLd.Features[2].WeightVector = adaptiveConfig.Detection.LD0.WeightVector[2]; 
                configLd.Features[3].WeightVector = adaptiveConfig.Detection.LD0.WeightVector[3]; 
                // Set the normalization vectors for the power inputs, since only one channel is used rest can be zero. 
                configLd.Features[0].NormalizationMultiplyVector = adaptiveConfig.Detection.LD0.NormalizationMultiplyVector[0];
                configLd.Features[1].NormalizationMultiplyVector = adaptiveConfig.Detection.LD0.NormalizationMultiplyVector[1];
                configLd.Features[2].NormalizationMultiplyVector = adaptiveConfig.Detection.LD0.NormalizationMultiplyVector[2];
                configLd.Features[3].NormalizationMultiplyVector = adaptiveConfig.Detection.LD0.NormalizationMultiplyVector[3];
                // Set the normalization subtract vectors for the power inputs
                configLd.Features[0].NormalizationSubtractVector = adaptiveConfig.Detection.LD0.NormalizationSubtractVector[0];
                configLd.Features[1].NormalizationSubtractVector = adaptiveConfig.Detection.LD0.NormalizationSubtractVector[1];
                configLd.Features[2].NormalizationSubtractVector = adaptiveConfig.Detection.LD0.NormalizationSubtractVector[2];
                configLd.Features[3].NormalizationSubtractVector = adaptiveConfig.Detection.LD0.NormalizationSubtractVector[3];
                // Set the thresholds
                configLd.BiasTerm[0] = adaptiveConfig.Detection.LD0.B0;
                configLd.BiasTerm[1] = adaptiveConfig.Detection.LD0.B1;
                // Set the fixed point value
                configLd.FractionalFixedPointValue = adaptiveConfig.Detection.LD0.FractionalFixedPointValue;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("--ERROR: Writing LD0 Detector Parameters. Please check that adaptive config file is correct.--");
                return false;
            }
            
            // Write the detector down to the INS
            try
            {
                //Attempt to write the detection parameters to medtronic api
                bufferReturnInfo = localSummit.WriteAdaptiveDetectionParameters(0, configLd);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Error Writing Detection Parameters", turnErrorHandlingOn))
                    return false;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("--ERROR: Writing LD0 Detector Parameters--");
                return false;
            }
            return true;
        }
        /// <summary>
        /// Write Detector Configuration for LD0
        /// </summary>
        /// <returns>true if successfully write LD detector settings or false if unsuccessful</returns>
        private bool WriteLD1DetectorConfiguration(AdaptiveModel adaptiveConfig, SummitSystem localSummit, bool turnErrorHandlingOn)
        {
            APIReturnInfo bufferReturnInfo;
            // ********************************** Detector Settings **********************************`

            // Create a LD configuration and write it down to the device for both LD.
            LinearDiscriminantConfiguration configLd = new LinearDiscriminantConfiguration();
            try
            {
                // Enable dual threshold mode - LD1 output can be high, in-range, or below the two thresholds defined in the bias term.
                // If DualThreshold is not enabled, set it to None.
                if (adaptiveConfig.Detection.LD1.DualThreshold && adaptiveConfig.Detection.LD1.BlankBothLD)
                {
                    configLd.DetectionEnable = DetectionEnables.DualThresholdEnabled | DetectionEnables.BlankBoth;
                }
                else if (adaptiveConfig.Detection.LD1.DualThreshold && !adaptiveConfig.Detection.LD1.BlankBothLD)
                {
                    configLd.DetectionEnable = DetectionEnables.DualThresholdEnabled;
                }
                else if (!adaptiveConfig.Detection.LD1.DualThreshold && adaptiveConfig.Detection.LD1.BlankBothLD)
                {
                    configLd.DetectionEnable = DetectionEnables.BlankBoth;
                }
                else
                {
                    configLd.DetectionEnable = DetectionEnables.None;
                }
                // Convert the Detection inputs based on the config file values
                configLd.DetectionInputs = ConfigConversions.DetectionInputsConvert(
                    adaptiveConfig.Detection.LD1.Inputs.Ch0Band0,
                    adaptiveConfig.Detection.LD1.Inputs.Ch0Band1,
                    adaptiveConfig.Detection.LD1.Inputs.Ch1Band0,
                    adaptiveConfig.Detection.LD1.Inputs.Ch1Band1,
                    adaptiveConfig.Detection.LD1.Inputs.Ch2Band0,
                    adaptiveConfig.Detection.LD1.Inputs.Ch2Band1,
                    adaptiveConfig.Detection.LD1.Inputs.Ch3Band0,
                    adaptiveConfig.Detection.LD1.Inputs.Ch3Band1);
                // Update LD state 
                configLd.UpdateRate = adaptiveConfig.Detection.LD1.UpdateRate;
                // Set other timing parameters
                configLd.OnsetDuration = adaptiveConfig.Detection.LD1.OnsetDuration;
                configLd.TerminationDuration = adaptiveConfig.Detection.LD1.TerminationDuration;
                configLd.HoldoffTime = adaptiveConfig.Detection.LD1.HoldOffOnStartupTime;
                configLd.BlankingDurationUponStateChange = adaptiveConfig.Detection.LD1.StateChangeBlankingUponStateChange;
                // Set the weight vectors for the power inputs, since only one channel is used rest can be zero.
                configLd.Features[0].WeightVector = adaptiveConfig.Detection.LD1.WeightVector[0];
                configLd.Features[1].WeightVector = adaptiveConfig.Detection.LD1.WeightVector[1];
                configLd.Features[2].WeightVector = adaptiveConfig.Detection.LD1.WeightVector[2];
                configLd.Features[3].WeightVector = adaptiveConfig.Detection.LD1.WeightVector[3];
                // Set the normalization vectors for the power inputs, since only one channel is used rest can be zero. 
                configLd.Features[0].NormalizationMultiplyVector = adaptiveConfig.Detection.LD1.NormalizationMultiplyVector[0];
                configLd.Features[1].NormalizationMultiplyVector = adaptiveConfig.Detection.LD1.NormalizationMultiplyVector[1];
                configLd.Features[2].NormalizationMultiplyVector = adaptiveConfig.Detection.LD1.NormalizationMultiplyVector[2];
                configLd.Features[3].NormalizationMultiplyVector = adaptiveConfig.Detection.LD1.NormalizationMultiplyVector[3];
                // Set the normalization subtract vectors for the power inputs
                configLd.Features[0].NormalizationSubtractVector = adaptiveConfig.Detection.LD1.NormalizationSubtractVector[0];
                configLd.Features[1].NormalizationSubtractVector = adaptiveConfig.Detection.LD1.NormalizationSubtractVector[1];
                configLd.Features[2].NormalizationSubtractVector = adaptiveConfig.Detection.LD1.NormalizationSubtractVector[2];
                configLd.Features[3].NormalizationSubtractVector = adaptiveConfig.Detection.LD1.NormalizationSubtractVector[3];
                // Set the thresholds
                configLd.BiasTerm[0] = adaptiveConfig.Detection.LD1.B0;
                configLd.BiasTerm[1] = adaptiveConfig.Detection.LD1.B1;
                // Set the fixed point value
                configLd.FractionalFixedPointValue = adaptiveConfig.Detection.LD1.FractionalFixedPointValue;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("--ERROR: Writing LD1 Detector Parameters. Please check that adaptive config file is correct.--");
                return false;
            }

            // Write the detector down to the INS
            try
            {
                //Attempt to write the detection parameters to medtronic api
                bufferReturnInfo = localSummit.WriteAdaptiveDetectionParameters(1, configLd);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Error Writing Detection Parameters", turnErrorHandlingOn))
                    return false;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("--ERROR: Writing LD1 Detector Parameters--");
                return false;
            }
            return true;
        }
        /// <summary>
        /// Setup adaptive states
        /// </summary>
        /// <returns>true if successfully write adaptive states or false if unsuccessful</returns>
        private bool WriteAdaptiveStates(AdaptiveModel adaptiveConfig, SummitSystem theSummit, bool turnErrorHandlingOn)
        {
            APIReturnInfo bufferReturnInfo;
            try
            {
                //For now, this just sets up state 0-2 and sets the other states at 25.5 which is the value to hold the current
                AdaptiveState aState = new AdaptiveState();
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State0AmpInMilliamps;
                aState.Prog1AmpInMilliamps = 0;
                aState.Prog2AmpInMilliamps = 0;
                aState.Prog3AmpInMilliamps = 0;
                aState.RateTargetInHz = adaptiveConfig.Adaptive.Program0.RateTargetInHz; // Hold Rate
                bufferReturnInfo = theSummit.WriteAdaptiveState(0, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State1AmpInMilliamps;
                bufferReturnInfo = theSummit.WriteAdaptiveState(1, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State2AmpInMilliamps;
                bufferReturnInfo = theSummit.WriteAdaptiveState(2, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State3AmpInMilliamps;
                bufferReturnInfo = theSummit.WriteAdaptiveState(3, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State4AmpInMilliamps;
                bufferReturnInfo = theSummit.WriteAdaptiveState(4, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State5AmpInMilliamps;
                bufferReturnInfo = theSummit.WriteAdaptiveState(5, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State6AmpInMilliamps;
                bufferReturnInfo = theSummit.WriteAdaptiveState(6, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State7AmpInMilliamps;
                bufferReturnInfo = theSummit.WriteAdaptiveState(7, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;
                aState.Prog0AmpInMilliamps = adaptiveConfig.Adaptive.Program0.State8AmpInMilliamps;
                bufferReturnInfo = theSummit.WriteAdaptiveState(8, aState);
                if (CheckForReturnError(bufferReturnInfo, theSummit, "Error Writing adaptive state", turnErrorHandlingOn))
                    return false;

            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }
            return true;
        }

        #endregion

        #region Medtronic for Sensing
        /// <summary>
        /// Stops sensing
        /// </summary>
        /// <returns>true if success and false if unsuccessful</returns>
        private bool StopSensing(SummitSystem localSummit, bool turnErrorHandlingOn)
        {
            APIReturnInfo bufferReturnInfo;
            bool success = true;
            try
            {
                bufferReturnInfo = localSummit.WriteSensingDisableStreams(true, true, true, true, true, true, true, true);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Turn off streaming in StopSensing", turnErrorHandlingOn))
                    success = false;
                bufferReturnInfo = localSummit.WriteSensingState(SenseStates.None, 0x00);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Turn off Sensing", turnErrorHandlingOn))
                    success = false;
            }
            catch (Exception e)
            {
                _log.Error(e);
                success = false;
            }
            return success;
        }

        /// <summary>
        /// Starts sensing and steaming
        /// </summary>
        /// <returns>true if success and false if not success</returns>
        private bool StartSensingAndStreaming(bool turnErrorHandlingOn, SummitSystem localSummit, SenseModel senseModel)
        {
            APIReturnInfo bufferReturnInfo;
            try
            {
                // Start sensing
                bufferReturnInfo = localSummit.WriteSensingState(ConfigConversions.TDSenseStatesConvert(
                    senseModel.SenseOptions.TimeDomain,
                    senseModel.SenseOptions.FFT,
                    senseModel.SenseOptions.Power,
                    senseModel.SenseOptions.LD0,
                    senseModel.SenseOptions.LD1,
                    senseModel.SenseOptions.AdaptiveState,
                    senseModel.SenseOptions.LoopRecording,
                    senseModel.SenseOptions.Unused), ConfigConversions.FFTChannelConvert(senseModel));
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Write Sensing State", turnErrorHandlingOn))
                    return false;
                // Start streaming
                bufferReturnInfo = localSummit.WriteSensingEnableStreams(
                    senseModel.StreamEnables.TimeDomain,
                    senseModel.StreamEnables.FFT,
                    senseModel.StreamEnables.Power,
                    senseModel.StreamEnables.AdaptiveTherapy,
                    senseModel.StreamEnables.AdaptiveState,
                    senseModel.StreamEnables.Accelerometry,
                    senseModel.StreamEnables.TimeStamp,
                    senseModel.StreamEnables.EventMarker);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Stream Enables", turnErrorHandlingOn))
                    return false;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("Error starting sensing or streaming");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Write Sense Configuration Settings
        /// </summary>
        /// <param name="localSummit">Summit system to use</param>
        /// <param name="localModel">The config file to use for sensing parameters</param>
        /// <param name="turnErrorHandlingOn">If true, then the error handling will be done on each error. If false, then no error handling will be done</param>
        /// <returns>true if successfully configuring sensing or false if unsuccessful</returns>
        private bool SummitConfigureSensing(SummitSystem localSummit, SenseModel localModel, bool turnErrorHandlingOn)
        {
            APIReturnInfo bufferReturnInfo;
            // Create a sensing configuration
            List<TimeDomainChannel> TimeDomainChannels = new List<TimeDomainChannel>(4);

            // Channel Specific configuration - 0
            TimeDomainChannels.Add(new TimeDomainChannel(
                getTDSampleRate(localModel.Sense.TimeDomains[0].IsEnabled, localModel),
                ConfigConversions.TdMuxInputsConvert(localModel.Sense.TimeDomains[0].Inputs[0]),
                ConfigConversions.TdMuxInputsConvert(localModel.Sense.TimeDomains[0].Inputs[1]),
                TdEvokedResponseEnable.Standard,
                ConfigConversions.TdLpfStage1Convert(localModel.Sense.TimeDomains[0].Lpf1),
                ConfigConversions.TdLpfStage2Convert(localModel.Sense.TimeDomains[0].Lpf2),
                ConfigConversions.TdHpfsConvert(localModel.Sense.TimeDomains[0].Hpf)));

            // Channel Specific configuration - 1
            TimeDomainChannels.Add(new TimeDomainChannel(
                getTDSampleRate(localModel.Sense.TimeDomains[1].IsEnabled, localModel),
                ConfigConversions.TdMuxInputsConvert(localModel.Sense.TimeDomains[1].Inputs[0]),
                ConfigConversions.TdMuxInputsConvert(localModel.Sense.TimeDomains[1].Inputs[1]),
                TdEvokedResponseEnable.Standard,
                ConfigConversions.TdLpfStage1Convert(localModel.Sense.TimeDomains[1].Lpf1),
                ConfigConversions.TdLpfStage2Convert(localModel.Sense.TimeDomains[1].Lpf2),
                ConfigConversions.TdHpfsConvert(localModel.Sense.TimeDomains[1].Hpf)));

            // Channel Specific configuration - 2
            TimeDomainChannels.Add(new TimeDomainChannel(
                getTDSampleRate(localModel.Sense.TimeDomains[2].IsEnabled, localModel),
                ConfigConversions.TdMuxInputsConvert(localModel.Sense.TimeDomains[2].Inputs[0]),
                ConfigConversions.TdMuxInputsConvert(localModel.Sense.TimeDomains[2].Inputs[1]),
                TdEvokedResponseEnable.Standard,
                ConfigConversions.TdLpfStage1Convert(localModel.Sense.TimeDomains[2].Lpf1),
                ConfigConversions.TdLpfStage2Convert(localModel.Sense.TimeDomains[2].Lpf2),
                ConfigConversions.TdHpfsConvert(localModel.Sense.TimeDomains[2].Hpf)));

            // Channel Specific configuration - 3
            TimeDomainChannels.Add(new TimeDomainChannel(
                getTDSampleRate(localModel.Sense.TimeDomains[3].IsEnabled, localModel),
                ConfigConversions.TdMuxInputsConvert(localModel.Sense.TimeDomains[3].Inputs[0]),
                ConfigConversions.TdMuxInputsConvert(localModel.Sense.TimeDomains[3].Inputs[1]),
                TdEvokedResponseEnable.Standard,
                ConfigConversions.TdLpfStage1Convert(localModel.Sense.TimeDomains[3].Lpf1),
                ConfigConversions.TdLpfStage2Convert(localModel.Sense.TimeDomains[3].Lpf2),
                ConfigConversions.TdHpfsConvert(localModel.Sense.TimeDomains[3].Hpf)));

            // Set up the FFT 
            FftConfiguration fftChannel = new FftConfiguration(
                ConfigConversions.FftSizesConvert(localModel.Sense.FFT.FftSize),
                localModel.Sense.FFT.FftInterval,
                ConfigConversions.FftWindowAutoLoadsConvert(localModel.Sense.FFT.WindowLoad),
                localModel.Sense.FFT.WindowEnabled,
                FftWeightMultiplies.Shift7,
                localModel.Sense.FFT.StreamSizeBins,
                localModel.Sense.FFT.StreamOffsetBins);

            // Set up the Power channels
            List<PowerChannel> powerChannels = new List<PowerChannel>();
            //This goes through each power channel and gets the lower power band and upper power band.
            //Medtronic api uses bin index values for setting power channels instead of actual values in Hz
            //This calls the CalculatePowerBins class to convert to proper medtronic api values from the user config file
            //User config file has estimated values in Hz for each channel.  
            //CalculatePowerBins converts them to actual power values and we are able to get the bin index value from that.
            CalculatePowerBins calculatePowerBins = new CalculatePowerBins(_log);
            List<PowerBandModel> powerBandsList = calculatePowerBins.GetPowerBands(localModel);
            if (powerBandsList == null || powerBandsList.Count < 3)
            {
                MessageBox.Show("Error calculating power bins. Please check that power bins are correct in the config file and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
                return false;
            }
            for (int i = 0; i < 4; i++)
            {
                //Add the lower and upper power bands to the power channel
                powerChannels.Add(new PowerChannel(powerBandsList[i].lowerIndexBand0, powerBandsList[i].upperIndexBand0, powerBandsList[i].lowerIndexBand1, powerBandsList[i].upperIndexBand1));
            }
            //Gets the enabled power bands from the sense config file and returns the correct api call for all enabled
            BandEnables theBandEnables = ConfigConversions.PowerBandEnablesConvert(localModel);

            // Set up the miscellaneous settings
            MiscellaneousSensing miscsettings = new MiscellaneousSensing();
            miscsettings.StreamingRate = ConfigConversions.MiscStreamRateConvert(localModel.Sense.Misc.StreamingRate);
            miscsettings.LrTriggers = ConfigConversions.MiscloopRecordingTriggersConvert(localModel.Sense.Misc.LoopRecordingTriggersState, localModel.Sense.Misc.LoopRecordingTriggersIsEnabled);
            miscsettings.LrPostBufferTime = localModel.Sense.Misc.LoopRecordingPostBufferTime;
            miscsettings.Bridging = ConfigConversions.MiscBridgingConfigConvert(localModel.Sense.Misc.Bridging);

            //Writes all sensing information here
            try
            {
                bufferReturnInfo = localSummit.WriteSensingTimeDomainChannels(TimeDomainChannels);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Writing Sensing Time Domain", turnErrorHandlingOn))
                    return false;
                bufferReturnInfo = localSummit.WriteSensingFftSettings(fftChannel);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Writing Sensing FFT", turnErrorHandlingOn))
                    return false;
                bufferReturnInfo = localSummit.WriteSensingPowerChannels(theBandEnables, powerChannels);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Writing Sensing Power", turnErrorHandlingOn))
                    return false;
                bufferReturnInfo = localSummit.WriteSensingMiscSettings(miscsettings);
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Writing Sensing Misc", turnErrorHandlingOn))
                    return false;
                bufferReturnInfo = localSummit.WriteSensingAccelSettings(ConfigConversions.AccelSampleRateConvert(localModel.Sense.Accelerometer.SampleRate, localModel.Sense.Accelerometer.SampleRateDisabled));
                if (CheckForReturnError(bufferReturnInfo, localSummit, "Writing Sensing Accel", turnErrorHandlingOn))
                    return false;
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("--ERROR: Writing Sensing--");
                return false;
            }
            return true;
        }
        #endregion

        #region Error Handling
        private bool WriteEventLog(SummitSystem localSummit, string successLogMessage, string unsuccessfulMessageBoxMessage)
        {
            APIReturnInfo bufferReturnInfo;
            int counter = 5;
            try
            {
                do
                {
                    bufferReturnInfo = localSummit.LogCustomEvent(DateTime.Now, DateTime.Now, successLogMessage, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                if (counter == 0)
                {
                    MessageBox.Show(unsuccessfulMessageBoxMessage, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                MessageBox.Show("Error calling summit system while logging event.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return true;
        }
        /// <summary>
        /// Checks for return error code from APIReturnInfo from Medtronic
        /// If there is an error, the method calls error handling method SetEmbeddedOffGroupAStimOnWhenErrorOccurs() to turn embedded off, change to group A and turn Stim ON
        /// The Error location and error descriptor from the returned API call are displayed to user in a message box.
        /// </summary>
        /// <param name="info">The APIReturnInfo value returned from the Medtronic API call</param>
        /// <param name="localSummit">Summit system</param>
        /// <param name="errorLocation">The location where the error is being check. Can be turning stim on, changing group, etc</param>
        /// /// <param name="runErrorHandling">If true, run the error handling process of turn stim on, group A, turn embedded off. If false, don't run error handling</param>
        /// <returns>True if there was an error or false if no error</returns>
        private bool CheckForReturnError(APIReturnInfo info, SummitSystem localSummit, string errorLocation, bool runErrorHandling)
        {
            if (info.RejectCode != 0)
            {
                _log.Warn("Error from Medtronic API. Reject code: " + info.RejectCode + ". Reject description: " + info.Descriptor + ". Error Location: " + errorLocation);
                _stopBothThreads = true;
                if (runErrorHandling)
                {
                    SetEmbeddedOffGroupAStimOnWhenErrorOccurs(localSummit);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Method for error handling
        /// This method turns embedded therapy off, changes to group A and turns stim ON.
        /// If this fails for any of these steps, then an error message is displayed to user detailing what went wrong and the program is closed
        /// If the error handling is successful, then the error is logged in the event log and program execution continues
        /// </summary>
        private void SetEmbeddedOffGroupAStimOnWhenErrorOccurs(SummitSystem theSummit)
        {
            _log.Info("Running Error Handling..................");
            APIReturnInfo bufferReturnInfo;
            try
            {
                _log.Info("Turn stim therapy off");
                //Turn everything back to normal: change to Group A, turn stim on, turn embedded off;
                bufferReturnInfo = theSummit.StimChangeTherapyOff(false);
                Messages.Add("Turning Stim Therapy OFF: " + bufferReturnInfo.Descriptor);
                if (bufferReturnInfo.RejectCode != 0)
                {
                    _log.Warn("Could not turn therapy off. Reject code: " + bufferReturnInfo.RejectCode + ".Reject description: " + bufferReturnInfo.Descriptor);
                    DisplayMessageBox("Could NOT turn therapy off to turn embedded therapy off. Reject Description: " + bufferReturnInfo.Descriptor + ". Please connect with PTM, move to group A and turn stim back on. SHUTTING DOWN WINDOW");
                    TryClose();
                    return;
                }
                Thread.Sleep(300);
                _log.Info("Turn embedded off");
                bufferReturnInfo = theSummit.WriteAdaptiveMode(AdaptiveTherapyModes.Disabled);
                Messages.Add("Disabling Therapy Mode: " + bufferReturnInfo.Descriptor);
                if (bufferReturnInfo.RejectCode != 0)
                {
                    _log.Warn("Could not turn off embedded. Reject code: " + bufferReturnInfo.RejectCode + ".Reject description: " + bufferReturnInfo.Descriptor);
                    DisplayMessageBox("Could NOT disable embedded therapy. Reject Description: " + bufferReturnInfo.Descriptor + ". Please connect with PTM, move to group A and turn stim back on. SHUTTING DOWN WINDOW");
                    TryClose();
                    return;
                }
                _log.Info("Move to Group A");
                bufferReturnInfo = theSummit.StimChangeActiveGroup(ActiveGroup.Group0);
                Messages.Add("Changing to Group A: " + bufferReturnInfo.Descriptor);
                if (bufferReturnInfo.RejectCode != 0)
                {
                    _log.Warn("Could not move to Group A. Reject code: " + bufferReturnInfo.RejectCode + ".Reject description: " + bufferReturnInfo.Descriptor);
                    DisplayMessageBox("Could NOT change to Group A. Reject Description: " + bufferReturnInfo.Descriptor + ". Please connect with PTM, move to group A and turn stim back on. SHUTTING DOWN WINDOW");
                    TryClose();
                    return;
                }
                _log.Info("Turn Stim therapy ON");
                bufferReturnInfo = theSummit.StimChangeTherapyOn();
                Messages.Add("Turning Stim Therapy ON: " + bufferReturnInfo.Descriptor);
                if (bufferReturnInfo.RejectCode != 0)
                {
                    _log.Warn("Could not turn therapy on. Reject code: " + bufferReturnInfo.RejectCode + ".Reject description: " + bufferReturnInfo.Descriptor);
                    DisplayMessageBox("Could NOT turn Stim Therapy On. Reject Description: " + bufferReturnInfo.Descriptor + ". Please connect with PTM, move to group A and turn stim back on. SHUTTING DOWN WINDOW");
                    TryClose();
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                DisplayMessageBox("ERROR: Could not run error handling.  Please connect with PTM, move to group A and turn stim back on. Closing program");
                TryClose();
                return;
            }
        }

        /// <summary>
        /// Displays a message box to user
        /// </summary>
        /// <param name="message">Message to display to user</param>
        private void DisplayMessageBox(string message)
        {
            string messageBoxText = message;
            string caption = "ATTENTION";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = MessageBoxImage.Information;
            MessageBox.Show(messageBoxText, caption, button, icon);
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Gets the TDSampleRate for the TimeDomain Channel
        /// Calls TD SampleRateConvert from ConfigConversions class
        /// Checks for disabled time domain channels and returns proper sample rate or disabled for disabled channels
        /// </summary>
        /// <param name="sampleRateIsEnabled">Either true or false depending on value for Time Domain Channel IsEnabled value from config file</param>
        /// <param name="localModel">Sense model from the config file</param>
        /// <returns>If the sampleRateIsEnabled variable is set to false, then it returns the TdSampleRates.Disabled. Otherwise it returns the correct TdSampleRates variable for the corresponding TD sample rate from the config file</returns>
        private TdSampleRates getTDSampleRate(bool sampleRateIsEnabled, SenseModel localModel)
        {
            TdSampleRates the_sample_rate = ConfigConversions.TDSampleRateConvert(localModel.Sense.TDSampleRate);
            if (!sampleRateIsEnabled)
            {
                the_sample_rate = TdSampleRates.Disabled;
            }
            return the_sample_rate;
        }

        /// <summary>
        /// Checks to make sure upper power band is greater than lower power band index
        /// The upper power band index must be greater or equal to lower power band index
        /// </summary>
        /// <param name="lower">Lower Power band index</param>
        /// <param name="upper">Upper Power band index</param>
        /// <returns>The new upper band index which is now equal to the lower index if the upper index happens to be less than the lower. Else it returns the original upper index that was passed in</returns>
        private ushort CheckThatUpperPowerBandGreaterThanLowerPowerBand(ushort lower, ushort upper)
        {
            if (upper < lower)
            {
                upper = lower;
            }
            return upper;
        }
        /// <summary>
        /// Checks to make sure that the upper value minus the lower value is under 64 as per medtronic rules.
        /// </summary>
        /// <param name="lower">Lower power band value</param>
        /// <param name="upper">Upper power band value</param>
        /// <returns>Original upper if within range or lower + 63 if not within range.</returns>
        private ushort CheckThatUpperPowerBandLessLessThanSixtyFourFromLower(ushort lower, ushort upper)
        {
            ushort retVal = 0;
            //if within range, just return upper.
            //else return within range
            if ((upper - lower) < 64)
            {
                retVal = upper;
            }
            else
            {
                retVal = (ushort)(lower + 63);
            }
            return retVal;
        }

        /// <summary>
        /// Makes sure that upper power band is under certain number according to FFT size.
        /// </summary>
        /// <param name="upper">Upper Power band</param>
        /// <param name="FFT">FFT currently being used</param>
        /// <returns>True if powerband within range or false if not within range</returns>
        private bool CheckThatUpperPowerBandInRangePerFFT(ushort upper, int FFT)
        {
            bool isTrue = false;
            switch (FFT)
            {
                case 64:
                    if (upper < 32) { isTrue = true; }
                    break;
                case 256:
                    if (upper < 128) { isTrue = true; }
                    break;
                case 1024:
                    if (upper < 512) { isTrue = true; }
                    break;
                default:
                    isTrue = false;
                    break;
            }
            return isTrue;
        }

        /// <summary>
        /// Resets the POR bit if it was set
        /// </summary>
        /// <param name="theSummit">SummitSystem for the api call</param>
        private void ResetPOR(SummitSystem theSummit)
        {
            Messages.Add("POR was set, resetting...");
            try
            {
                // reset POR
                theSummit.ResetErrorFlags(Medtronic.NeuroStim.Olympus.DataTypes.Core.StatusBits.Por);

                // check battery
                BatteryStatusResult theStatus;
                theSummit.ReadBatteryLevel(out theStatus);

                // perform interrogate command and check if therapy is enabled.s
                GeneralInterrogateData interrogateBuffer;
                theSummit.ReadGeneralInfo(out interrogateBuffer);
                if (interrogateBuffer.IsTherapyUnavailable)
                {
                    Console.WriteLine("Therapy still unavailable after reset");
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("--ERROR: Resetting POR bit");
            }
        }

        /// <summary>
        /// Gets the path to the medtronic json files directory base on projectID, deviceID, and patientID
        /// Sets this path to filepath variable for use for later.
        /// This method is used in the constructor when creating the class
        /// </summary>
        private string GetDirectoryPathForCurrentSession(string projectID, string patientID, string deviceID)
        {
            string filepath = null;
            try
            {
                //This gets the directories in the summitData directory and sort it
                //This is because we want the most recent directory and directories in there are sorted by linux timestamp
                //once sorted, we can find the most recent one (last one) and return the name of that directory to add to the filepath
                if (appModel.BasePathToJSONFiles != null)
                {
                    string[] folders = Directory.GetDirectories(appModel.BasePathToJSONFiles + "\\SummitData\\" + projectID + "\\" + patientID + "\\");
                    Array.Sort(folders);
                    filepath = folders[folders.Length - 1] + "\\" + "Device" + deviceID;
                }
                else
                {
                    string[] folders = Directory.GetDirectories(@"C:\ProgramData\Medtronic ORCA\SummitData\" + projectID + "\\" + patientID + "\\");
                    Array.Sort(folders);
                    filepath = folders[folders.Length - 1] + "\\" + "Device" + deviceID;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                Messages.Add("--ERROR: Could not find directory path");
            }
            return filepath;
        }

        /// <summary>
        /// Builds the filepath for sense
        /// </summary>
        /// <param name="filepath">path to file</param>
        /// <param name="currIndex">Index of current switch run</param>
        /// <returns>full filepath</returns>
        private string AppendFilepathToSense(string filepath, int currIndex)
        {
            //prepend the version number to front with date and time
            string path = String.Concat(currIndex.ToString("000"), "_" + DateTime.Now.ToString("MM_dd_yyyy_hh_mm_ss_tt") + "_sense.json");
            //Add filepath found in constructor to beginning and path at end. ConfigLogFiles is a directory made inside medtronic directory
            path = filepath + "\\ConfigLogFiles\\" + path;
            return path;
        }
        /// <summary>
        /// Builds the filepath for adaptive
        /// </summary>
        /// <param name="filepath">path to file</param>
        /// <param name="currIndex">Index of current switch run</param>
        /// <returns>full filepath</returns>
        private string AppendFilepathToAdaptive(string filepath, int currIndex)
        {
            //prepend the version number to front with date and time
            string path = String.Concat(currIndex.ToString("000"), "_" + DateTime.Now.ToString("MM_dd_yyyy_hh_mm_ss_tt") + "_adaptive.json");
            //Add filepath found in constructor to beginning and path at end. ConfigLogFiles is a directory made inside medtronic directory
            path = filepath + "\\ConfigLogFiles\\" + path;
            return path;
        }
        /// <summary>
        /// Writes the sense config files to session directories for left
        /// </summary>
        /// <returns>true if success and false if unsuccessful</returns>
        private bool WriteLeftConfigFilesToDirectories()
        {
            string filepathAdaptive, filepathSense;
            JSONService jSONService = new JSONService(_log);
            //write just left config files to directory
            if (projectID != null && leftPatientID != null && leftDeviceID != null)
            {
                filepathSense = GetDirectoryPathForCurrentSession(projectID, leftPatientID, leftDeviceID);
                filepathAdaptive = filepathSense;
            }
            else
            {
                _log.Warn("Error finding filepath for directory left");
                DisplayMessageBox("Error finding filepath for directory... Please run switch again!");
                return false;
            }
            if (filepathAdaptive != null && filepathSense != null)
            {
                filepathAdaptive = AppendFilepathToAdaptive(filepathAdaptive, previousIndexLeft);
                filepathSense = AppendFilepathToSense(filepathSense, previousIndexLeft);
                jSONService.WriteModelBackToConfigFile(leftAdaptiveModel, filepathAdaptive);
                jSONService.WriteModelBackToConfigFile(leftSenseModel, filepathSense);
            }
            else
            {
                _log.Warn("Error finding filepath for directory left");
                DisplayMessageBox("Error finding filepath for directory... Please run switch again!");
                return false;
            }

            //write left config file back to directory with new index
            if (masterSwitchModelLeftDefault != null)
            {
                jSONService.WriteModelBackToConfigFile(masterSwitchModelLeftDefault, switchLeftDefaultFileLocation);
            }
            else
            {
                _log.Warn("Error writing file to directory left");
                DisplayMessageBox("Error writing file to directory... Please run switch again!");
                return false;
            }
            return true;
        }
        /// <summary>
        /// Writes the sense config files to session directories for right
        /// </summary>
        /// <returns>true if success and false if unsuccessful</returns>
        private bool WriteRightConfigFilesToDirecotries()
        {
            string filepathAdaptive, filepathSense;
            JSONService jSONService = new JSONService(_log);
            //write just left config files to directory
            if (projectID != null && rightPatientID != null && rightDeviceID != null)
            {
                filepathSense = GetDirectoryPathForCurrentSession(projectID, rightPatientID, rightDeviceID);
                filepathAdaptive = filepathSense;
            }
            else
            {
                _log.Warn("Error finding filepath for directory right");
                DisplayMessageBox("Error finding filepath for directory... Please run switch again!");
                return false;
            }
            if (filepathAdaptive != null && filepathSense != null)
            {
                filepathAdaptive = AppendFilepathToAdaptive(filepathAdaptive, previousIndexRight);
                filepathSense = AppendFilepathToSense(filepathSense, previousIndexRight);
                jSONService.WriteModelBackToConfigFile(rightAdaptiveModel, filepathAdaptive);
                jSONService.WriteModelBackToConfigFile(rightSenseModel, filepathSense);
            }
            else
            {
                _log.Warn("Error finding filepath for directory right");
                DisplayMessageBox("Error finding filepath for directory... Please run switch again!");
                return false;
            }

            //write left config file back to directory with new index
            if (masterSwitchModelRight != null)
            {
                jSONService.WriteModelBackToConfigFile(masterSwitchModelRight, switchRightFileLocation);
            }
            else
            {
                _log.Warn("Error writing file to directory right");
                DisplayMessageBox("Error writing file to directory... Please run switch again!");
                return false;
            }
            return true;
        }

        private void SendEmailToConfirm()
        {
            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");

                mail.From = new MailAddress("youremail@gmail.com");
                mail.To.Add("emailtosendto");
                mail.Subject = "Switch Success!";
                mail.Body = "Current index: " + previousIndexLeft;
                if (isBilateral)
                {
                    mail.Body += ".  Current index bilateral side: " + previousIndexRight;
                }
                
                SmtpServer.Port = 587;
                SmtpServer.UseDefaultCredentials = false;
                SmtpServer.Credentials = new System.Net.NetworkCredential("username", "password");
                SmtpServer.EnableSsl = true;

                SmtpServer.Send(mail);
                MessageBox.Show("mail Send");
            }
            catch (Exception e)
            {
                _log.Error(e);
                MessageBox.Show(e.ToString());
            }
        }
        #endregion

        /// <summary>
        /// Used to display messages to user
        /// </summary>
        public BindableCollection<string> Messages
        {
            get { return _message; }
            set { _message = value; }
        }
    }
}
