using Caliburn.Micro;
using Medtronic.SummitAPI.Classes;
using SCBS.Models;
using SCBS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Threading;
using Medtronic.NeuroStim.Olympus.DataTypes.Sensing;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using System.Windows.Threading;
using System.Diagnostics;
using NAudio.Wave;
using System.Reflection;
using Medtronic.NeuroStim.Olympus.DataTypes.Core.DataManagement;
using log4net.Config;

namespace SCBS.ViewModels
{
    /// <summary>
    /// Main Class that contains window closing and many UI bindings
    /// </summary>
    public partial class MainViewModel : Screen
    {
        #region MainView Variables
        //logger
        private static ILog _log = LogManager.GetLog(typeof(MainViewModel));
        //location of sense files
        private static readonly string senseLeftFileLocation = @"C:\SCBS\senseLeft_config.json";
        private static readonly string senseRightFileLocation = @"C:\SCBS\senseRight_config.json";
        private static readonly string applicationFileLocation = @"C:\SCBS\application_config.json";
        private static readonly string PROJECT_ID = "SummitContinuousBilateralStreaming";
        private static Thread workerThreadLeft;
        private static Thread workerThreadRight;
        private static Thread alignThread;
        private static Thread downloadLogThreadLeftUni;
        private static Thread downloadLogThreadRight;
        //Variable to stop worker thread when window is closing. Cleaner than just aborting thread, but aborting happens anyway
        private static volatile bool _shouldStopWorkerThread = false;
        private volatile static SummitManager theSummitManager = null;
        private JSONService jSONService;
        private static SenseModel senseLeftConfigModel = null;
        private static SenseModel senseRightConfigModel = null;
        private static AppModel appConfigModel = null;
        //Variables for writing an event for a beep
        private WaveIn waveIn;
        private int signalOnValue = 10;
        private bool previousOnFlag = false;
        private bool currentOnFlag = false;
        private int beepLogCounterForText = 5;
        //UI variables
        private static bool CanConnect = true;
        private static bool connectButtonEnabled = true;
        private static bool alignButtonEnabled = false;
        private static bool isBilateral = false;
        private static bool _isSwitchVisible = false;
        private static bool _isAlignVisible = false;
        //stim display settings
        private string _applicationTitleText;
        private WindowStyle _windowStyleForMainWindow = WindowStyle.None;
        private static bool _visibilityStimGroupLeftUni = true;
        private static bool _visibilityStimAmpLeftUni = true;
        private static bool _visibilityStimRateLeftUni = true;
        private static bool _visibilityStimContactsLeftUni = true;
        private static bool _visibilityStimTherapyOnOffLeftUni = true;
        private static bool _visibilityStimAdaptiveOnLeftUni = true;
        private static bool _visibilityStimGroupRight = true;
        private static bool _visibilityStimAmpRight = true;
        private static bool _visibilityStimRateRight = true;
        private static bool _visibilityStimContactsRight = true;
        private static bool _visibilityStimTherapyOnOffRight = true;
        private static bool _visibilityStimAdaptiveOnRight = true;
        private static bool _isSpinnerVisible = false;
        private static bool _reportButtonVisible = false;
        private string _connectButtonText;
        private string _downloadLogButtonText;
        private Brush _connectButtonColor, _stimStateLeftTextColor, _stimStateRightTextColor;
        private Brush _borderCTMLeftBackground;
        private Brush _borderCTMRightBackground;
        private Brush _borderINSLeftBackground;
        private Brush _borderINSRightBackground;
        private Brush _borderStreamLeftBackground;
        private Brush _borderStreamRightBackground;
        private string _CTMLeftBatteryLevel;
        private string _CTMRightBatteryLevel;
        private string _INSLeftBatteryLevel;
        private string _INSRightBatteryLevel;
        private string _activeGroupLeft;
        private string _activeGroupRight;
        private string _stimRateLeft;
        private string _stimRateRight;
        private string _stimAmpLeft, _stimAmpRight, _stimElectrodeLeft, _stimElectrodeRight;
        private string _stimStateLeft;
        private string _stimStateRight;
        private string _adaptiveRunningRight;
        private string _adaptiveRunningLeft;
        private string _ctmLeftText, _insLeftText, _streamLeftText;
        private DispatcherTimer dispatcherTimerLeft = new DispatcherTimer();
        private Stopwatch stopWatchLeft = new Stopwatch();
        private string currentTimeLeft = string.Empty;
        private string _stopWatchTimeLeft = "";
        private DispatcherTimer dispatcherTimerRight = new DispatcherTimer();
        private Stopwatch stopWatchRight = new Stopwatch();
        private string currentTimeRight = string.Empty;
        private string _stopWatchTimeRight = "";
        private string _laptopBatteryLevel = "";
        private int _currentProgress = 0;
        private Visibility _progressVisibility = Visibility.Collapsed;
        private Visibility _tabVisibility = Visibility.Collapsed;
        private Visibility _beepEnabledVisibility = Visibility.Collapsed;
        private string _progressText = "";
        private bool _webPageOneButtonEnabled, _webPageTwoButtonEnabled, _montageButtonEnabled, _stimSweepButtonEnabled, 
            _newSessionButtonEnabled, _moveGroupButtonEnabled, _downloadLogButtonVisible, _patientStimControlButtonVisible;
        private string _webPageOneButtonText = "";
        private string _webPageTwoButtonText = "";
        private string _moveGroupButtonText = "";
        private string _beepLoggedRight, _beepLoggedLeft;
        private string versionNumber = "";
        private volatile bool alignSuccessShown = false;
        private volatile bool rightLogDownloadFinished = false; 
        private volatile bool leftLogDownloadFinished = false;
        #endregion
        #region ResearcherToolsViewModel Variables

        #endregion
        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            #region MainViewModel Constructor
            jSONService = new JSONService(_log);
            summitSensing = new SummitSensing(_log);
            stimInfoRight = new SummitStimulationInfo(_log);
            stimInfoLeft = new SummitStimulationInfo(_log);
            //Load the application model. This will determine what UI elements to show and if bilateral
            appConfigModel = jSONService?.GetApplicationModelFromFile(applicationFileLocation);
            //Application config if required. If user is missing it (hence null) then they can't move on
            if (appConfigModel == null)
            {
                return;
            }

            if (appConfigModel.BasePathToJSONFiles != null && Directory.Exists(appConfigModel.BasePathToJSONFiles))
            {
                log4net.GlobalContext.Properties["LogFileName"] = appConfigModel.BasePathToJSONFiles; //log file path 
            }
            else
            {
                log4net.GlobalContext.Properties["LogFileName"] = "C:\\SCBS\\"; //log file path 
            }
            
            log4net.Config.XmlConfigurator.Configure();

            //Load the left_default sense config file. This will always load even in unilateral case
            senseLeftConfigModel = jSONService?.GetSenseModelFromFile(senseLeftFileLocation);
            if (senseLeftConfigModel == null)
            {
                return;
            }
            

            //If bilateral, show right side data on UI and load right side config file
            if (appConfigModel.Bilateral)
            {
                //Set UI visibility to false
                IsBilateral = true;
                CTMLeftText = "CTM (L)";
                INSLeftText = "INS (L)";
                StreamLeftText = "Stream (L)";
                //Load right sense config file
                senseRightConfigModel = jSONService?.GetSenseModelFromFile(senseRightFileLocation);
                if (senseRightConfigModel == null)
                {
                    return;
                }
            }

            if (appConfigModel.PatientStimControl)
            {
                PatientStimControlButtonVisible = true;
            }

            //Show switch button and hide stim data such as amp and frequency. This is so patient doesn't know what amp or hz they are at
            if (appConfigModel.Switch)
            {
                IsSwitchVisible = true;
            }

            //Show the align button
            if (appConfigModel.Align)
            {
                IsAlignVisible = true;
            }

            //Show the montage button
            if (appConfigModel.Montage)
            {
                MontageButtonEnabled = true;
            }

            //Show the stim sweep button
            if (appConfigModel.StimSweep)
            {
                StimSweepButtonEnabled = true;
            }

            //Show the new session button
            if (appConfigModel.NewSession)
            {
                NewSessionButtonEnabled = true;
            }

            if (appConfigModel.HideReportButton)
            {
                ReportButtonVisible = false;
            }
            else
            {
                ReportButtonVisible = true;
            }

            if(appConfigModel.WebPageButtons != null)
            {
                if (appConfigModel.WebPageButtons.WebPageOneButtonEnabled)
                {
                    WebPageOneButtonEnabled = true;
                    WebPageOneButtonText = appConfigModel.WebPageButtons?.WebPageOneButtonText;
                }
                if (appConfigModel.WebPageButtons.WebPageTwoButtonEnabled)
                {
                    WebPageTwoButtonEnabled = true;
                    WebPageTwoButtonText = appConfigModel.WebPageButtons?.WebPageTwoButtonText;
                }
            }
            //move group button
            if(appConfigModel.MoveGroupButton != null)
            {
                if (appConfigModel.MoveGroupButton.MoveGroupButtonEnabled)
                {
                    MoveGroupButtonEnabled = true;
                    MoveGroupButtonText = appConfigModel.MoveGroupButton.MoveGroupButtonText;
                }
            }
            //mirror, event and app log download button
            if (appConfigModel.LogDownloadButton != null)
            {
                if (appConfigModel.LogDownloadButton.LogDownloadButtonEnabled)
                {
                    DownloadLogButtonVisible = true;
                    DownloadLogButtonText = appConfigModel.LogDownloadButton.LogDownloadButtonText;
                }
            }

