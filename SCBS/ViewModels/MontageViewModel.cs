using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.Therapy.Adaptive;
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
    class MontageViewModel : Screen
    {
        private readonly string MONTAGE_FILEPATH = @"C:\SCBS\Montage\montage_config.json";
        private readonly string MONTAGE_BASEFILEPATH = @"C:\SCBS\Montage\";
        private readonly string MONTAGE_FILETYPE = ".json";
        private readonly int TIME_BEFORE_BEGIN_END_MONTAGE = 5;
        private AppModel appModel = null;
        private MontageModel montageModel = null;
        private bool _isMontageEnabled;
        private SummitSystem theSummitLeft, theSummitRight;
        private volatile SummitSensing summitSensing;
        private bool isBilateral;
        private ILog _log;
        private JSONService jSONService;
        private string _instructionsTextBox;
        private static Thread leftThread, rightThread;
        private int _currentProgress = 0;
        private Visibility _progressVisibility = Visibility.Collapsed;
        private string _progressText = "";
        private string _montageTimeTextBox = "";
        private int timeLeftForMontage, totalTimeForMontage;
        private volatile bool _stopBothThreads = false;
        private volatile bool leftFinished = false;
        private volatile bool rightFinished = false;
        private List<SenseModel> montageSweepConfigListLeft = new List<SenseModel>();
        private List<SenseModel> montageSweepConfigListRight = new List<SenseModel>();
        private StimParameterModel stimParameterLeft, stimParameterRight;

        public MontageViewModel(ref SummitSystem theSummitLeft, ref SummitSystem theSummitRight, ILog _log, AppModel appModel)
        {
            this.theSummitLeft = theSummitLeft;
            this.theSummitRight = theSummitRight;
            this.isBilateral = appModel.Bilateral;
            this._log = _log;
            this.appModel = appModel;
            IsMontageEnabled = true;

            summitSensing = new SummitSensing(_log);
            jSONService = new JSONService(_log);
            stimParameterLeft = new StimParameterModel("", "", "", "", null);
            stimParameterRight = new StimParameterModel("", "", "", "", null);

            montageModel = jSONService.GetMontageModelFromFile(MONTAGE_FILEPATH);
            if(montageModel == null)
            {
                MessageBox.Show("The montage config file could not be read from the file. Please check that it exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            montageSweepConfigListLeft = montageSweepConfigListRight = LoadSenseJSONFilesForMontage(MONTAGE_BASEFILEPATH, MONTAGE_FILETYPE, montageModel);
            if(montageSweepConfigListLeft == null || montageSweepConfigListRight == null)
            {
                IsMontageEnabled = false;
                return;
            }

            //Get total time left and total time for montage. Display on progress bar
            timeLeftForMontage = totalTimeForMontage = GetTotalTimeForMontage(montageModel);
            TimeSpan time = TimeSpan.FromSeconds(timeLeftForMontage);
            //here backslash is must to tell that colon is
            //not the part of format, it just a character that we want in output
            string str = time.ToString(@"hh\:mm\:ss");
            MontageTimeTextBox = "Total Montage Time: " + str;
            InstructionsTextBox = montageModel?.Instructions;
        }

        #region Button Clicks
        public void RunMontageButtonClick()
        {
            IsMontageEnabled = false;
            ProgressVisibility = Visibility.Visible;

            TimeSpan time = TimeSpan.FromSeconds(timeLeftForMontage);
            //here backslash is must to tell that colon is
            //not the part of format, it just a character that we want in output
            string str = time.ToString(@"hh\:mm\:ss");
            ProgressText = str + " time left";

            try
            {
                theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Montage Sequence Begin", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
            }
            catch(Exception e)
            {
                _log.Error(e);
                MessageBox.Show("Could not log start event.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //Start default left thread
            leftThread = new Thread(new ThreadStart(MontageLeftThreadCode));
            leftThread.IsBackground = true;
            leftThread.Start();
            //Start right thread if bilateral
            if (isBilateral)
            {
                try
                {
                    theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Montage Sequence Begin", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    MessageBox.Show("Could not log start event.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                rightThread = new Thread(new ThreadStart(MontageRightThreadCode));
                rightThread.IsBackground = true;
                rightThread.Start();
            }
            else
            {
                rightFinished = true;
            }
        }

        public void CancelMontageButtonClick()
        {
            _stopBothThreads = true;
            TryClose();
        }
        #endregion

        #region Thread code
        private void MontageLeftThreadCode()
        {
            APIReturnInfo leftBufferReturnInfo;
            int counter;
            if (theSummitLeft == null)
            {
                MessageBox.Show("The summit system is null.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _stopBothThreads = true;
            }
            if (_stopBothThreads)
            {
                return;
            }
            try
            {
                if (theSummitLeft.IsDisposed)
                {
                    MessageBox.Show("The summit system is disposed.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _stopBothThreads = true;
                }
            }
            catch(Exception e)
            {
                _log.Error(e);
                _stopBothThreads = true;
            }
            
            if (_stopBothThreads)
            {
                return;
            }
            try
            {
                //Make sure embedded therapy is turned off while setting up parameters
                //try it 5 times before error out
                counter = 5;
                while(counter > 0)
                {
                    leftBufferReturnInfo = theSummitLeft.WriteAdaptiveMode(AdaptiveTherapyModes.Disabled);
                    if (leftBufferReturnInfo.RejectCode != 0)
                    {
                        counter--;
                        Thread.Sleep(300);
                    }
                    else
                    {
                        break;
                    }
                }
                if(counter == 0)
                {
                    MessageBox.Show("Could not turn off adaptive therapy.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _stopBothThreads = true;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not turn off adaptive therapy.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                _stopBothThreads = true;
            }
            if (_stopBothThreads)
            {
                return;
            }

            //Check if therapy active. If so, then check if 0-3 contacts stimming
            //if so, then change lfp1 and lfp2 to 100 for each time domain 0 and 1
            bool shouldChangeLfp1andLfp2to100ForLower = false;
            bool shouldChangeLfp1andLfp2to100ForUpper = false;
            SummitStimulationInfo stimInfoLeft = new SummitStimulationInfo(_log);
            if (stimInfoLeft.GetTherapyStatus(ref theSummitLeft).Equals("TherapyActive"))
            {
                StimParameterModel localStimModel = new StimParameterModel("", "", "", "", null);
                localStimModel = stimInfoLeft.GetStimParamsBasedOnGroup(theSummitLeft, stimInfoLeft.GetActiveGroup(ref theSummitLeft), 0);
                for (int i = 0; i < 4; i++)
                {
                    if (!localStimModel.TherapyElectrodes[i].IsOff)
                    {
                        shouldChangeLfp1andLfp2to100ForLower = true;
                    }
                }
                for (int i = 8; i < 12; i++)
                {
                    if (!localStimModel.TherapyElectrodes[i].IsOff)
                    {
                        shouldChangeLfp1andLfp2to100ForUpper = true;
                    }
                }
            }



            int montageIndex = 0;
            foreach (SenseModel localSenseModel in montageSweepConfigListLeft)
            {
                if (shouldChangeLfp1andLfp2to100ForLower)
                {
                    localSenseModel.Sense.TimeDomains[0].Lpf1 = 100;
                    localSenseModel.Sense.TimeDomains[0].Lpf2 = 100;
                    localSenseModel.Sense.TimeDomains[1].Lpf1 = 100;
                    localSenseModel.Sense.TimeDomains[1].Lpf2 = 100;
                }
                if (shouldChangeLfp1andLfp2to100ForUpper)
                {
                    localSenseModel.Sense.TimeDomains[2].Lpf1 = 100;
                    localSenseModel.Sense.TimeDomains[2].Lpf2 = 100;
                    localSenseModel.Sense.TimeDomains[3].Lpf1 = 100;
                    localSenseModel.Sense.TimeDomains[3].Lpf2 = 100;
                }
                if (_stopBothThreads)
                {
                    return;
                }
                //stop/configure sensing. Try for 5 times before error out
                counter = 5;
                while (counter > 0)
                {
                    if (summitSensing.SummitConfigureSensing(theSummitLeft, localSenseModel, false))
                    {
                        break;
                    }
                    else
                    {
                        counter--;
                        Thread.Sleep(300);
                    }
                    if (_stopBothThreads)
                    {
                        return;
                    }
                }
                if (counter == 0)
                {
                    MessageBox.Show("Could not configure sensing.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _stopBothThreads = true;
                }
                if (_stopBothThreads)
                {
                    return;
                }
                //start sensing. Try for 5 times before error out
                counter = 5;
                while (counter > 0)
                {
                    if (summitSensing.StartSensingAndStreaming(theSummitLeft, localSenseModel, false))
                    {
                        break;
                    }
                    else
                    {
                        counter--;
                        Thread.Sleep(300);
                    }
                    if (_stopBothThreads)
                    {
                        return;
                    }
                }
                if (counter == 0)
                {
                    MessageBox.Show("Could not start sensing.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _stopBothThreads = true;
                }
                if (_stopBothThreads)
                {
                    return;
                }

                //Set timer to run for timeToRun amount
                int timeToRunForCurrentMontage = montageModel.MontageFiles[montageIndex].TimeToRunInSeconds;
                int timeMarkerToAddEvent = timeToRunForCurrentMontage - TIME_BEFORE_BEGIN_END_MONTAGE;
                bool startTimeHasRun = false;
                bool stopTimeHasRun = false;
                while (timeToRunForCurrentMontage > 0)
                {
                    if (timeToRunForCurrentMontage == timeMarkerToAddEvent && !startTimeHasRun)
                    {
                        try
                        {
                            //try once and if fails then try one more time
                            leftBufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Start : " + montageModel.MontageFiles[montageIndex].Filename, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            if(leftBufferReturnInfo.RejectCode == 0)
                            {
                                startTimeHasRun = true;
                            }
                            else
                            {
                                leftBufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Start : " + montageModel.MontageFiles[montageIndex].Filename, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                                startTimeHasRun = true;
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (timeToRunForCurrentMontage == TIME_BEFORE_BEGIN_END_MONTAGE && !stopTimeHasRun)
                    {
                        try
                        {
                            //try once and if fails then try one more time
                            leftBufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Stop : " + montageModel.MontageFiles[montageIndex].Filename, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            if (leftBufferReturnInfo.RejectCode == 0)
                            {
                                stopTimeHasRun = true;
                            }
                            else
                            {
                                leftBufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Stop : " + montageModel.MontageFiles[montageIndex].Filename, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                                stopTimeHasRun = true;
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (_stopBothThreads)
                    {
                        return;
                    }
                    timeToRunForCurrentMontage--;
                    int precentageDoneForTotalMontage = 100 - (int)Math.Round((double)(100 * timeLeftForMontage) / totalTimeForMontage);
                    timeLeftForMontage--;
                    CurrentProgress = precentageDoneForTotalMontage;
                    TimeSpan time = TimeSpan.FromSeconds(timeLeftForMontage);
                    //here backslash is must to tell that colon is
                    //not the part of format, it just a character that we want in output
                    string str = time.ToString(@"hh\:mm\:ss");
                    ProgressText = str + " time left";
                    Thread.Sleep(1000);
                }
                ProgressText = "Loading next montage file...";
                montageIndex++;
            }
            ProgressText = "Finishing up...";
            leftFinished = true;
            while (true)
            {
                if (rightFinished)
                {
                    try
                    {
                        theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Montage Stop", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                        if (isBilateral)
                        {
                            theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Montage Stop", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        MessageBox.Show("Could not log stop event.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    ProgressText = "Success";
                    MessageBox.Show("Montage Successful. Report Screen will open after clicking OK.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    CancelMontageButtonClick();
                    break;
                }
            }
        }

        private void MontageRightThreadCode()
        {
            APIReturnInfo rightBufferReturnInfo;
            int counter;
            if (theSummitRight == null)
            {
                MessageBox.Show("The summit system is null.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _stopBothThreads = true;
            }
            if (_stopBothThreads)
            {
                return;
            }
            if (theSummitRight.IsDisposed)
            {
                MessageBox.Show("The summit system is disposed.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _stopBothThreads = true;
            }
            if (_stopBothThreads)
            {
                return;
            }
            try
            {
                //Make sure embedded therapy is turned off while setting up parameters
                //try it 5 times before error out
                counter = 5;
                while (counter > 0)
                {
                    //Make sure embedded therapy is turned off while setting up parameters
                    rightBufferReturnInfo = theSummitRight.WriteAdaptiveMode(AdaptiveTherapyModes.Disabled);
                    if (rightBufferReturnInfo.RejectCode != 0)
                    {
                        counter--;
                        Thread.Sleep(300);
                    }
                    else
                    {
                        break;
                    }
                    if (_stopBothThreads)
                    {
                        return;
                    }
                }
                if (counter == 0)
                {
                    MessageBox.Show("Could not turn off adaptive therapy.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _stopBothThreads = true;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not turn off adaptive therapy.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                _stopBothThreads = true;
            }
            if (_stopBothThreads)
            {
                return;
            }

            //Check if therapy active. If so, then check if 0-3 contacts stimming
            //if so, then change lfp1 and lfp2 to 100 for each time domain 0 and 1
            bool shouldChangeLfp1andLfp2to100 = false;
            bool shouldChangeLfp1andLfp2to100ForUpper = false;
            SummitStimulationInfo stimInfoRight = new SummitStimulationInfo(_log);
            if (stimInfoRight.GetTherapyStatus(ref theSummitRight).Equals("TherapyActive"))
            {
                StimParameterModel localStimModel = new StimParameterModel("", "", "", "", null);
                localStimModel = stimInfoRight.GetStimParamsBasedOnGroup(theSummitRight, stimInfoRight.GetActiveGroup(ref theSummitRight), 0);
                for (int i = 0; i < 4; i++)
                {
                    if (!localStimModel.TherapyElectrodes[i].IsOff)
                    {
                        shouldChangeLfp1andLfp2to100 = true;
                    }
                }
                for (int i = 8; i < 12; i++)
                {
                    if (!localStimModel.TherapyElectrodes[i].IsOff)
                    {
                        shouldChangeLfp1andLfp2to100ForUpper = true;
                    }
                }
            }

            int montageIndex = 0;
            foreach (SenseModel localSenseModel in montageSweepConfigListRight)
            {
                if (shouldChangeLfp1andLfp2to100)
                {
                    localSenseModel.Sense.TimeDomains[0].Lpf1 = 100;
                    localSenseModel.Sense.TimeDomains[0].Lpf2 = 100;
                    localSenseModel.Sense.TimeDomains[1].Lpf1 = 100;
                    localSenseModel.Sense.TimeDomains[1].Lpf2 = 100;
                }
                if (shouldChangeLfp1andLfp2to100ForUpper)
                {
                    localSenseModel.Sense.TimeDomains[2].Lpf1 = 100;
                    localSenseModel.Sense.TimeDomains[2].Lpf2 = 100;
                    localSenseModel.Sense.TimeDomains[3].Lpf1 = 100;
                    localSenseModel.Sense.TimeDomains[3].Lpf2 = 100;
                }
                if (_stopBothThreads)
                {
                    return;
                }
                //stop/configure sensing. Try for 5 times before error out
                counter = 5;
                while (counter > 0)
                {
                    if (summitSensing.SummitConfigureSensing(theSummitRight, localSenseModel, false))
                    {
                        break;
                    }
                    else
                    {
                        counter--;
                        Thread.Sleep(300);
                    }
                    if (_stopBothThreads)
                    {
                        return;
                    }
                }
                if (counter == 0)
                {
                    MessageBox.Show("Could not configure sensing.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _stopBothThreads = true;
                }
                if (_stopBothThreads)
                {
                    return;
                }
                //start sensing. Try for 5 times before error out
                counter = 5;
                while (counter > 0)
                {
                    if (summitSensing.StartSensingAndStreaming(theSummitRight, localSenseModel, false))
                    {
                        break;
                    }
                    else
                    {
                        counter--;
                        Thread.Sleep(300);
                    }
                    if (_stopBothThreads)
                    {
                        return;
                    }
                }
                if (counter == 0)
                {
                    MessageBox.Show("Could not start sensing.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _stopBothThreads = true;
                }
                if (_stopBothThreads)
                {
                    return;
                }

                //Set timer to run for timeToRun amount
                int timeToRunForCurrentMontage = montageModel.MontageFiles[montageIndex].TimeToRunInSeconds;
                int timeMarkerToAddEvent = timeToRunForCurrentMontage - TIME_BEFORE_BEGIN_END_MONTAGE;
                bool startTimeHasRun = false;
                bool stopTimeHasRun = false;
                while (timeToRunForCurrentMontage > 0)
                {
                    if (timeMarkerToAddEvent == timeToRunForCurrentMontage && !startTimeHasRun)
                    {
                        try
                        {
                            //try once and if fails then try one more time
                            rightBufferReturnInfo = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Start : " + montageModel.MontageFiles[montageIndex].Filename, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            if (rightBufferReturnInfo.RejectCode == 0)
                            {
                                startTimeHasRun = true;
                            }
                            else
                            {
                                rightBufferReturnInfo = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Start : " + montageModel.MontageFiles[montageIndex].Filename, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                                startTimeHasRun = true;
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (timeToRunForCurrentMontage == TIME_BEFORE_BEGIN_END_MONTAGE && !stopTimeHasRun)
                    {
                        try
                        {
                            //try once and if fails then try one more time
                            rightBufferReturnInfo = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Stop : " + montageModel.MontageFiles[montageIndex].Filename, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            if (rightBufferReturnInfo.RejectCode == 0)
                            {
                                stopTimeHasRun = true;
                            }
                            else
                            {
                                rightBufferReturnInfo = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Stop : " + montageModel.MontageFiles[montageIndex].Filename, DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                                stopTimeHasRun = true;
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (_stopBothThreads)
                    {
                        return;
                    }
                    timeToRunForCurrentMontage--;
                    Thread.Sleep(1000);
                }
                montageIndex++;
            }
            rightFinished = true;
            while (true)
            {
                if (leftFinished)
                {
                    break;
                }
            }
        }
        #endregion

        #region Helper functions
        /// <summary>
        /// Reads the config file and gets the total amount of time to run 
        /// </summary>
        /// <returns>time in seconds for total montage</returns>
        private int GetTotalTimeForMontage(MontageModel localMontageModel)
        {
            //Go through the files
            //Add the total amount of time for the whole thing and save in local variable to get use later
            int totalTimeForMontage = 0;
            foreach (MontageFile fileInfo in localMontageModel.MontageFiles)
            {
                totalTimeForMontage += fileInfo.TimeToRunInSeconds;
            }
            return totalTimeForMontage;
        }
        /// <summary>
        /// Method that loads all of the sense config files based on what was loaded from montage config file
        /// </summary>
        /// <returns>List of sense models if successful and null if unsuccessful</returns>
        private List<SenseModel> LoadSenseJSONFilesForMontage(string baseFilepath, string filetype, MontageModel localMontageModel)
        {
            List<SenseModel> localList = new List<SenseModel>();
            foreach (MontageFile montageFileObject in localMontageModel.MontageFiles)
            {
                string filepath = baseFilepath + montageFileObject.Filename + filetype;
                SenseModel tempModel = jSONService.GetSenseModelFromFile(filepath);
                if (tempModel != null)
                {
                    localList.Add(tempModel);
                }
                else
                {
                    MessageBox.Show("The sense config file: " + montageFileObject.Filename + " could not be loaded up correctly.  Please try montage again", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
            return localList;
        }
        #endregion

        #region UI Bindings
        public string MontageTimeTextBox
        {
            get { return _montageTimeTextBox; }
            set
            {
                _montageTimeTextBox = value;
                NotifyOfPropertyChange(() => MontageTimeTextBox);
            }
        }
        public bool IsMontageEnabled
        {
            get { return _isMontageEnabled; }
            set
            {
                _isMontageEnabled = value;
                NotifyOfPropertyChange(() => IsMontageEnabled);
            }
        }

        public string InstructionsTextBox
        {
            get { return _instructionsTextBox; }
            set
            {
                _instructionsTextBox = value;
                NotifyOfPropertyChange(() => InstructionsTextBox);
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
    }
}
