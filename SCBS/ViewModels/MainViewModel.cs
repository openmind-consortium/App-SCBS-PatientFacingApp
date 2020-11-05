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

namespace SCBS.ViewModels
{
    /// <summary>
    /// Main Class that contains window closing and many UI bindings
    /// </summary>
    public partial class MainViewModel : Screen
    {
        #region Variables
        //logger
        private static readonly ILog _log = LogManager.GetLog(typeof(MainViewModel));
        //location of sense files
        private static readonly string senseLeftFileLocation = @"C:\SCBS\senseLeft_config.json";
        private static readonly string senseRightFileLocation = @"C:\SCBS\senseRight_config.json";
        private static readonly string applicationFileLocation = @"C:\SCBS\application_config.json";
        private static readonly string PROJECT_ID = "SummitContinuousBilateralStreaming";
        private static Thread workerThreadLeft;
        private static Thread workerThreadRight;
        private static Thread startThread;
        private static Thread alignThread;
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
        private static bool canConnect = true;
        private static bool isBilateral = false;
        private static bool _isSwitchVisible = false;
        private static bool _isAlignVisible = false;
        private static bool _stimDataVisible = true;
        private static bool _isSpinnerVisible = false;
        private static bool _reportButtonVisible = false;
        private string _connectButtonText;
        private Brush _connectButtonColor;
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
        private string _progressText = "";
        private bool _webPageOneButtonEnabled, _webPageTwoButtonEnabled, _montageButtonEnabled, _stimSweepButtonEnabled, _newSessionButtonEnabled, _moveGroupButtonEnabled;
        private string _webPageOneButtonText = "";
        private string _webPageTwoButtonText = "";
        private string _moveGroupButtonText = "";
        private string _beepLoggedRight, _beepLoggedLeft;
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
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

            //Load the left_default sense config file. This will always load even in unilateral case
            senseLeftConfigModel = jSONService?.GetSenseModelFromFile(senseLeftFileLocation);
            if (senseLeftConfigModel == null)
            {
                return;
            }
            //Check to see if the sense setup is going to have major packet loss due to too much data over bandwidth.
            if (!CheckPacketLoss(senseLeftConfigModel))
            {
                ShowMessageBox("ERROR in Left/Default - Either packet loss over maximum or config file incorrect.  Please check before proceeding.");
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
                //Check to see if the sense setup is going to have major packet loss due to too much data over bandwidth.
                if (!CheckPacketLoss(senseRightConfigModel))
                {
                    ShowMessageBox("ERROR in Right/Bilateral - Either packet loss over maximum or config file incorrect.  Please check before proceeding.");
                }
            }

            //Show switch button and hide stim data such as amp and frequency. This is so patient doesn't know what amp or hz they are at
            if (appConfigModel.Switch)
            {
                IsSwitchVisible = true;
                StimDataVisible = false;
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

            if(appConfigModel.MoveGroupButton != null)
            {
                if (appConfigModel.MoveGroupButton.MoveGroupButtonEnabled)
                {
                    MoveGroupButtonEnabled = true;
                    MoveGroupButtonText = appConfigModel.MoveGroupButton.MoveGroupButtonText;
                }
            }

            //Initialize to listen for a beep noise.
            if (appConfigModel.LogBeepEvent)
            {
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

            //Setup Stop watch to show user how long they are streaming for.
            dispatcherTimerLeft.Tick += new EventHandler(dt_TickLeft);
            dispatcherTimerLeft.Interval = new TimeSpan(0, 0, 0, 1);
            dispatcherTimerRight.Tick += new EventHandler(dt_TickRight);
            dispatcherTimerRight.Interval = new TimeSpan(0, 0, 0, 1);
        }

        #region Button Clicks
        /// <summary>
        /// Starts up the stim sweep window.
        /// </summary>
        /// <returns></returns>
        public void StimSweepButtonClick()
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
            IsSpinnerVisible = true;
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
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group B";

                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    ActiveGroupRight = "Group B";

                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(3000);

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(4000);

                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(3000);

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(2000);

                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group A";

                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    ActiveGroupRight = "Group A";
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
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group B";

                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    ActiveGroupRight = "Group B";

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(3000);

                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(4000);

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(3000);

                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(2000);

                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group A";

                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    ActiveGroupRight = "Group A";
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
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group B";

                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    ActiveGroupRight = "Group B";

                    //Change stim
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(3000);

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(4000);

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(3000);

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(2000);

                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group A";

                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    ActiveGroupRight = "Group A";
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
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group B";

                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group1);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    ActiveGroupRight = "Group B";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(3000);

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(4000);

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyOff";
                    Thread.Sleep(3000);

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";

                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    StimStateRight = "TherapyActive";
                    Thread.Sleep(2000);

                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group A";

                    try
                    {
                        summitSensing.StopStreaming(theSummitRight, false);
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitRight.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupRight();
                        return;
                    }
                    ActiveGroupRight = "Group A";
                }
                else
                {
                    MessageBox.Show("Could not read therapy status. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward..", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    IsSpinnerVisible = false;
                    return;
                }
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
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to Group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group B";

                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        return;
                    }
                    StimStateLeft = "TherapyOff";
                    Thread.Sleep(3000);

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";
                    Thread.Sleep(4000);

                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";
                    Thread.Sleep(3000);

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";
                    Thread.Sleep(2000);

                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group A";
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
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group B. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group B";

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn(); 
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";
                    Thread.Sleep(3000);

                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";
                    Thread.Sleep(4000);