            //Initialize to listen for a beep noise.
            if (appConfigModel.LogBeepEvent)
            {
                BeepEnabledVisibility = Visibility.Visible;
                try
                {
                    waveIn = new WaveIn();
                    waveIn.DataAvailable += new EventHandler<WaveInEventArgs>(WaveIn_data);
                    waveIn.WaveFormat = new WaveFormat(48000, 8, 1);
                    waveIn.BufferMilliseconds = 20;
                    waveIn.NumberOfBuffers = 2;
                    waveIn.StartRecording();
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }

            if (appConfigModel.StimDisplaySettings != null)
            {
                if (appConfigModel.StimDisplaySettings.LeftUnilateralSettings != null)
                {
                    if (appConfigModel.StimDisplaySettings.LeftUnilateralSettings.HideGroup)
                    {
                        VisibilityStimGroupLeftUni = false;
                    }
                    if (appConfigModel.StimDisplaySettings.LeftUnilateralSettings.HideAmp)
                    {
                        VisibilityStimAmpLeftUni = false;
                    }
                    if (appConfigModel.StimDisplaySettings.LeftUnilateralSettings.HideRate)
                    {
                        VisibilityStimRateLeftUni = false;
                    }
                    if (appConfigModel.StimDisplaySettings.LeftUnilateralSettings.HideStimContacts)
                    {
                        VisibilityStimContactsLeftUni = false;
                    }
                    if (appConfigModel.StimDisplaySettings.LeftUnilateralSettings.HideTherapyOnOff)
                    {
                        VisibilityStimTherapyOnOffLeftUni = false;
                    }
                    if (appConfigModel.StimDisplaySettings.LeftUnilateralSettings.HideAdaptiveOn)
                    {
                        VisibilityStimAdaptiveOnLeftUni = false;
                    }
                }
                if (appConfigModel.StimDisplaySettings.RightSettings != null)
                {
                    if (appConfigModel.StimDisplaySettings.RightSettings.HideGroup)
                    {
                        VisibilityStimGroupRight = false;
                    }
                    if (appConfigModel.StimDisplaySettings.RightSettings.HideAmp)
                    {
                        VisibilityStimAmpRight = false;
                    }
                    if (appConfigModel.StimDisplaySettings.RightSettings.HideRate)
                    {
                        VisibilityStimRateRight = false;
                    }
                    if (appConfigModel.StimDisplaySettings.RightSettings.HideStimContacts)
                    {
                        VisibilityStimContactsRight = false;
                    }
                    if (appConfigModel.StimDisplaySettings.RightSettings.HideTherapyOnOff)
                    {
                        VisibilityStimTherapyOnOffRight = false;
                    }
                    if (appConfigModel.StimDisplaySettings.RightSettings.HideAdaptiveOn)
                    {
                        VisibilityStimAdaptiveOnRight = false;
                    }
                }
                if (appConfigModel.TurnOnResearcherTools)
                {
                    TabVisibility = Visibility.Visible;
                    WindowStyleForMainWindow = WindowStyle.ThreeDBorderWindow;
                }
            }

            //Setup Stop watch to show user how long they are streaming for.
            dispatcherTimerLeft.Tick += new EventHandler(dt_TickLeft);
            dispatcherTimerLeft.Interval = new TimeSpan(0, 0, 0, 1);
            dispatcherTimerRight.Tick += new EventHandler(dt_TickRight);
            dispatcherTimerRight.Interval = new TimeSpan(0, 0, 0, 1);

            //version number
            Assembly assem = Assembly.GetExecutingAssembly();
            AssemblyName assemName = assem.GetName();
            Version ver = assemName.Version;
            versionNumber = "SCBS Version number: " + ver;

            //Application name and version
            ApplicationTitleText = "Summit Continuous Bilateral Streaming (SCBS) Application. Version " + ver;
            #endregion

            #region ResearcherTools Constructor
            ProgramOptionsLeft.Add(program0Option);
            ProgramOptionsLeft.Add(program1Option);
            ProgramOptionsLeft.Add(program2Option);
            ProgramOptionsLeft.Add(program3Option);
            SelectedProgramLeft = program0Option;
            ProgramOptionsRight.Add(program0Option);
            ProgramOptionsRight.Add(program1Option);
            ProgramOptionsRight.Add(program2Option);
            ProgramOptionsRight.Add(program3Option);
            SelectedProgramRight = program0Option;

            DeviceOptions.Add(leftUnilateralDeviceOption);
            SelectedDevice = leftUnilateralDeviceOption;
            if (isBilateral)
            {
                DeviceOptions.Add(rightDeviceOption);
                DeviceOptions.Add(bothDeviceOption);
            }
            dispatcherChangeTimer.Tick += new EventHandler(AmpChangeTimer);
            dispatcherChangeTimer.Interval = new TimeSpan(0, 0, 0, 1);
            #endregion
        }