                    //Turn stim on
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOn();
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy on. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyActive";
                    Thread.Sleep(3000);

                    //Turn stim off
                    try
                    {
                        counter = 5;
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeTherapyOff(false);
                            counter--;
                        } while (bufferReturnInfo.RejectCode != 0 && counter > 5);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error turning stim therapy off. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    StimStateLeft = "TherapyOff";
                    Thread.Sleep(2000);

                    //Change to group A
                    try
                    {
                        counter = 5;
                        summitSensing.StopStreaming(theSummitLeft, false);
                        do
                        {
                            bufferReturnInfo = theSummitLeft.StimChangeActiveGroup(ActiveGroup.Group0);
                            counter--;
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
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    if (!CheckReturnCodeForClinician(bufferReturnInfo) || counter == 0)
                    {
                        ShowMessageBox("Error moving to group A. Please try again. Settings may not be in original state. Be sure to correct settings before moving forward.");
                        IsSpinnerVisible = false;
                        UpdateStimStatusGroupLeft();
                        return;
                    }
                    ActiveGroupLeft = "Group A";
                }
            }
            IsSpinnerVisible = false;
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
            //If first time connecting, check that summit manager is null so we don't create another one
            if (theSummitManager == null)
            {
                _log.Info("Initializing Summit Manager");
                theSummitManager = new SummitManager(PROJECT_ID, 200, appConfigModel.VerboseLogOnForMedtronic);
            }
            
            //If we're not connected already, start the worker thread to connect
            if (CanConnect)
            {
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
            OpenUri(appConfigModel.WebPageButtons.WebPageOneURL);
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
            OpenUri(appConfigModel.WebPageButtons.WebPageTwoURL);
        }

        /// <summary>
        /// Moves to group designated in the app config file
        /// </summary>
        public async Task MoveGroupButtonClick()
        {
            if (canConnect)
            {
                MessageBox.Show("Not Connected to INS", "Error moving groups on INS", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            SummitStim summitStim = new SummitStim(_log);
            HelperFunctions helperFunctions = new HelperFunctions();
            if (helperFunctions.CheckGroupIsCorrectFormat(appConfigModel.MoveGroupButton.GroupToMoveToLeftUnilateral))
            {
                Tuple<bool, string> valueReturn = await Task.Run(() => summitStim.ChangeActiveGroup(theSummitLeft, helperFunctions.ConvertStimModelGroupToAPIGroup(appConfigModel.MoveGroupButton.GroupToMoveToLeftUnilateral), senseLeftConfigModel));
                if (!valueReturn.Item1)
                {
                    MessageBox.Show(valueReturn.Item2, "Error moving group on Left INS", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    await Task.Run(UpdateStimStatusGroupLeft);
                }
            }
            if (isBilateral)
            {
                if (helperFunctions.CheckGroupIsCorrectFormat(appConfigModel.MoveGroupButton.GroupToMoveToRight))
                {
                    Tuple<bool, string> valueReturn = summitStim.ChangeActiveGroup(theSummitRight, helperFunctions.ConvertStimModelGroupToAPIGroup(appConfigModel.MoveGroupButton.GroupToMoveToRight), senseRightConfigModel);
                    if (!valueReturn.Item1)
                    {
                        MessageBox.Show(valueReturn.Item2, "Error moving group on Right INS", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        await Task.Run(UpdateStimStatusGroupRight);
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
        public async Task ExitButtonClick()
        {
            _shouldStopWorkerThread = true;
            if (theSummitLeft != null)
            {
                if (!theSummitLeft.IsDisposed)
                {
                    try
                    {
                        await Task.Run(() => theSummitLeft.WriteSensingState(SenseStates.None, 0x00));
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
                        await Task.Run(() => theSummitRight.WriteSensingState(SenseStates.None, 0x00));
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }
            }
            Thread.Sleep(500);
            // perform clean up. Abort continuous connection worker thread and dispose of summit system/manager
            await Task.Run(() => DisposeSummitManagerAndSystem());
            Environment.Exit(0);
        }
        #endregion

        #region UI Binding Elements
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
        /// Hides the stim data if switch is enabled if set to true
        /// </summary>
        public bool StimDataVisible
        {
            get { return _stimDataVisible; }
            set
            {
                _stimDataVisible = value;
                NotifyOfPropertyChange(() => StimDataVisible);
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
        public bool CanConnect
        {
            get { return canConnect; }
            set
            {
                canConnect = value;
                NotifyOfPropertyChange(() => CanConnect);
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
            get { return _CTMLeftBatteryLevel ?? (_CTMLeftBatteryLevel = "Not Connected"); }
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
            get { return _CTMRightBatteryLevel ?? (_CTMRightBatteryLevel = "Not Connected"); }
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
            get { return _INSLeftBatteryLevel ?? (_INSLeftBatteryLevel = "Not Connected"); }
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
            get { return _INSRightBatteryLevel ?? (_INSRightBatteryLevel = "Not Connected"); }
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
                NotifyOfPropertyChange(() => StimStateLeft);
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
            CTMLeftBatteryLevel = "Not Connected";
            INSLeftBatteryLevel = "Not Connected";
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
            CTMRightBatteryLevel = "Not Connected";
            INSRightBatteryLevel = "Not Connected";
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