        #region Button Clicks
        /// <summary>
        /// Opens window for patient stim change
        /// </summary>
        public void AdjustLeftPatientStim()
        {
            if (theSummitLeft == null || theSummitLeft.IsDisposed)
            {
                return;
            }
            WindowManager window = new WindowManager();
            window.ShowDialog(new PatientStimControlViewModel(theSummitLeft, true, senseLeftConfigModel, _log), null, null);
            UpdateStimStatusGroupLeft();
        }
        /// <summary>
        /// Opens window for patient stim change
        /// </summary>
        public void AdjustRightPatientStim()
        {
            if (theSummitRight == null || theSummitRight.IsDisposed)
            {
                return;
            }
            WindowManager window = new WindowManager();
            window.ShowDialog(new PatientStimControlViewModel(theSummitRight, false, senseRightConfigModel, _log), null, null);
            UpdateStimStatusGroupRight();
        }
        /// <summary>
        /// Downloads the mirror and application log files
        /// </summary>
        public void DownloadLogButtonClick()
        {
            //Check if connected
            if (CanConnect)
            {
                return;
            }
            //Run new thread!!!
            ProgressVisibility = Visibility.Visible;
            CurrentProgress = 0;
            ProgressText = "Downloading Logs...";
            if (!IsBilateral && theSummitLeft != null && !theSummitLeft.IsDisposed)
            {
                rightLogDownloadFinished = true;
                leftLogDownloadFinished = false;
                downloadLogThreadLeftUni = new Thread(new ThreadStart(DownloadLogThreadCodeLeftUnilateral));
                downloadLogThreadLeftUni.IsBackground = true;
                downloadLogThreadLeftUni.Start();
            }
            else if (IsBilateral && theSummitLeft != null && !theSummitLeft.IsDisposed && theSummitRight != null && !theSummitRight.IsDisposed)
            {
                if (!CanConnect)
                {
                    rightLogDownloadFinished = false;
                    leftLogDownloadFinished = false;
                    downloadLogThreadLeftUni = new Thread(new ThreadStart(DownloadLogThreadCodeLeftUnilateral));
                    downloadLogThreadLeftUni.IsBackground = true;
                    downloadLogThreadLeftUni.Start();
                    downloadLogThreadRight = new Thread(new ThreadStart(DownloadLogThreadCodeRight));
                    downloadLogThreadRight.IsBackground = true;
                    downloadLogThreadRight.Start();
                }
            }
            else
            {
                ProgressVisibility = Visibility.Hidden;
            }
            
        }
        private void DownloadLogThreadCodeLeftUnilateral()
        {
            string logPathLeftUni = "";
            //Create filepath for left or unilateral
            logPathLeftUni = GetDirectoryPathForCurrentSession(theSummitLeft, PROJECT_ID, leftPatientID, leftDeviceID);
            logPathLeftUni += @"\LogDataFromLeftUnilateralINS\";
            //Get app log data
            if (appConfigModel.LogDownloadButton != null && appConfigModel.LogDownloadButton.LogTypesToDownload != null)
            {
                CurrentProgress = 15;
                if (appConfigModel.LogDownloadButton.LogTypesToDownload.ApplicationLog)
                {
                    if(!GetApplicationLogInfo(theSummitLeft, logPathLeftUni, FlashLogTypes.Application, "AppLog"))
                    {
                        MessageBox.Show("Could not Log Application Data. Please retry. If the problem persists, please power off your ctm and power back on and try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                        while (!rightLogDownloadFinished)
                        {
                            Thread.Sleep(300);
                        }
                        ProgressVisibility = Visibility.Hidden;
                        return;
                    }
                }
                CurrentProgress = 60;
                if (appConfigModel.LogDownloadButton.LogTypesToDownload.EventLog)
                {
                    if(!GetApplicationLogInfo(theSummitLeft, logPathLeftUni, FlashLogTypes.Event, "EventLog"))
                    {
                        MessageBox.Show("Could not Log Event Data. Please retry. If the problem persists, please power off your ctm and power back on and try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                        while (!rightLogDownloadFinished)
                        {
                            Thread.Sleep(300);
                        }
                        ProgressVisibility = Visibility.Hidden;
                        return;
                    }
                }
                CurrentProgress = 80;
                if (appConfigModel.LogDownloadButton.LogTypesToDownload.MirrorLog)
                {
                    if(!GetApplicationMirrorData(theSummitLeft, logPathLeftUni, appConfigModel))
                    {
                        MessageBox.Show("Could not Log Mirror Data. Please retry. If the problem persists, please power off your ctm and power back on and try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                        while (!rightLogDownloadFinished)
                        {
                            Thread.Sleep(300);
                        }
                        ProgressVisibility = Visibility.Hidden;
                        return;
                    }
                }
                CurrentProgress = 100;
            }
            while (!rightLogDownloadFinished)
            {
                Thread.Sleep(300);
            }
            ProgressVisibility = Visibility.Hidden;
        }
        private void DownloadLogThreadCodeRight()
        {
            if (!IsBilateral)
            {
                return;
            }
            string logPathRight = "";
            //Create filepath for right
            logPathRight = GetDirectoryPathForCurrentSession(theSummitRight, PROJECT_ID, rightPatientID, rightDeviceID);
            logPathRight += @"\LogDataFromRightINS\";
            //Get app log data
            if (appConfigModel.LogDownloadButton != null && appConfigModel.LogDownloadButton.LogTypesToDownload != null)
            {
                if (appConfigModel.LogDownloadButton.LogTypesToDownload.ApplicationLog)
                {
                    if(!GetApplicationLogInfo(theSummitRight, logPathRight, FlashLogTypes.Application, "AppLog"))
                    {
                        MessageBox.Show("Could not Log Application Data. Please retry. If the problem persists, please power off your ctm and power back on and try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                        rightLogDownloadFinished = true;
                        return;
                    }
                }
                if (appConfigModel.LogDownloadButton.LogTypesToDownload.EventLog)
                {
                    if(!GetApplicationLogInfo(theSummitRight, logPathRight, FlashLogTypes.Event, "EventLog"))
                    {
                        MessageBox.Show("Could not Log Event Data. Please retry. If the problem persists, please power off your ctm and power back on and try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                        rightLogDownloadFinished = true;
                        return;
                    }
                }
                if (appConfigModel.LogDownloadButton.LogTypesToDownload.MirrorLog)
                {
                    if(!GetApplicationMirrorData(theSummitRight, logPathRight, appConfigModel))
                    {
                        MessageBox.Show("Could not Log Mirror Data. Please retry. If the problem persists, please power off your ctm and power back on and try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                        rightLogDownloadFinished = true;
                        return;
                    }
                }
            }
            rightLogDownloadFinished = true;
        }
        /// <summary>
        /// Starts up the stim sweep window.
        /// </summary>
        /// <returns></returns>
        public async Task StimSweepButtonClick()
        {
            WindowManager window = new WindowManager();
            //window.ShowDialog(new StimSweepViewModel(theSummitLeft, theSummitRight, IsBilateral, senseLeftConfigModel, senseRightConfigModel, _log, appConfigModel), null, null);
            if (theSummitLeft != null)
            {
                if (!theSummitLeft.IsDisposed)
                {
                    if (isBilateral)
                    {
                        if (theSummitRight != null)
                        {
                            if (!theSummitRight.IsDisposed)
                            {
                                window.ShowDialog(new StimSweepViewModel(theSummitLeft, theSummitRight, IsBilateral, senseLeftConfigModel, senseRightConfigModel, _log, appConfigModel), null, null);
                                Task.Run(()=> UpdateStimStatusGroupLeft());
                                Task.Run(() => UpdateStimStatusGroupRight());
                                SensingState state;
                                //This checks to see if sensing is already enabled. This can happen if adaptive is already running and we don't need to configure it. 
                                //If it is, then skip setting up sensing
                                try
                                {
                                    theSummitLeft.ReadSensingState(out state);
                                    if (state.State.ToString().Contains("DetectionLd0") && ActiveGroupLeft.Equals("Group D"))
                                    {
                                        AdaptiveRunningLeft = "Adaptive On";
                                    }
                                }
                                catch (Exception error)
                                {
                                    _log.Error(error);
                                }

                                //This checks to see if sensing is already enabled. This can happen if adaptive is already running and we don't need to configure it. 
                                //If it is, then skip setting up sensing
                                try
                                {
                                    theSummitRight.ReadSensingState(out state);
                                    if (state.State.ToString().Contains("DetectionLd0") && ActiveGroupRight.Equals("Group D"))
                                    {
                                        AdaptiveRunningRight = "Adaptive On";
                                    }
                                }
                                catch (Exception error)
                                {
                                    _log.Error(error);
                                }
                            }
                        }
                    }
                    else if (!isBilateral)
                    {
                        window.ShowDialog(new StimSweepViewModel(theSummitLeft, theSummitRight, IsBilateral, senseLeftConfigModel, senseRightConfigModel, _log, appConfigModel), null, null);
                        UpdateStimStatusGroupLeft();
                        SensingState state;
                        //This checks to see if sensing is already enabled. This can happen if adaptive is already running and we don't need to configure it. 
                        //If it is, then skip setting up sensing
                        try
                        {
                            theSummitLeft.ReadSensingState(out state);
                            if (state.State.ToString().Contains("DetectionLd0") && ActiveGroupLeft.Equals("Group D"))
                            {
                                AdaptiveRunningLeft = "Adaptive On";
                            }
                        }
                        catch (Exception error)
                        {
                            _log.Error(error);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Starts up the montage window. Must reconnect with mode 4 and after it finishes reconnect to normal sense settings.
        /// </summary>
        /// <returns>async Task</returns>
        public async Task MontageButtonClick()
        {
            //set mode to 4 in left and if bilateral right config models
            senseLeftConfigModel.Mode = 4;
            if (IsBilateral)
            {
                senseRightConfigModel.Mode = 4;
            }
            //run spinner
            IsSpinnerVisible = true;
            //reconnect
            _shouldStopWorkerThread = true;
            try
            {
                if (workerThreadLeft != null)
                {
                    workerThreadLeft.Abort();
                    // make sure null so next operation can be executed
                    workerThreadLeft = null;
                }
                if (IsBilateral)
                {
                    if (workerThreadRight != null)
                    {
                        workerThreadRight.Abort();
                        // make sure null so next operation can be executed
                        workerThreadRight = null;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            if (theSummitLeft != null)
            {
                if (!theSummitLeft.IsDisposed)
                {
                    try
                    {
                        theSummitLeft.WriteSensingState(SenseStates.None, 0x00);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }
            }
            if (IsBilateral)
            {
                if (theSummitRight != null)
                {
                    if (!theSummitRight.IsDisposed)
                    {
                        try
                        {
                            theSummitRight.WriteSensingState(SenseStates.None, 0x00);
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                }
            }
            DisposeSummitSystem();
            CanConnect = true;
            _shouldStopWorkerThread = false;
            waitUntilCheckForCorrectINS = true;
            summitRightIsReadyForLeftToConnect = false;
            ConnectButtonClick();
            await Task.Run(() => WaitForConnectThreadCode());
            IsSpinnerVisible = false;
            //open window
            WindowManager window = new WindowManager();
            window.ShowDialog(new MontageViewModel(ref theSummitLeft, ref theSummitRight, _log, appConfigModel), null, null);
            window = new WindowManager();
            window.ShowDialog(new ReportWindowViewModel(ref theSummitLeft, ref theSummitRight, _log), null, null);
            MessageBoxResult messageResult = MessageBox.Show("Click Yes to Close Program or No to start streaming.", "Close Program?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            switch (messageResult)
            {
                case MessageBoxResult.Yes:
                    await Task.Run(() => ExitButtonClick());
                    return;
                case MessageBoxResult.No:
                    break;
            }
           //Reload sense files
            senseLeftConfigModel = jSONService?.GetSenseModelFromFile(senseLeftFileLocation);
            if (senseLeftConfigModel == null)
            {
                MessageBox.Show("Error loading sense config file. Please Restart Program.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (IsBilateral)
            {
                senseRightConfigModel = jSONService?.GetSenseModelFromFile(senseRightFileLocation);
                if (senseRightConfigModel == null)
                {
                    MessageBox.Show("Error loading sense config file. Please Restart Program.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            //run spinner
            IsSpinnerVisible = true;
            _shouldStopWorkerThread = true;
            try
            {
                if (workerThreadLeft != null)
                {
                    workerThreadLeft.Abort();
                    // make sure null so next operation can be executed
                    workerThreadLeft = null;
                }
                if (IsBilateral)
                {
                    if (workerThreadRight != null)
                    {
                        workerThreadRight.Abort();
                        // make sure null so next operation can be executed
                        workerThreadRight = null;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            if (theSummitLeft != null)
            {
                if (!theSummitLeft.IsDisposed)
                {
                    try
                    {
                        theSummitLeft.WriteSensingState(SenseStates.None, 0x00);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }
            }
            if (IsBilateral)
            {
                if (theSummitRight != null)
                {
                    if (!theSummitRight.IsDisposed)
                    {
                        try
                        {
                            theSummitRight.WriteSensingState(SenseStates.None, 0x00);
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                }
            }
            DisposeSummitSystem();
            CanConnect = true;
            waitUntilCheckForCorrectINS = true;
            summitRightIsReadyForLeftToConnect = false;
            _shouldStopWorkerThread = false;
            ConnectButtonClick();
            IsSpinnerVisible = false;
        }

        /// <summary>
        /// Waits until connection fully made before returning
        /// </summary>
        /// <returns>asnyc Task</returns>
        private void WaitForConnectThreadCode()
        {
            while (CanConnect)
            {
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Switch button
        /// </summary>
        public void SwitchButtonClick()
        {
            if (theSummitLeft != null)
            {
                if (!theSummitLeft.IsDisposed)
                {
                    if (isBilateral)
                    {
                        if (theSummitRight != null)
                        {
                            if (!theSummitRight.IsDisposed)
                            {
                                WindowManager window = new WindowManager();
                                window.ShowDialog(new SwitchViewModel(ref theSummitLeft, ref theSummitRight, IsBilateral, PROJECT_ID, _log, appConfigModel), null, null);
                                UpdateStimStatusGroupLeft();
                                UpdateStimStatusGroupRight();
                                SensingState state;
                                //This checks to see if sensing is already enabled. This can happen if adaptive is already running and we don't need to configure it. 
                                //If it is, then skip setting up sensing
                                try
                                {
                                    theSummitLeft.ReadSensingState(out state);
                                    if (state.State.ToString().Contains("DetectionLd0") && ActiveGroupLeft.Equals("Group D"))
                                    {
                                        AdaptiveRunningLeft = "Adaptive On";
                                    }
                                }
                                catch (Exception error)
                                {
                                    _log.Error(error);
                                }

                                //This checks to see if sensing is already enabled. This can happen if adaptive is already running and we don't need to configure it. 
                                //If it is, then skip setting up sensing
                                try
                                {
                                    theSummitRight.ReadSensingState(out state);
                                    if (state.State.ToString().Contains("DetectionLd0") && ActiveGroupRight.Equals("Group D"))
                                    {
                                        AdaptiveRunningRight = "Adaptive On";
                                    }
                                }
                                catch (Exception error)
                                {
                                    _log.Error(error);
                                }
                            }
                        }
                    }
                    else if (!isBilateral)
                    {
                        WindowManager window = new WindowManager();
                        window.ShowDialog(new SwitchViewModel(ref theSummitLeft, ref theSummitRight, IsBilateral, PROJECT_ID, _log, appConfigModel), null, null);
                        UpdateStimStatusGroupLeft();
                        SensingState state;
                        //This checks to see if sensing is already enabled. This can happen if adaptive is already running and we don't need to configure it. 
                        //If it is, then skip setting up sensing
                        try
                        {
                            theSummitLeft.ReadSensingState(out state);
                            if (state.State.ToString().Contains("DetectionLd0") && ActiveGroupLeft.Equals("Group D"))
                            {
                                AdaptiveRunningLeft = "Adaptive On";
                            }
                        }
                        catch (Exception error)
                        {
                            _log.Error(error);
                        }
                    }
                }
            }
        }
        #region Align
        /// <summary>
        /// Used to align the data points on both INS in bilateral
        /// </summary>
        public void AlignButtonClick()
        {
            if (IsBilateral)
            {
                if (theSummitLeft != null && theSummitRight != null)
                {
                    if (!theSummitLeft.IsDisposed && !theSummitRight.IsDisposed)
                    {
                        if (!CanConnect)
                        {
                            alignThread = new Thread(new ThreadStart(AlignThreadCode));
                            alignThread.IsBackground = true;
                            alignThread.Start();

                        }
                    }
                }
            }
            else
            {
                if (theSummitLeft != null)
                {
                    if (!theSummitLeft.IsDisposed)
                    {
                        if (!CanConnect)
                        {
                            alignThread = new Thread(new ThreadStart(AlignThreadCode));
                            alignThread.IsBackground = true;
                            alignThread.Start();

                        }
                    }
                }
            }
        }

        /// <summary>
        /// Worker thread that does the align process
        /// </summary>
        private void AlignThreadCode()
        {
            ProgressVisibility = Visibility.Visible;
            CurrentProgress = 5;
            ProgressText = "Running Align...";
            AlignButtonEnabled = false;
            alignSuccessShown = false;
            int counter = 5;
            if (IsBilateral)
            {
                if (StimStateLeft.Equals("TherapyActive") && StimStateRight.Equals("TherapyActive"))
                {
                    //Change to group B
                    try
                    {
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch(Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group B";
                    CurrentProgress = 8;
                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupRight();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitRight, senseRightConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupRight = "Group B";
                    CurrentProgress = 16;
                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    CurrentProgress = 24;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(3000);
                    CurrentProgress = 32;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    CurrentProgress = 40;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(4000);
                    CurrentProgress = 48;
                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    CurrentProgress = 56;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(3000);
                    CurrentProgress = 64;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    CurrentProgress = 72;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(2000);
                    CurrentProgress = 80;
                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group A";
                    CurrentProgress = 88;
                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupRight();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitRight, senseRightConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupRight = "Group A";
                    CurrentProgress = 100;
                }
                else if (StimStateLeft.Equals("TherapyOff") && StimStateRight.Equals("TherapyOff"))
                {
                    //Change to group B
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group B";
                    CurrentProgress = 8;
                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupRight();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitRight, senseRightConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupRight = "Group B";
                    CurrentProgress = 16;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    CurrentProgress = 24;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(3000);
                    CurrentProgress = 32;
                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    CurrentProgress = 40;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(4000);
                    CurrentProgress = 48;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    CurrentProgress = 56;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(3000);
                    CurrentProgress = 64;
                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    CurrentProgress = 72;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(2000);
                    CurrentProgress = 80;
                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group A";
                    CurrentProgress = 88;
                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupRight();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitRight, senseRightConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupRight = "Group A";
                    CurrentProgress = 100;
                }
                else if (StimStateLeft.Equals("TherapyActive") && StimStateRight.Equals("TherapyOff"))
                {
                    //Change to group B
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group B";
                    CurrentProgress = 8;
                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupRight();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitRight, senseRightConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupRight = "Group B";
                    CurrentProgress = 16;
                    //Change stim
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    CurrentProgress = 24;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(3000);
                    CurrentProgress = 32;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    CurrentProgress = 40;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(4000);
                    CurrentProgress = 48;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    CurrentProgress = 56;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(3000);
                    CurrentProgress = 64;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    CurrentProgress = 72;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(2000);
                    CurrentProgress = 80;
                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group A";
                    CurrentProgress = 88;
                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupRight();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitRight, senseRightConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupRight = "Group A";
                    CurrentProgress = 100;
                }
                else if (StimStateLeft.Equals("TherapyOff") && StimStateRight.Equals("TherapyActive"))
                {
                    //Change to group B
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group B";
                    CurrentProgress = 8;
                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupRight();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitRight, senseRightConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupRight = "Group B";
                    CurrentProgress = 16;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    CurrentProgress = 24;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(3000);
                    CurrentProgress = 32;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    CurrentProgress = 40;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(4000);
                    CurrentProgress = 48;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    CurrentProgress = 56;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(3000);
                    CurrentProgress = 64;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    CurrentProgress = 72;
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(2000);
                    CurrentProgress = 80;
                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group A";
                    CurrentProgress = 88;
                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupRight();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitRight, senseRightConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupRight();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupRight = "Group A";
                    CurrentProgress = 100;
                }
                else
                {
                    MessageBox.Show("Could not read therapy status. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward..", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    ProgressVisibility = Visibility.Hidden;
                    AlignButtonEnabled = true; return;
                }
                if (alignSuccessShown)
                {
                    ShowMessageBox("Align Finished Successfully", "Success!", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    AlignButtonEnabled = true;
                }
                alignSuccessShown = true;
            }
            else
            {
                if (StimStateLeft.Equals("TherapyActive"))
                {
                    //Change to group B
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group B";
                    CurrentProgress = 15;
                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    Thread.Sleep(3000);
                    CurrentProgress = 30;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    Thread.Sleep(4000);
                    CurrentProgress = 45;
                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    Thread.Sleep(3000);
                    CurrentProgress = 65;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    Thread.Sleep(2000);
                    CurrentProgress = 85;
                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group A";
                    CurrentProgress = 100;
                }
                else if (StimStateLeft.Equals("TherapyOff"))
                {
                    //Change to group B
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group B";
                    CurrentProgress = 15;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn(); 
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    Thread.Sleep(3000);
                    CurrentProgress = 30;
                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    Thread.Sleep(4000);
                    CurrentProgress = 45;
                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyActive";
                    Thread.Sleep(3000);
                    CurrentProgress = 65;
                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    StimStateLeft = "TherapyOff";
                    Thread.Sleep(2000);
                    CurrentProgress = 85;
                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--; Thread.Sleep(500);
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                        Thread.Sleep(300);
                        UpdateStimStatusGroupLeft();
                        Thread.Sleep(1000);
                        summitSensing.StartStreaming(theSummitLeft, senseLeftConfigModel, false);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        ProgressVisibility = Visibility.Hidden;
                        UpdateStimStatusGroupLeft();
                        AlignButtonEnabled = true; return;
                    }
                    ActiveGroupLeft = "Group A";
                    CurrentProgress = 100;
                }
            }
            AlignButtonEnabled = true;
            ProgressVisibility = Visibility.Hidden;
            ShowMessageBox("Align Finished Successfully", "Success!", MessageBoxButton.OK, MessageBoxImage.Asterisk);
        }

        /// <summary>
        /// Used just to check for error in the align
        /// </summary>
        /// <param name="info">Retrun value from medtronic api call</param>
        /// <returns>true if success and false if unsuccessful</returns>
        private bool CheckReturnCodeForClinician(APIReturnInfo info)
        {
            if (info.RejectCode != 0)
            {
                _log.Warn("Error in Return Code for clinician. Reject Description: " + info.Descriptor + ". Reject Code: " + info.RejectCode);
                string messageBoxText = "Reject Description: " + info.Descriptor + ". Reject Code: " + info.RejectCode;
                string caption = "ERROR";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Error;
                MessageBox.Show(messageBoxText, caption, button, icon);
                return false;
            }
            return true;
        }
        #endregion
        /// <summary>
        /// Actual Connect button pressed by user and calls the connection WorkerThread
        /// WorkerThread used so not to freeze UI
        /// </summary>
        public void ConnectButtonClick()
        {
            //Check to see if the sense setup is going to have major packet loss due to too much data over bandwidth.
            if (!CheckPacketLoss(senseLeftConfigModel))
            {
                ShowMessageBox("ERROR in Left/Unilateral config file! Major packet loss will occur due to too much data over bandwidth.  Please fix senseLeft_config.json file and restart application to reload changes.");
                return;
            }
            if (IsBilateral)
            {
                //Check to see if the sense setup is going to have major packet loss due to too much data over bandwidth.
                if (!CheckPacketLoss(senseRightConfigModel))
                {
                    ShowMessageBox("ERROR in Right config file! Packet loss over maximum. Major packet loss will occur due to too much data over bandwidth.  Please fix senseRight_config.json file and restart application to reload changes.");
                    return;
                }
            }
            
            //If first time connecting, check that summit manager is null so we don't create another one
            if (theSummitManager == null)
            {
                _log.Info("Initializing Summit Manager");
                theSummitManager = new SummitManager(PROJECT_ID, 200, false);
            }
            
            //If we're not connected already, start the worker thread to connect
            if (CanConnect)
            {
                ConnectButtonEnabled = false;
                IsSpinnerVisible = true;
                _log.Info("Connecting...");
                ConnectButtonText = "Connecting...";
                ConnectButtonColor = Brushes.Yellow;
                //Start worker threads for left and only start for right if bilateral
                workerThreadLeft = new Thread(new ThreadStart(WorkerThreadLeft));
                workerThreadLeft.IsBackground = true;
                workerThreadLeft.Start();
                if (IsBilateral)
                {
                    workerThreadRight = new Thread(new ThreadStart(WorkerThreadRight));
                    workerThreadRight.IsBackground = true;
                    workerThreadRight.Start();
                }
            }
            _log.Info("ConnectButtonClick function finished");
        }
        /// <summary>
        /// Button for reporting conditions or medicaions
        /// </summary>
        public void ReportButtonClick()
        {
            if (theSummitLeft != null)
            {
                if (!theSummitLeft.IsDisposed)
                {
                    WindowManager window = new WindowManager();
                    window.ShowWindow(new ReportWindowViewModel(ref theSummitLeft, ref theSummitRight, _log), null, null);
                }
            }
        }

        /// <summary>
        /// Web page one button click
        /// </summary>
        public void WebPageOneButtonClick()
        {
            if (CanConnect && !appConfigModel.WebPageButtons.OpenWithoutBeingConnected)
            {
                MessageBox.Show("You must be connected to proceed to website. Please press connect and wait until both CTM and INS are green and try again.", "Connection Required", MessageBoxButton.OK, MessageBoxImage.Hand);
                return;
            }
            if (!OpenUri(appConfigModel.WebPageButtons.WebPageOneURL))
            {
                MessageBox.Show("Please be sure you have the correct URL and the format includes https:// (ie https://www.google.com)", "Warning", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        /// <summary>
        /// Web page two button click
        /// </summary>
        public void WebPageTwoButtonClick()
        {
            if (CanConnect && !appConfigModel.WebPageButtons.OpenWithoutBeingConnected)
            {
                MessageBox.Show("You must be connected to proceed to website. Please press connect and wait until both CTM and INS are green and try again.", "Connection Required", MessageBoxButton.OK, MessageBoxImage.Hand);
                return;
            }
            if (!OpenUri(appConfigModel.WebPageButtons.WebPageTwoURL))
            {
                MessageBox.Show("Please be sure you have the correct URL and the format includes https:// (ie https://www.google.com)", "Warning", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        /// <summary>
        /// Moves to group designated in the app config file
        /// </summary>
        public async Task MoveGroupButtonClick()
        {
            if (CanConnect)
            {
                MessageBox.Show("Not Connected to INS", "Error moving groups on INS", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            bool shouldNotMoveGroup = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    MessageBoxResult messageResult = MessageBox.Show(Application.Current.MainWindow, "You are about to move to a different group. Proceed?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                    switch (messageResult)
                    {
                        case MessageBoxResult.Yes:
                            break;
                        case MessageBoxResult.No:
                            shouldNotMoveGroup = true;
                            break;
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }

            });
            if (shouldNotMoveGroup)
            {
                return;
            }
            SummitStim summitStim = new SummitStim(_log);
            HelperFunctions helperFunctions = new HelperFunctions();
            if (helperFunctions.CheckGroupIsCorrectFormat(appConfigModel.MoveGroupButton.GroupToMoveToLeftUnilateral))
            {
                Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitLeft, helperFunctions.ConvertStimModelGroupToAPIGroup(appConfigModel.MoveGroupButton.GroupToMoveToLeftUnilateral), senseLeftConfigModel);
                if (!valueReturn.Item1)
                {
                    MessageBox.Show(valueReturn.Item2, "Error moving group on Left INS", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    UpdateStimStatusGroupLeft();
                }
            }
            if (isBilateral)
            {
                if (helperFunctions.CheckGroupIsCorrectFormat(appConfigModel.MoveGroupButton.GroupToMoveToRight))
                {
                    Tuple<bool, string> valueReturn = await summitStim.ChangeActiveGroup(theSummitRight, helperFunctions.ConvertStimModelGroupToAPIGroup(appConfigModel.MoveGroupButton.GroupToMoveToRight), senseRightConfigModel);
                    if (!valueReturn.Item1)
                    {
                        MessageBox.Show(valueReturn.Item2, "Error moving group on Right INS", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        UpdateStimStatusGroupRight();
                    }
                }
            }
        }

        /// <summary>
        /// Disposes of summit system and reconnects. This creates a wholel new session directory for the Medtronic json files
        /// </summary>
        public void NewSessionButtonClick()
        {
            IsSpinnerVisible = true;
            senseLeftConfigModel = jSONService.GetSenseModelFromFile(senseLeftFileLocation);
            if (senseLeftConfigModel == null)
            {
                MessageBox.Show("Sense Config could not be loaded. Please check that it exists or has the correct format", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }
            if (IsBilateral)
            {
                senseRightConfigModel = jSONService.GetSenseModelFromFile(senseRightFileLocation);
                if (senseRightConfigModel == null)
                {
                    MessageBox.Show("Sense Config could not be loaded. Please check that it exists or has the correct format", "Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }
            }

            _shouldStopWorkerThread = true;
            try
            {
                if (workerThreadLeft != null)
                {
                    workerThreadLeft.Abort();
                    // make sure null so next operation can be executed
                    workerThreadLeft = null;
                }
                if (IsBilateral)
                {
                    if (workerThreadRight != null)
                    {
                        workerThreadRight.Abort();
                        // make sure null so next operation can be executed
                        workerThreadRight = null;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            if (theSummitLeft != null)
            {
                if (!theSummitLeft.IsDisposed)
                {
                    try
                    {
                        theSummitLeft.WriteSensingState(SenseStates.None, 0x00);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }
            }
            if (IsBilateral)
            {
                if (theSummitRight != null)
                {
                    if (!theSummitRight.IsDisposed)
                    {
                        try
                        {
                            theSummitRight.WriteSensingState(SenseStates.None, 0x00);
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                }
            }
            DisposeSummitSystem();
            CanConnect = true;
            waitUntilCheckForCorrectINS = true;
            summitRightIsReadyForLeftToConnect = false;
            _shouldStopWorkerThread = false;
            ConnectButtonClick();
        }

        /// <summary>
        /// Button to exit program
        /// </summary>
        public void ExitButtonClick()
        {
            bool shouldNotExit = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    MessageBoxResult messageResult = MessageBox.Show(Application.Current.MainWindow, "Exit Program?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                    switch (messageResult)
                    {
                        case MessageBoxResult.Yes:
                            break;
                        case MessageBoxResult.No:
                            shouldNotExit = true;
                            break;
                    }
                }
                catch(Exception e)
                {
                    _log.Error(e);
                }
                
            });
            if (shouldNotExit)
            {
                return;
            }
            _shouldStopWorkerThread = true;
            if (theSummitLeft != null)
            {
                if (!theSummitLeft.IsDisposed)
                {
                    try
                    {
                        theSummitLeft.WriteSensingState(SenseStates.None, 0x00);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }
            }
            if (theSummitRight != null)
            {
                if (!theSummitRight.IsDisposed)
                {
                    try
                    {
                        theSummitRight.WriteSensingState(SenseStates.None, 0x00);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }
            }
            Thread.Sleep(500);
            // perform clean up. Abort continuous connection worker thread and dispose of summit system/manager
            DisposeSummitManagerAndSystem();
            Environment.Exit(0);
        }
        #endregion

        #region UI Binding Elements
        /// <summary>
        /// Determines if the patient stim control button is visible or hidden
        /// </summary>
        public bool PatientStimControlButtonVisible
        {
            get { return _patientStimControlButtonVisible; }
            set
            {
                _patientStimControlButtonVisible = value;
                NotifyOfPropertyChange(() => PatientStimControlButtonVisible);
            }
        }
        /// <summary>
        /// Text for the application name and version
        /// </summary>
        public string ApplicationTitleText
        {
            get { return _applicationTitleText; }
            set
            {
                _applicationTitleText = value;
                NotifyOfPropertyChange(() => ApplicationTitleText);
            }
        }
        /// <summary>
        /// Sets the window style for researcher vs patient
        /// </summary>
        public WindowStyle WindowStyleForMainWindow
        {
            get { return _windowStyleForMainWindow; }
            set
            {
                _windowStyleForMainWindow = value;
                NotifyOfPropertyChange(() => WindowStyleForMainWindow);
            }
        }
        /// <summary>
        /// Sets visiblity for the beep enables display
        /// </summary>
        public Visibility BeepEnabledVisibility
        {
            get { return _beepEnabledVisibility; }
            set
            {
                _beepEnabledVisibility = value;
                NotifyOfPropertyChange(() => BeepEnabledVisibility);
            }
        }
        /// <summary>
        /// Determines if the move group button is visible or hidden
        /// </summary>
        public bool MoveGroupButtonEnabled
        {
            get { return _moveGroupButtonEnabled; }
            set
            {
                _moveGroupButtonEnabled = value;
                NotifyOfPropertyChange(() => MoveGroupButtonEnabled);
            }
        }

        /// <summary>
        /// Text for move group button
        /// </summary>
        public string MoveGroupButtonText
        {
            get { return _moveGroupButtonText; }
            set
            {
                _moveGroupButtonText = value;
                NotifyOfPropertyChange(() => MoveGroupButtonText);
            }
        }
        /// <summary>
        /// Decides if report button is visible or not.
        /// </summary>
        public bool ReportButtonVisible
        {
            get { return _reportButtonVisible; }
            set
            {
                _reportButtonVisible = value;
                NotifyOfPropertyChange(() => ReportButtonVisible);
            }
        }
        /// <summary>
        /// Displays beep logged text on screen for Left
        /// </summary>
        public string BeepLoggedLeft
        {
            get { return _beepLoggedLeft; }
            set
            {
                _beepLoggedLeft = value;
                NotifyOfPropertyChange(() => BeepLoggedLeft);
            }
        }
        /// <summary>
        /// Displays beep logged text on screen for right
        /// </summary>
        public string BeepLoggedRight
        {
            get { return _beepLoggedRight; }
            set
            {
                _beepLoggedRight = value;
                NotifyOfPropertyChange(() => BeepLoggedRight);
            }
        }
        /// <summary>
        /// Determines if switch button is visible or hidden
        /// </summary>
        public bool IsSwitchVisible
        {
            get { return _isSwitchVisible; }
            set
            {
                _isSwitchVisible = value;
                NotifyOfPropertyChange(() => IsSwitchVisible);
            }
        }
        /// <summary>
        /// Determines if the align button is visible or hidden
        /// </summary>
        public bool IsAlignVisible
        {
            get { return _isAlignVisible; }
            set
            {
                _isAlignVisible = value;
                NotifyOfPropertyChange(() => IsAlignVisible);
            }
        }
        /// <summary>
        /// Determines if spinner is visible or not
        /// </summary>
        public bool IsSpinnerVisible
        {
            get { return _isSpinnerVisible; }
            set
            {
                _isSpinnerVisible = value;
                NotifyOfPropertyChange(() => IsSpinnerVisible);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimGroupLeftUni
        {
            get { return _visibilityStimGroupLeftUni; }
            set
            {
                _visibilityStimGroupLeftUni = value;
                NotifyOfPropertyChange(() => VisibilityStimGroupLeftUni);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimAmpLeftUni
        {
            get { return _visibilityStimAmpLeftUni; }
            set
            {
                _visibilityStimAmpLeftUni = value;
                NotifyOfPropertyChange(() => VisibilityStimAmpLeftUni);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimRateLeftUni
        {
            get { return _visibilityStimRateLeftUni; }
            set
            {
                _visibilityStimRateLeftUni = value;
                NotifyOfPropertyChange(() => VisibilityStimRateLeftUni);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimContactsLeftUni
        {
            get { return _visibilityStimContactsLeftUni; }
            set
            {
                _visibilityStimContactsLeftUni = value;
                NotifyOfPropertyChange(() => VisibilityStimContactsLeftUni);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimTherapyOnOffLeftUni
        {
            get { return _visibilityStimTherapyOnOffLeftUni; }
            set
            {
                _visibilityStimTherapyOnOffLeftUni = value;
                NotifyOfPropertyChange(() => VisibilityStimTherapyOnOffLeftUni);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimAdaptiveOnLeftUni
        {
            get { return _visibilityStimAdaptiveOnLeftUni; }
            set
            {
                _visibilityStimAdaptiveOnLeftUni = value;
                NotifyOfPropertyChange(() => VisibilityStimAdaptiveOnLeftUni);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimGroupRight
        {
            get { return _visibilityStimGroupRight; }
            set
            {
                _visibilityStimGroupRight = value;
                NotifyOfPropertyChange(() => VisibilityStimGroupRight);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimAmpRight
        {
            get { return _visibilityStimAmpRight; }
            set
            {
                _visibilityStimAmpRight = value;
                NotifyOfPropertyChange(() => VisibilityStimAmpRight);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimRateRight
        {
            get { return _visibilityStimRateRight; }
            set
            {
                _visibilityStimRateRight = value;
                NotifyOfPropertyChange(() => VisibilityStimRateRight);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimContactsRight
        {
            get { return _visibilityStimContactsRight; }
            set
            {
                _visibilityStimContactsRight = value;
                NotifyOfPropertyChange(() => VisibilityStimContactsRight);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimTherapyOnOffRight
        {
            get { return _visibilityStimTherapyOnOffRight; }
            set
            {
                _visibilityStimTherapyOnOffRight = value;
                NotifyOfPropertyChange(() => VisibilityStimTherapyOnOffRight);
            }
        }
        /// <summary>
        /// Visibilitys the stim data if set to true
        /// </summary>
        public bool VisibilityStimAdaptiveOnRight
        {
            get { return _visibilityStimAdaptiveOnRight; }
            set
            {
                _visibilityStimAdaptiveOnRight = value;
                NotifyOfPropertyChange(() => VisibilityStimAdaptiveOnRight);
            }
        }
        /// <summary>
        /// Determines if the Web page 1 button is visible or hidden
        /// </summary>
        public bool WebPageOneButtonEnabled
        {
            get { return _webPageOneButtonEnabled; }
            set
            {
                _webPageOneButtonEnabled = value;
                NotifyOfPropertyChange(() => WebPageOneButtonEnabled);
            }
        }

        /// <summary>
        /// Text for Web page 1 button
        /// </summary>
        public string WebPageOneButtonText
        {
            get { return _webPageOneButtonText; }
            set
            {
                _webPageOneButtonText = value;
                NotifyOfPropertyChange(() => WebPageOneButtonText);
            }
        }
        /// <summary>
        /// Determines if the Web page 2 button is visible or hidden
        /// </summary>
        public bool WebPageTwoButtonEnabled
        {
            get { return _webPageTwoButtonEnabled; }
            set
            {
                _webPageTwoButtonEnabled = value;
                NotifyOfPropertyChange(() => WebPageTwoButtonEnabled);
            }
        }

        /// <summary>
        /// Text for Web page 2 button
        /// </summary>
        public string WebPageTwoButtonText
        {
            get { return _webPageTwoButtonText; }
            set
            {
                _webPageTwoButtonText = value;
                NotifyOfPropertyChange(() => WebPageTwoButtonText);
            }
        }
        /// <summary>
        /// Binding used to change the color of the connect button/displays
        /// </summary>
        public Brush ConnectButtonColor
        {
            get { return _connectButtonColor ?? (_connectButtonColor = Brushes.LightGray); }
            set
            {
                _connectButtonColor = value;
                NotifyOfPropertyChange(() => ConnectButtonColor);
            }
        }
        /// <summary>
        /// Binding used to change the text of the download log button/displays
        /// </summary>
        public string DownloadLogButtonText
        {
            get { return _downloadLogButtonText; }
            set
            {
                _downloadLogButtonText = value;
                NotifyOfPropertyChange(() => DownloadLogButtonText);
            }
        }
        /// <summary>
        /// Determines if the download log button is visible or hidden
        /// </summary>
        public bool DownloadLogButtonVisible
        {
            get { return _downloadLogButtonVisible; }
            set
            {
                _downloadLogButtonVisible = value;
                NotifyOfPropertyChange(() => DownloadLogButtonVisible);
            }
        }
        /// <summary>
        /// Binding used to change the text of the connect button/displays
        /// </summary>
        public string ConnectButtonText
        {
            get { return _connectButtonText ?? (_connectButtonText = "Connect"); }
            set
            {
                _connectButtonText = value;
                NotifyOfPropertyChange(() => ConnectButtonText);
            }
        }
        /// <summary>
        /// Changes the text for the left CTM
        /// </summary>
        public string CTMLeftText
        {
            get { return _ctmLeftText ?? (_ctmLeftText = "CTM"); }
            set
            {
                _ctmLeftText = value;
                NotifyOfPropertyChange(() => CTMLeftText);
            }
        }
        /// <summary>
        /// Changes the text for the left INS
        /// </summary>
        public string INSLeftText
        {
            get { return _insLeftText ?? (_insLeftText = "INS"); }
            set
            {
                _insLeftText = value;
                NotifyOfPropertyChange(() => INSLeftText);
            }
        }
        /// <summary>
        /// Changes the text for the Left Stream to either include an L for bilateral or no L for unilateral
        /// </summary>
        public string StreamLeftText
        {
            get { return _streamLeftText ?? (_streamLeftText = "Stream"); }
            set
            {
                _streamLeftText = value;
                NotifyOfPropertyChange(() => StreamLeftText);
            }
        }
        /// <summary>
        /// Determines if the connect button is enabled or disabled
        /// </summary>
        public bool ConnectButtonEnabled
        {
            get { return connectButtonEnabled; }
            set
            {
                connectButtonEnabled = value;
                NotifyOfPropertyChange(() => ConnectButtonEnabled);
            }
        }
        /// <summary>
        /// Determines if the align button is enabled or disabled
        /// </summary>
        public bool AlignButtonEnabled
        {
            get { return alignButtonEnabled; }
            set
            {
                alignButtonEnabled = value;
                NotifyOfPropertyChange(() => AlignButtonEnabled);
            }
        }

        /// <summary>
        /// Sets the visibility on the right UI elements
        /// </summary>
        public bool IsBilateral
        {
            get { return isBilateral; }
            set
            {
                isBilateral = value;
                NotifyOfPropertyChange(() => IsBilateral);
            }
        }
        /// <summary>
        /// Displays if montage button is visible or not
        /// </summary>
        public bool MontageButtonEnabled
        {
            get { return _montageButtonEnabled; }
            set
            {
                _montageButtonEnabled = value;
                NotifyOfPropertyChange(() => MontageButtonEnabled);
            }
        }
        /// <summary>
        /// Displays if stim sweep button is visible or not
        /// </summary>
        public bool StimSweepButtonEnabled
        {
            get { return _stimSweepButtonEnabled; }
            set
            {
                _stimSweepButtonEnabled = value;
                NotifyOfPropertyChange(() => StimSweepButtonEnabled);
            }
        }
        /// <summary>
        /// Displays if new session button is visible or not
        /// </summary>
        public bool NewSessionButtonEnabled
        {
            get { return _newSessionButtonEnabled; }
            set
            {
                _newSessionButtonEnabled = value;
                NotifyOfPropertyChange(() => NewSessionButtonEnabled);
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
        /// True displays the tab bar and false hides it
        /// </summary>
        public Visibility TabVisibility
        {
            get { return _tabVisibility; }
            set
            {
                _tabVisibility = value;
                NotifyOfPropertyChange(() => TabVisibility);
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
        /// <summary>
        /// Percent of the battery of the laptop
        /// </summary>
        public string LaptopBatteryLevel
        {
            get { return _laptopBatteryLevel; }
            set
            {
                _laptopBatteryLevel = value;
                NotifyOfPropertyChange(() => LaptopBatteryLevel);
            }
        }
        /// <summary>
        /// Displays the stopwatch timer on the screen for Left
        /// </summary>
        public string StopWatchTimeLeft
        {
            get { return _stopWatchTimeLeft; }
            set
            {
                _stopWatchTimeLeft = value;
                NotifyOfPropertyChange(() => StopWatchTimeLeft);
            }
        }
        /// <summary>
        /// Displays the stopwatch timer on the screen for Right
        /// </summary>
        public string StopWatchTimeRight
        {
            get { return _stopWatchTimeRight; }
            set
            {
                _stopWatchTimeRight = value;
                NotifyOfPropertyChange(() => StopWatchTimeRight);
            }
        }
        /// <summary>
        /// Binding for the CTM Left Color
        /// </summary>
        public Brush BorderCTMLeftBackground
        {
            get { return _borderCTMLeftBackground ?? (_borderCTMLeftBackground = Brushes.LightGray); }
            set
            {
                _borderCTMLeftBackground = value;
                NotifyOfPropertyChange(() => BorderCTMLeftBackground);
            }
        }
        /// <summary>
        /// Binding for the CTM Right Color
        /// </summary>
        public Brush BorderCTMRightBackground
        {
            get { return _borderCTMRightBackground ?? (_borderCTMRightBackground = Brushes.LightGray); }
            set
            {
                _borderCTMRightBackground = value;
                NotifyOfPropertyChange(() => BorderCTMRightBackground);
            }
        }
        /// <summary>
        /// Binding for the INS Left Color
        /// </summary>
        public Brush BorderINSLeftBackground
        {
            get { return _borderINSLeftBackground ?? (_borderINSLeftBackground = Brushes.LightGray); }
            set
            {
                _borderINSLeftBackground = value;
                NotifyOfPropertyChange(() => BorderINSLeftBackground);
            }
        }
        /// <summary>
        /// Binding for the INS Right Color
        /// </summary>
        public Brush BorderINSRightBackground
        {
            get { return _borderINSRightBackground ?? (_borderINSRightBackground = Brushes.LightGray); }
            set
            {
                _borderINSRightBackground = value;
                NotifyOfPropertyChange(() => BorderINSRightBackground);
            }
        }
        /// <summary>
        /// Binding for the Stream Left Color
        /// </summary>
        public Brush BorderStreamLeftBackground
        {
            get { return _borderStreamLeftBackground ?? (_borderStreamLeftBackground = Brushes.LightGray); }
            set
            {
                _borderStreamLeftBackground = value;
                NotifyOfPropertyChange(() => BorderStreamLeftBackground);
            }
        }
        /// <summary>
        /// Binding for the Stream Right Color
        /// </summary>
        public Brush BorderStreamRightBackground
        {
            get { return _borderStreamRightBackground ?? (_borderStreamRightBackground = Brushes.LightGray); }
            set
            {
                _borderStreamRightBackground = value;
                NotifyOfPropertyChange(() => BorderStreamRightBackground);
            }
        }
        /// <summary>
        /// Binding for the CTM Left Battery Level
        /// </summary>
        public string CTMLeftBatteryLevel
        {
            get { return _CTMLeftBatteryLevel ?? (_CTMLeftBatteryLevel = "%"); }
            set
            {
                _CTMLeftBatteryLevel = value;
                NotifyOfPropertyChange(() => CTMLeftBatteryLevel);
            }
        }
        /// <summary>
        /// Binding for the CTM Right Battery Level
        /// </summary>
        public string CTMRightBatteryLevel
        {
            get { return _CTMRightBatteryLevel ?? (_CTMRightBatteryLevel = "%"); }
            set
            {
                _CTMRightBatteryLevel = value;
                NotifyOfPropertyChange(() => CTMRightBatteryLevel);
            }
        }
        /// <summary>
        /// Binding for the INS Left Battery Level
        /// </summary>
        public string INSLeftBatteryLevel
        {
            get { return _INSLeftBatteryLevel ?? (_INSLeftBatteryLevel = "%"); }
            set
            {
                _INSLeftBatteryLevel = value;
                NotifyOfPropertyChange(() => INSLeftBatteryLevel);
            }
        }
        /// <summary>
        /// Binding for the INS Right Battery Level
        /// </summary>
        public string INSRightBatteryLevel
        {
            get { return _INSRightBatteryLevel ?? (_INSRightBatteryLevel = "%"); }
            set
            {
                _INSRightBatteryLevel = value;
                NotifyOfPropertyChange(() => INSRightBatteryLevel);
            }
        }
        /// <summary>
        /// Binding for the Group for Left
        /// </summary>
        public string ActiveGroupLeft
        {
            get { return _activeGroupLeft ?? (_activeGroupLeft = ""); }
            set
            {
                _activeGroupLeft = value;
                NotifyOfPropertyChange(() => ActiveGroupLeft);
            }
        }
        /// <summary>
        /// Binding for the Group for Right
        /// </summary>
        public string ActiveGroupRight
        {
            get { return _activeGroupRight ?? (_activeGroupRight = ""); }
            set
            {
                _activeGroupRight = value;
                NotifyOfPropertyChange(() => ActiveGroupRight);
            }
        }
        /// <summary>
        /// Binding for the Stim Rate for Left
        /// </summary>
        public string StimRateLeft
        {
            get { return _stimRateLeft ?? (_stimRateLeft = ""); }
            set
            {
                _stimRateLeft = value;
                NotifyOfPropertyChange(() => StimRateLeft);
            }
        }
        /// <summary>
        /// Binding for the Stim Rate for Right
        /// </summary>
        public string StimRateRight
        {
            get { return _stimRateRight ?? (_stimRateRight = ""); }
            set
            {
                _stimRateRight = value;
                NotifyOfPropertyChange(() => StimRateRight);
            }
        }
        /// <summary>
        /// Binding used to change the color of the Stim Therapy TextColor Right
        /// </summary>
        public Brush StimStateRightTextColor
        {
            get { return _stimStateRightTextColor ?? (_stimStateRightTextColor = Brushes.Black); }
            set
            {
                _stimStateRightTextColor = value;
                NotifyOfPropertyChange(() => StimStateRightTextColor);
            }
        }
        /// <summary>
        /// Binding for the Stim Amp for Left
        /// </summary>
        public string StimAmpLeft
        {
            get { return _stimAmpLeft ?? (_stimAmpLeft = ""); }
            set
            {
                _stimAmpLeft = value;
                NotifyOfPropertyChange(() => StimAmpLeft);
            }
        }
        /// <summary>
        /// Binding for the Stim Amp for Right
        /// </summary>
        public string StimAmpRight
        {
            get { return _stimAmpRight ?? (_stimAmpRight = ""); }
            set
            {
                _stimAmpRight = value;
                NotifyOfPropertyChange(() => StimAmpRight);
            }
        }
        /// <summary>
        /// Binding for the Stim State for Left
        /// </summary>
        public string StimStateLeft
        {
            get { return _stimStateLeft ?? (_stimStateLeft = ""); }
            set
            {
                _stimStateLeft = value;
                if(_stimStateLeft.Equals("TherapyActive"))
                {
                    StimStateLeftTextColor = Brushes.ForestGreen;
                }
                else if(_stimStateLeft.Equals("TherapyOff"))
                {
                    StimStateLeftTextColor = Brushes.Red;
                }
                else
                {
                    StimStateLeftTextColor = Brushes.Black;
                }
                NotifyOfPropertyChange(() => StimStateLeft);
            }
        }
        /// <summary>
        /// Binding used to change the color of the Stim Therapy TextColor Left
        /// </summary>
        public Brush StimStateLeftTextColor
        {
            get { return _stimStateLeftTextColor ?? (_stimStateLeftTextColor = Brushes.Black); }
            set
            {
                _stimStateLeftTextColor = value;
                NotifyOfPropertyChange(() => StimStateLeftTextColor);
            }
        }
        /// <summary>
        /// Binding for stim electrodes for left
        /// </summary>
        public string StimElectrodeLeft
        {
            get { return _stimElectrodeLeft ?? (_stimElectrodeLeft = ""); }
            set
            {
                _stimElectrodeLeft = value;
                NotifyOfPropertyChange(() => StimElectrodeLeft);
            }
        }
        /// <summary>
        /// Binding for the Stim State for Right
        /// </summary>
        public string StimStateRight
        {
            get { return _stimStateRight ?? (_stimStateRight = ""); }
            set
            {
                _stimStateRight = value;
                if (_stimStateRight.Equals("TherapyActive"))
                {
                    StimStateRightTextColor = Brushes.ForestGreen;
                }
                else if (_stimStateRight.Equals("TherapyOff"))
                {
                    StimStateRightTextColor = Brushes.Red;
                }
                else
                {
                    StimStateRightTextColor = Brushes.Black;
                }
                NotifyOfPropertyChange(() => StimStateRight);
            }
        }
        /// <summary>
        /// Binding for stim electrodes for right
        /// </summary>
        public string StimElectrodeRight
        {
            get { return _stimElectrodeRight ?? (_stimElectrodeRight = ""); }
            set
            {
                _stimElectrodeRight = value;
                NotifyOfPropertyChange(() => StimElectrodeRight);
            }
        }
        /// <summary>
        /// Binding for displaying if adaptive is running for left
        /// </summary>
        public string AdaptiveRunningLeft
        {
            get { return _adaptiveRunningLeft ?? (_adaptiveRunningLeft = ""); }
            set
            {
                _adaptiveRunningLeft = value;
                NotifyOfPropertyChange(() => AdaptiveRunningLeft);
            }
        }
        /// <summary>
        /// Binding for displaying if adaptive is running for right
        /// </summary>
        public string AdaptiveRunningRight
        {
            get { return _adaptiveRunningRight ?? (_adaptiveRunningRight = ""); }
            set
            {
                _adaptiveRunningRight = value;
                NotifyOfPropertyChange(() => AdaptiveRunningRight);
            }
        }
        #endregion

        #region StopWatch for stream time and log beep code

        private void dt_TickLeft(object sender, EventArgs e)
        {
            if (stopWatchLeft.IsRunning)
            {
                TimeSpan ts = stopWatchLeft.Elapsed;
                currentTimeLeft = ts.ToString("hh\\:mm\\:ss");
                StopWatchTimeLeft = currentTimeLeft;
            }
        }

        private void dt_TickRight(object sender, EventArgs e)
        {
            if (stopWatchRight.IsRunning)
            {
                TimeSpan ts = stopWatchRight.Elapsed;
                currentTimeRight = ts.ToString("hh\\:mm\\:ss");
                StopWatchTimeRight = currentTimeRight;
            }
        }

        private void WaveIn_data(object sender, WaveInEventArgs e)
        {
            //Makes it so the beep logged text on screen stays on for sufficient time
            if(beepLogCounterForText == 0)
            {
                BeepLoggedLeft = "";
                BeepLoggedRight = "";
                
            }
            else if(beepLogCounterForText >= 0)
            {
                beepLogCounterForText--;
            }
            
            //find mean
            int mean = 0;
            for (int i = 0; i < e.BytesRecorded; i++)
            {
                mean += e.Buffer[i];
            }
            mean = mean / e.BytesRecorded;

            //subtract mean from each value in array and use absolute value
            for (int i = 0; i < e.BytesRecorded; i++)
            {
                e.Buffer[i] = (byte)Math.Abs((e.Buffer[i] - mean));
            }

            //Check if the max value in the array is above threshold value
            if (e.Buffer.Max() > signalOnValue)
            {
                currentOnFlag = true;
            }
            else
            {
                currentOnFlag = false;
            }

            if (!previousOnFlag && currentOnFlag)
            {
                if (theSummitLeft != null && !CanConnect)
                {
                    if (!theSummitLeft.IsDisposed)
                    {
                        try
                        {
                            //Log event that stim was turned on and check to make sure that event logging was successful
                            bufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Log Beep", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            CheckForReturnError(bufferReturnInfo, "Log Beep");
                            BeepLoggedLeft = "Beep Logged";
                            //reset the timer
                            beepLogCounterForText = 25;
                        }
                        catch (Exception error)
                        {
                            _log.Error(error);
                        }
                    }
                }
                if (theSummitRight != null && !CanConnect && isBilateral)
                {
                    if (!theSummitRight.IsDisposed)
                    {
                        try
                        {
                            //Log event that stim was turned on and check to make sure that event logging was successful
                            bufferReturnInfo = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Log Beep", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            CheckForReturnError(bufferReturnInfo, "Log Beep");
                            BeepLoggedRight = "Beep Logged";
                        }
                        catch (Exception error)
                        {
                            _log.Error(error);
                        }
                    }
                }
            }
            previousOnFlag = currentOnFlag;
        }
        #endregion

        #region Functions to Dispose of Summit System/Manager or Both
        /// <summary>
        /// Disposes of both summit manager and summit system
        /// </summary>
        public static void DisposeSummitManagerAndSystem()
        {
            DisposeSummitSystem();
            DisposeSummitManager();
        }

        /// <summary>
        /// Dispose just the SummitManager
        /// Called after disposing of summit system
        /// </summary>
        private static void DisposeSummitManager()
        {
            try
            {
                if (theSummitManager != null)
                {
                    theSummitManager.Dispose();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Error Disposing Summit Manager");
                _log.Error(e);
            }
        }

        /// <summary>
        /// Dispose just the SummitSystem
        /// </summary>
        private static void DisposeSummitSystem()
        {
            try
            {
                if (theSummitManager != null)
                {
                    //Dispose of both summit systems
                    if (theSummitLeft != null)
                    {
                        if (!theSummitLeft.IsDisposed)
                        {
                            Console.WriteLine("Disposing of Summit System");
                            theSummitManager.DisposeSummit(theSummitLeft);
                        }
                        theSummitLeft = null;
                    }
                    if (theSummitRight != null)
                    {
                        if (!theSummitRight.IsDisposed)
                        {
                            Console.WriteLine("Disposing of Summit System");
                            theSummitManager.DisposeSummit(theSummitRight);
                        }
                        theSummitRight = null;
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Error Disposing Summit System");
                _log.Error(e);
            }
        }
        #endregion

        #region Methods for Changing Colors (INS background, CTM background, Stream background)
        private void ResetColorsAndStatusValuesForUILeft()
        {
            TurnCTMGrayLeft();
            TurnINSGrayLeft();
            TurnStreamingGrayLeft();
            CTMLeftBatteryLevel = "%";
            INSLeftBatteryLevel = "%";
            ActiveGroupLeft = "";
            StimStateLeft = "";
            StimAmpLeft = "";
            StimRateLeft = "";
            LaptopBatteryLevel = "";
        }

        private void ResetColorsAndStatusValuesForUIRight()
        {
            TurnCTMGrayRight();
            TurnINSGrayRight();
            TurnStreamingGrayRight();
            CTMRightBatteryLevel = "%";
            INSRightBatteryLevel = "%";
            ActiveGroupRight = "";
            StimStateRight = "";
            StimAmpRight = "";
            StimRateRight = "";
        }

        private void TurnINSGreenRight()
        {
            BorderINSRightBackground = Brushes.ForestGreen;
        }

        private void TurnINSYellowRight()
        {
            BorderINSRightBackground = Brushes.Yellow;
        }

        private void TurnINSGrayRight()
        {
            BorderINSRightBackground = Brushes.LightGray;
        }

        private void TurnCTMGreenRight()
        {
            BorderCTMRightBackground = Brushes.ForestGreen;
        }
        private void TurnCTMYellowRight()
        {
            BorderCTMRightBackground = Brushes.Yellow;
        }

        private void TurnCTMGrayRight()
        {
            BorderCTMRightBackground = Brushes.LightGray;
        }

        private void TurnINSGreenLeft()
        {
            BorderINSLeftBackground = Brushes.ForestGreen;
        }

        private void TurnINSYellowLeft()
        {
            BorderINSLeftBackground = Brushes.Yellow;
        }

        private void TurnINSGrayLeft()
        {
            BorderINSLeftBackground = Brushes.LightGray;
        }

        private void TurnCTMGreenLeft()
        {
            BorderCTMLeftBackground = Brushes.ForestGreen;
        }
        private void TurnCTMYellowLeft()
        {
            BorderCTMLeftBackground = Brushes.Yellow;
        }

        private void TurnCTMGrayLeft()
        {
            BorderCTMLeftBackground = Brushes.LightGray;
        }

        private void TurnStreamingGreenRight()
        {
            BorderStreamRightBackground = Brushes.ForestGreen;
        }

        private void TurnStreamingYellowRight()
        {
            BorderStreamRightBackground = Brushes.Yellow;
        }

        private void TurnStreamingGrayRight()
        {
            BorderStreamRightBackground = Brushes.LightGray;
        }

        private void TurnStreamingGreenLeft()
        {
            BorderStreamLeftBackground = Brushes.ForestGreen;
        }

        private void TurnStreamingYellowLeft()
        {
            BorderStreamLeftBackground = Brushes.Yellow;
        }

        private void TurnStreamingGrayLeft()
        {
            BorderStreamLeftBackground = Brushes.LightGray;
        }
        #endregion
    }
}
