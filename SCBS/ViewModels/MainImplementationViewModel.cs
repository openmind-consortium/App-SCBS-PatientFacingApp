using Caliburn.Micro;
using Medtronic.SummitAPI.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Threading;
using SCBS.Services;
using Medtronic.TelemetryM;
using SCBS.Models;
using Medtronic.NeuroStim.Olympus.DataTypes.Sensing;
using System.Windows;
using Medtronic.SummitAPI.Events;
using System.Timers;
using System.Diagnostics;
using Medtronic.SummitAPI.Flash;
using Medtronic.NeuroStim.Olympus.DataTypes.Core.DataManagement;
using System.IO;
using Medtronic.NeuroStim.Olympus.Commands.Core.DataManagement;
using Medtronic.TelemetryM.CtmProtocol.Commands;
using System.Management;

namespace SCBS.ViewModels
{
    public partial class MainViewModel : Screen
    {
        private static readonly object mainViewModelLock = new object();
        private static volatile SummitSystem theSummitLeft = null;
        private static volatile SummitSystem theSummitRight = null;
        private ConnectLeft connectLeft = new ConnectLeft();
        private ConnectRight connectRight = new ConnectRight();
        private BatteryLevels batteryLevelLeft = new BatteryLevels();
        private BatteryLevels batteryLevelRight = new BatteryLevels();
        private SummitStimulationInfo stimInfoLeft;
        private SummitStimulationInfo stimInfoRight;
        private volatile SummitSensing summitSensing;
        //If both connected then disable connect button
        private bool isLeftConnected = false;
        private bool isRightConnected = false;
        //This is used to check return values from medtronic api calls
        //Each return value is checked if reject code is an error. 
        //If it is an error, then error handling is done
        private APIReturnInfo bufferReturnInfo;
        private volatile static System.Timers.Timer aTimerLeft = new System.Timers.Timer();
        private static bool flagForStreamingPacketsLeft = false;
        private volatile static System.Timers.Timer aTimerRight = new System.Timers.Timer();
        private static bool flagForStreamingPacketsRight = false;
        //Variables for checking battery level above amount
        private static bool insBatteryLevelOverMinAmoutLeft = true;
        private static int minINSBatteryLevelAmount = 10;
        private static bool insBatteryLevelOverMinAmoutRight = true;
        //Battery timer to check for Battery status
        private static System.Timers.Timer batteryTimerLeft = new System.Timers.Timer();
        private static System.Timers.Timer batteryTimerRight = new System.Timers.Timer();
        //Timer for logging battery status in json event logs
        private static System.Timers.Timer batteryTimerForEventLogLeft = new System.Timers.Timer();
        private static System.Timers.Timer batteryTimerForEventLogRight = new System.Timers.Timer();
        private volatile bool waitUntilCheckForCorrectINS = true;
        private volatile bool summitRightIsReadyForLeftToConnect = false;
        private static string leftPatientID, rightPatientID, leftDeviceID, rightDeviceID;
        private INSDataInfo dataInfoLeft, dataInfoRight;

        #region Worker Threads
        private void WorkerThreadLeft()
        {
            //Keep going until _shouldStopWorkerThread is switched. This is done in closing the window
            while (!_shouldStopWorkerThread)
            {
                if (theSummitManager == null)
                {
                    isLeftConnected = false;
                    CanConnect = true;
                    ConnectButtonText = "Not Connected";
                    ConnectButtonColor = Brushes.LightGray;
                    _log.Warn("Summit Manager null in Left");
                    return;
                }
                //Connect using the SummitConnect class. Return true if connected and false if failed connection
                if (!SummitConnectLeft(theSummitManager))
                {
                    ResetColorsAndStatusValuesForUILeft();
                    isLeftConnected = false;
                    CanConnect = true;
                    ConnectButtonText = "Connecting";
                    ConnectButtonColor = Brushes.Yellow;
                    _log.Warn("Connect unsuccessful in Left");
                    if (_shouldStopWorkerThread)
                        break;
                    Thread.Sleep(1000);
                }
                else
                {
                    _log.Info("Connection Successful in Left");

                    //Need to check if the INS got switched up.
                    //IF they did then we need to switch the summit systems so that the correct INS can get the correct config file.
                    //One issue is that the ctm for that side will be backwards, but better to have correct INS connection
                    dataInfoLeft = new INSDataInfo(_log, theSummitLeft);
                    leftPatientID = dataInfoLeft.GetPatientID();
                    if (leftPatientID == null)
                    {
                        _log.Warn("Bilateral: Could not find patient id for patient.");
                        ShowMessageBox("Critical error: could not get patient ID from Medtonic API... The program cannot continue and will be closing down!");
                        ExitButtonClick();
                        return;
                    }
                    if (isBilateral)
                    {
                        while (!summitRightIsReadyForLeftToConnect)
                        {
                            Thread.Sleep(300);
                        }
                        dataInfoRight = new INSDataInfo(_log, theSummitRight);
                        if (leftPatientID[leftPatientID.Length - 1].Equals('R'))
                        {
                            _log.Info("Bilateral: INS in incorrect order. Switching order");
                            rightPatientID = dataInfoRight.GetPatientID();
                            if (rightPatientID == null)
                            {
                                _log.Warn("Bilateral: Could not find patient id for patient.");
                                ShowMessageBox("Critical error: could not get patient ID from Medtonic API... The program cannot continue and will be closing down!");
                                ExitButtonClick();
                                return;
                            }
                            if (theSummitRight != null)
                            {
                                //Switch summit systems
                                SummitSystem tempSummit = theSummitLeft;
                                theSummitLeft = theSummitRight;
                                theSummitRight = tempSummit;
                                //switch patient ID
                                string tempPatientID = leftPatientID;
                                leftPatientID = rightPatientID;
                                rightPatientID = tempPatientID;
                                INSDataInfo tempdata = dataInfoLeft;
                                dataInfoLeft = dataInfoRight;
                                dataInfoRight = tempdata;
                                _log.Info("After switch Left Patient ID: " + leftPatientID);
                                _log.Info("After switch Right Patient ID: " + rightPatientID);
                            }
                        }
                        else if (!leftPatientID[leftPatientID.Length - 1].Equals('L'))
                        {
                            _log.Warn("Bilateral: INS in incorrect order. Cannot switch order. Need to check that INS names are correct.");
                            ShowMessageBox("Error occurred finding INS name ending in either L or R. Please fix with RLP and name INS ending in either L or R... Closing window");
                            Environment.Exit(0);
                        }
                        else
                        {
                            //Get patient ID anyway for the right side
                            rightPatientID = dataInfoRight.GetPatientID();
                        }
                        //Log the location data for right
                        try
                        {
                            theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "LeadLocationOne", dataInfoRight.GetLeadLocationOne());
                            theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "LeadLocationTwo", dataInfoRight.GetLeadLocationTwo());
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    waitUntilCheckForCorrectINS = false;
                    //log the location data for left
                    try
                    {
                        theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "LeadLocationOne", dataInfoLeft.GetLeadLocationOne());
                        theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "LeadLocationTwo", dataInfoLeft.GetLeadLocationTwo());
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }

                    //Update UI for statuses
                    UpdateStimStatusGroupLeft();
                    GetBatteryLevelsLeft();
                    isLeftConnected = true;

                    //Get Device info
                    leftDeviceID = dataInfoLeft.GetDeviceID(theSummitLeft);
                    if (leftDeviceID == null)
                    {
                        _log.Warn("Bilateral: Could not find device id from medtronic api.");
                        ShowMessageBox("Could not get patient ID from Medtonic API... The program cannot save files to current session directory.");
                    }
                    _log.Info("Left Device ID: " + leftDeviceID);
                    //check to make sure battery level over certain value. If it isn't, the user needs to recharge and program stopped.
                    if (!insBatteryLevelOverMinAmoutLeft)
                    {
                        ResetColorsAndStatusValuesForUILeft();
                        MessageBox.Show("INS battery below " + minINSBatteryLevelAmount + " %. Please Recharge INS", "Warning", MessageBoxButton.OK, MessageBoxImage.Hand);
                        INSLeftBatteryLevel = "Recharge";
                        _log.Info("INS battery below " + minINSBatteryLevelAmount + "%.  Stopping sensing");
                        return;
                    }

                    string logPath = "";
                    //Create filepath for left or right
                    if (appConfigModel.GetAdaptiveLogInfo || appConfigModel.GetAdaptiveMirrorInfo)
                    {
                        logPath = GetDirectoryPathForCurrentSession(theSummitLeft, PROJECT_ID, leftPatientID, leftDeviceID);
                        logPath += @"\LogDataFromLeftINS\";  
                    }
                    //Get Log info and write to file
                    if (appConfigModel.GetAdaptiveLogInfo)
                    {
                        IsSpinnerVisible = true;
                        //run get logs
                        GetApplicationLogInfo(theSummitLeft, logPath);
                        IsSpinnerVisible = false;
                    }
                    //Get mirror info and write to file
                    if (appConfigModel.GetAdaptiveMirrorInfo)
                    {
                        IsSpinnerVisible = true;
                        //run get logs
                        GetApplicationMirrorData(theSummitLeft, logPath, appConfigModel);
                        IsSpinnerVisible = false;
                    }

                    //Tries to configure and start sensing for 5 times. If it doesn't work then continue main while loop. 
                    //if it does work and gets to the last item, then break out of while loop
                    //this ensures that if we are already connected that we don't continue the main while loop and try and connect again.
                    int counter = 5;
                    while(counter > 0)
                    {
                        try
                        {
                            if (!summitSensing.SummitConfigureSensing(theSummitLeft, senseLeftConfigModel, true))
                            {
                                _log.Info("Could not configure sensing in Left");
                                counter--;
                                continue;
                            }
                        }
                        catch (Exception error)
                        {
                            _log.Error(error);
                            ShowMessageBox("Could not configure sensing in Left. Please check that config file is correct");
                            counter--;
                            continue;
                        }

                        try
                        {
                            if (!summitSensing.StartSensingAndStreaming(theSummitLeft, senseLeftConfigModel, true))
                            {
                                _log.Info("Could not start sensing in Left");
                                counter--;
                                continue;
                            }
                        }
                        catch (Exception error)
                        {
                            _log.Error(error);
                            ShowMessageBox("Could not configure sensing in Left. Please check that config file is correct");
                            counter--;
                            continue;
                        }
                        if (!RegisterDataListeners(theSummitLeft, true))
                        {
                            _log.Info("Could not register data listeners in Left");
                            continue;
                        }
                        break;
                    }
                    if(counter == 0)
                    {
                        continue;
                    }

                    SensingState state;
                    //This checks to see if sensing is already enabled. This can happen if adaptive is already running and we don't need to configure it. 
                    //If it is, then skip setting up sensing
                    try
                    {
                        theSummitLeft.ReadSensingState(out state);
                        if (state.State.ToString().Contains("DetectionLd0") && ActiveGroupLeft.Equals("Group D"))
                        {
                            AdaptiveRunningLeft = "Adaptive On";
                            counter = 5;
                            do
                            {
                                bufferReturnInfo = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "Adaptive On", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                            if (counter == 0)
                            {
                                MessageBox.Show("Could not log Adaptive On in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        _log.Error(error);
                    }

                    // Create the timer to periodically retrieve battery status
                    Console.WriteLine("Setup Timer for battery check");
                    TimeSpan interval = new TimeSpan(0, 0, 180);
                    batteryTimerLeft.Interval = interval.TotalMilliseconds;
                    batteryTimerLeft.AutoReset = true;
                    batteryTimerLeft.Elapsed += BatteryLevelTimedHandlerLeft;
                    batteryTimerLeft.Enabled = true;

                    TimeSpan intervalForEventLog = new TimeSpan(0, 10, 0);
                    batteryTimerForEventLogLeft.Interval = intervalForEventLog.TotalMilliseconds;
                    batteryTimerForEventLogLeft.AutoReset = true;
                    batteryTimerForEventLogLeft.Elapsed += BatteryLevelForEventLogTimedHandlerLeft;
                    batteryTimerForEventLogLeft.Enabled = true;

                    //Get laptop battery level
                    LaptopBatteryLevel = GetLaptopBatteryPercent().ToString();

                    //Start stopwatch
                    stopWatchLeft.Start();
                    dispatcherTimerLeft.Start();
                    // Worker thread to loop in inner loop while system is connected
                    Console.WriteLine("Inside the worker loop");
                    _log.Info("Inside worker loop in left");
                    //_shouldStopDoWhileLoop will stop the loop if theSummitLeft is disposed and main while loop will run summitConnect again to reconnect.
                    bool _shouldStopDoWhileLoop = false;
                    do
                    {
                        Thread.Sleep(1000);
                        if (theSummitLeft != null)
                        {
                            if (theSummitLeft.IsDisposed)
                            {
                                _log.Info("Summit disposed in left");
                                //if summit is disposed, reconnect
                                _shouldStopDoWhileLoop = true;
                            }
                        }
                        else
                        {
                            //if summit is null, reconnect
                            _shouldStopDoWhileLoop = true;
                        }
                        if (isRightConnected && isLeftConnected && isBilateral)
                        {
                            CanConnect = false;
                            ConnectButtonText = "Connected";
                            ConnectButtonColor = Brushes.ForestGreen;
                            IsSpinnerVisible = false;

                        }
                        else if(isLeftConnected && !isBilateral)
                        {
                            CanConnect = false;
                            ConnectButtonText = "Connected";
                            ConnectButtonColor = Brushes.ForestGreen;
                            IsSpinnerVisible = false;
                        }
                        if (!insBatteryLevelOverMinAmoutLeft)
                        {
                            summitSensing.StopSensing(theSummitLeft, true);
                            ResetColorsAndStatusValuesForUILeft();
                            INSLeftBatteryLevel = "Recharge";
                            ShowMessageBox("INS battery below " + minINSBatteryLevelAmount + "%.  Stopping sensing.  Please Recharge INS.  SHUTTING DOWN...");
                            _shouldStopWorkerThread = true;
                            _shouldStopDoWhileLoop = true;
                            _log.Info("INS battery below " + minINSBatteryLevelAmount + "%.  Stopping sensing");
                        }
                    } while (!_shouldStopDoWhileLoop && !_shouldStopWorkerThread);
                    //Stop stopwatch
                    stopWatchLeft.Stop();
                    dispatcherTimerLeft.Stop();
                    ResetColorsAndStatusValuesForUILeft();
                    aTimerLeft.Enabled = false;
                    batteryTimerLeft.Enabled = false;
                    isLeftConnected = false;
                    CanConnect = true;
                    ConnectButtonText = "Connecting";
                    ConnectButtonColor = Brushes.Yellow;
                }
            }
            DisposeSummitManagerAndSystem();
        }

        private void WorkerThreadRight()
        {
            //Keep going until _shouldStopWorkerThread is switched. This is done in closing the window
            while (!_shouldStopWorkerThread)
            {
                if (theSummitManager == null)
                {
                    isRightConnected = false;
                    CanConnect = true;
                    ConnectButtonText = "Not Connected";
                    ConnectButtonColor = Brushes.LightGray;
                    _log.Warn("Summit Manager null in Right");
                    return;
                }
                //Connect using the SummitConnect class. Return true if connected and false if failed connection
                if (!SummitConnectRight(theSummitManager))
                {
                    ResetColorsAndStatusValuesForUIRight();
                    isRightConnected = false;
                    CanConnect = true;
                    ConnectButtonText = "Connecting";
                    ConnectButtonColor = Brushes.Yellow;
                    _log.Warn("Connect unsuccessful in Right");
                    if (_shouldStopWorkerThread)
                        break;
                    Thread.Sleep(1000);
                }
                else
                {
                    _log.Info("Connection Successful in Right");

                    summitRightIsReadyForLeftToConnect = true;
                    //need to wait until left is done checking to see if the INS were switched around.
                    //Once done then it can move on
                    while (waitUntilCheckForCorrectINS)
                    {
                        Thread.Sleep(300);
                    }

                    //Update UI for statuses
                    UpdateStimStatusGroupRight();
                    GetBatteryLevelsRight();
                    isRightConnected = true;

                    //Get device info
                    if(dataInfoRight == null)
                    {
                        dataInfoRight = new INSDataInfo(_log, theSummitRight);
                    }
                    rightDeviceID = dataInfoRight.GetDeviceID(theSummitRight);
                    if (rightDeviceID == null)
                    {
                        _log.Warn("Bilateral: Could not find device id from medtronic api.");
                        ShowMessageBox("Could not get patient ID from Medtonic API... The program cannot save files to current session directory.");
                    }
                    _log.Info("Right Device ID: " + rightDeviceID);
                    //check to make sure battery level over certain value. If it isn't, the user needs to recharge and program stopped.
                    if (!insBatteryLevelOverMinAmoutRight)
                    {
                        ResetColorsAndStatusValuesForUIRight();
                        INSRightBatteryLevel = "Recharge";
                        MessageBox.Show("INS battery below " + minINSBatteryLevelAmount + " %. Please Recharge INS", "Warning", MessageBoxButton.OK, MessageBoxImage.Hand);
                        _log.Info("INS battery below in Right " + minINSBatteryLevelAmount + "%.  Stopping sensing");
                        return;
                    }

                    string logPath = "";
                    //Create filepath for left or right
                    if (appConfigModel.GetAdaptiveLogInfo || appConfigModel.GetAdaptiveMirrorInfo)
                    {
                        logPath = GetDirectoryPathForCurrentSession(theSummitRight, PROJECT_ID, rightPatientID, rightDeviceID);
                        logPath += @"\LogDataFromRightINS\";
                    }

                    //Get Log info and write to file if set to true
                    if (appConfigModel.GetAdaptiveLogInfo)
                    {
                        IsSpinnerVisible = true;
                        //run get logs
                        GetApplicationLogInfo(theSummitRight, logPath);
                        IsSpinnerVisible = false;
                    }

                    //Get mirror data and write to file if set to true
                    if (appConfigModel.GetAdaptiveMirrorInfo)
                    {
                        IsSpinnerVisible = true;
                        //run get logs
                        GetApplicationMirrorData(theSummitRight, logPath, appConfigModel);
                        IsSpinnerVisible = false;
                    }
                    //Tries to configure and start sensing for 5 times. If it doesn't work then continue main while loop. 
                    //if it does work and gets to the last item, then break out of while loop
                    //this ensures that if we are already connected that we don't continue the main while loop and try and connect again.
                    _log.Info("Right side configure sense");
                    int counter = 5;
                    while (counter > 0)
                    {
                        try
                        {
                            if (!summitSensing.SummitConfigureSensing(theSummitRight, senseRightConfigModel, true))
                            {
                                _log.Info("Could not configure sensing in Right");
                                counter--;
                                continue;
                            }
                        }
                        catch (Exception error)
                        {
                            _log.Error(error);
                            ShowMessageBox("Could not configure sensing in Right. Please check that config file is correct");
                            counter--;
                            continue;
                        }

                        try
                        {
                            if (!summitSensing.StartSensingAndStreaming(theSummitRight, senseRightConfigModel, true))
                            {
                                _log.Info("Could not start sensing in Right");
                                counter--;
                                continue;
                            }
                        }
                        catch (Exception error)
                        {
                            _log.Error(error);
                            ShowMessageBox("Could not configure sensing in Right. Please check that config file is correct");
                            counter--;
                            continue;
                        }

                        if (!RegisterDataListeners(theSummitRight, false))
                        {
                            _log.Info("Could not register data listeners in Right");
                            counter--;
                            continue;
                        }
                        break;
                    }
                    if (counter == 0)
                    {
                        _log.Warn("Counter in right while loop for starting stream ran all the way to 0");
                        continue;
                    }
                    _log.Info("Right side read sense state to determine adaptive status");

                    SensingState state;
                    //This checks to see if sensing is already enabled. This can happen if adaptive is already running and we don't need to configure it. 
                    //If it is, then skip setting up sensing
                    try
                    {
                        theSummitRight.ReadSensingState(out state);
                        if (state.State.ToString().Contains("DetectionLd0") && ActiveGroupRight.Equals("Group D"))
                        {
                            AdaptiveRunningRight = "Adaptive On";
                            counter = 5;
                            do
                            {
                                bufferReturnInfo = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "Adaptive On", DateTime.Now.ToString("MM_dd_yyyy hh:mm:ss tt"));
                            } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                            if (counter == 0)
                            {
                                MessageBox.Show("Could not log Adaptive On in event log. Please report error to clinician.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        _log.Error(error);
                    }
                    _log.Info("Right side configure battery");
                    // Create the timer to periodically retrieve battery status
                    Console.WriteLine("Setup Timer for battery check");
                    TimeSpan interval = new TimeSpan(0, 0, 180);
                    batteryTimerRight.Interval = interval.TotalMilliseconds;
                    batteryTimerRight.AutoReset = true;
                    batteryTimerRight.Elapsed += BatteryLevelTimedHandlerRight;
                    batteryTimerRight.Enabled = true;

                    TimeSpan intervalForEventLog = new TimeSpan(0, 10, 0);
                    batteryTimerForEventLogRight.Interval = intervalForEventLog.TotalMilliseconds;
                    batteryTimerForEventLogRight.AutoReset = true;
                    batteryTimerForEventLogRight.Elapsed += BatteryLevelForEventLogTimedHandlerRight;
                    batteryTimerForEventLogRight.Enabled = true;

                    //Start stream timers
                    stopWatchRight.Start();
                    dispatcherTimerRight.Start();

                    // Worker thread to loop in inner loop while system is connected
                    _log.Info("Inside the worker loop Right");
                    //_shouldStopDoWhileLoop will stop the loop if theSummitLeft is disposed and main while loop will run summitConnect again to reconnect.
                    bool _shouldStopDoWhileLoop = false;
                    do
                    {
                        Thread.Sleep(1000);
                        if (theSummitRight != null)
                        {
                            if (theSummitRight.IsDisposed)
                            {
                                _log.Info("summit is disposed in Right");
                                //if summit is disposed, reconnect
                                _shouldStopDoWhileLoop = true;
                            }
                        }
                        else
                        {
                            //if summit is null, reconnect
                            _shouldStopDoWhileLoop = true;
                        }
                        if (isRightConnected && isLeftConnected && isBilateral)
                        {
                            CanConnect = false;
                            ConnectButtonText = "Connected";
                            ConnectButtonColor = Brushes.ForestGreen;
                        }
                        if (!insBatteryLevelOverMinAmoutRight)
                        {
                            summitSensing.StopSensing(theSummitRight, true);
                            _log.Info("INS battery below in Right " + minINSBatteryLevelAmount + "%.  Stopping sensing");
                            ResetColorsAndStatusValuesForUIRight();
                            INSRightBatteryLevel = "Recharge";
                            ShowMessageBox("INS battery below " + minINSBatteryLevelAmount + "%.  Stopping sensing. Please Recharge INS. SHUTTING DOWN...");
                            _shouldStopWorkerThread = true;
                            _shouldStopDoWhileLoop = true;
                        }
                    } while (!_shouldStopDoWhileLoop && !_shouldStopWorkerThread);
                    _log.Warn("Right thread out of worker loop");
                    //Stop stream timers
                    stopWatchRight.Stop();
                    dispatcherTimerRight.Stop();
                    ResetColorsAndStatusValuesForUIRight();
                    batteryTimerRight.Enabled = false;
                    aTimerRight.Enabled = false;
                    isRightConnected = false;
                    CanConnect = true;
                    ConnectButtonText = "Connecting";
                    ConnectButtonColor = Brushes.Yellow;
                }
            }
        }
        #endregion

        #region Battery Levels
        /// <summary>
        /// Timer Elapsed Event Handler for querying the SummitSystem for battery information at regular intervals.
        /// </summary>
        /// <param name="sender">Required field for C# event handlers</param>
        /// <param name="e">Required field for C# event handlers, specific class for timers</param>
        private void BatteryLevelTimedHandlerLeft(object sender, ElapsedEventArgs e)
        {
            GetBatteryLevelsLeft();
            //Periodically get the laptop Battery as well.
            //Only in the left since it needs to be gotten once and if unilateral then the left side is the only one that runs.
            LaptopBatteryLevel = GetLaptopBatteryPercent().ToString();
        }

        /// <summary>
        /// Timer Elapsed Event Handler for querying the SummitSystem for battery information at regular intervals.
        /// </summary>
        /// <param name="sender">Required field for C# event handlers</param>
        /// <param name="e">Required field for C# event handlers, specific class for timers</param>
        private void BatteryLevelTimedHandlerRight(object sender, ElapsedEventArgs e)
        {
            GetBatteryLevelsRight();
        }

        /// <summary>
        /// Timer Elapsed Event Handler for querying the SummitSystem for battery information at regular intervals.
        /// </summary>
        /// <param name="sender">Required field for C# event handlers</param>
        /// <param name="e">Required field for C# event handlers, specific class for timers</param>
        private void BatteryLevelForEventLogTimedHandlerLeft(object sender, ElapsedEventArgs e)
        {
            try
            {
                APIReturnInfo result = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "CTMLeftBatteryLevel", CTMLeftBatteryLevel);
                result = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, "INSLeftBatteryLevel", INSLeftBatteryLevel);
            }
            catch(Exception error)
            {
                _log.Error(error);
            }
        }

        /// <summary>
        /// Timer Elapsed Event Handler for querying the SummitSystem for battery information at regular intervals.
        /// </summary>
        /// <param name="sender">Required field for C# event handlers</param>
        /// <param name="e">Required field for C# event handlers, specific class for timers</param>
        private void BatteryLevelForEventLogTimedHandlerRight(object sender, ElapsedEventArgs e)
        {
            try
            {
                APIReturnInfo result = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "CTMRightBatteryLevel", CTMRightBatteryLevel);
                result = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, "INSRightBatteryLevel", INSRightBatteryLevel);
            }
            catch (Exception error)
            {
                _log.Error(error);
            }
        }
        /// <summary>
        /// Gets the battery level for left
        /// </summary>
        private void GetBatteryLevelsLeft()
        {
            // Read and write the battery level
            //this checks to make sure that INS battery level is above certain amount and sets flag to false if not.
            //The logic is in the checkIfINSisLessThanMinBatteryLevel() method.  
            string tempInsBatteryLevel = batteryLevelLeft.GetINSBatteryLevel(ref theSummitLeft, _log);
            if (CheckIfINSisLessThanMinBatteryLevel(tempInsBatteryLevel))
            {
                insBatteryLevelOverMinAmoutLeft = false;
                Console.WriteLine("INS battery level under min value. Level is: " + tempInsBatteryLevel);
            }
            else
            {
                INSLeftBatteryLevel = tempInsBatteryLevel + "%";
                Console.WriteLine("Current INS Battery Level: " + INSLeftBatteryLevel);
            }

            CTMLeftBatteryLevel = batteryLevelLeft.GetCTMBatteryLevel(ref theSummitLeft, _log) + "%";
            Console.WriteLine("Current CTM Battery Level: " + CTMLeftBatteryLevel);
        }

        /// <summary>
        /// Gets the battery level for right
        /// </summary>
        private void GetBatteryLevelsRight()
        {
            // Read and write the battery level
            //this checks to make sure that INS battery level is above certain amount and sets flag to false if not.
            //The logic is in the checkIfINSisLessThanMinBatteryLevel() method.  
            string tempInsBatteryLevel = batteryLevelRight.GetINSBatteryLevel(ref theSummitRight, _log);
            if (CheckIfINSisLessThanMinBatteryLevel(tempInsBatteryLevel))
            {
                insBatteryLevelOverMinAmoutRight = false;
                Console.WriteLine("INS battery level under min value. Level is: " + tempInsBatteryLevel);
            }
            else
            {
                INSRightBatteryLevel = tempInsBatteryLevel + "%";
                Console.WriteLine("Current INS Battery Level: " + INSRightBatteryLevel);
            }

            CTMRightBatteryLevel = batteryLevelRight.GetCTMBatteryLevel(ref theSummitRight, _log) + "%";
            Console.WriteLine("Current CTM Battery Level: " + CTMRightBatteryLevel);
        }
        /// <summary>
        /// Checks if the battery level is less than the min amount set
        /// </summary>
        /// <param name="batteryLevelForINS">INS battery level</param>
        /// <returns>true if battery level is too low or false if fine</returns>
        private static bool CheckIfINSisLessThanMinBatteryLevel(string batteryLevelForINS)
        {
            if (string.IsNullOrEmpty(batteryLevelForINS))
            {
                return false;
            }
            //Check if battery level has been read and is an integer.
            bool isNumeric = int.TryParse(batteryLevelForINS, out int insBatteryLevelAsInt);
            //if it is an integer, check if it is less than min amout. This allows the battery level to also be a string of Not Connected
            if (isNumeric)
            {
                if (insBatteryLevelAsInt <= minINSBatteryLevelAmount && insBatteryLevelAsInt != 0)
                {
                    return true;
                }
            }
            //Always return false unless the battery level has been read and it is a value that is lesser than minimum amount.
            return false;
        }
        /// <summary>
        /// Gets the percent of power remaining in the battery.
        /// </summary>
        /// <returns>double representing the battery level percentage</returns>
        private double GetLaptopBatteryPercent()
        {
            ManagementClass wmi = new ManagementClass("Win32_Battery");
            ManagementObjectCollection allBatteries = wmi.GetInstances();

            double batteryLevel = 0;

            foreach (var battery in allBatteries)
            {
                batteryLevel = Convert.ToDouble(battery["EstimatedChargeRemaining"]);
            }

            return batteryLevel;
        }
        #endregion

        #region Status Info
        /// <summary>
        /// Updates the UI for Stim Status for Left
        /// </summary>
        public void UpdateStimStatusGroupLeft()
        {
            ActiveGroupLeft = stimInfoLeft.GetActiveGroup(ref theSummitLeft);
            StimStateLeft = stimInfoLeft.GetTherapyStatus(ref theSummitLeft);
            StimParameterModel localModel = new StimParameterModel("", "", "", "", null);
            localModel = GetStimParamsBasedOnGroup(theSummitLeft, stimInfoLeft, ActiveGroupLeft);
            StimAmpLeft = localModel.StimAmp;
            StimRateLeft = localModel.StimRate;
            StimElectrodeLeft = localModel.StimElectrodes;
        }
        /// <summary>
        /// Updates the UI for Stim Status for Right
        /// </summary>
        public void UpdateStimStatusGroupRight()
        {
            ActiveGroupRight = stimInfoRight.GetActiveGroup(ref theSummitRight);
            StimStateRight = stimInfoRight.GetTherapyStatus(ref theSummitRight);
            StimParameterModel localModel = new StimParameterModel("", "", "", "", null);
            localModel = GetStimParamsBasedOnGroup(theSummitRight, stimInfoRight, ActiveGroupRight);
            StimAmpRight = localModel.StimAmp;
            StimRateRight = localModel.StimRate;
            StimElectrodeRight = localModel.StimElectrodes;
        }
        /// <summary>
        /// This maybe should go into a different class like Stimulation.cs, but it's here for now. 
        ///It gets the group stim params based on the group that was read from the device.
        ///if Group b was read from the device, then it gets the params for that specific group.
        /// </summary>
        /// <param name="theSummit">Summit System</param>
        /// <param name="stimulation">The stimulation info model</param>
        /// <param name="group">Active Group after being converted</param>
        /// <returns>StimParameterModel filled with data</returns>
        private StimParameterModel GetStimParamsBasedOnGroup(SummitSystem theSummit, SummitStimulationInfo stimulation, string group)
        {
            StimParameterModel stimParam = new StimParameterModel("", "", "", "", null);
            if (string.IsNullOrEmpty(group))
            {
                return stimParam;
            }
            switch (group)
            {
                case "Group A":
                    stimParam = stimulation.GetStimParameterModelGroupA(ref theSummit);
                    break;
                case "Group B":
                    stimParam = stimulation.GetStimParameterModelGroupB(ref theSummit);
                    break;
                case "Group C":
                    stimParam = stimulation.GetStimParameterModelGroupC(ref theSummit);
                    break;
                case "Group D":
                    stimParam = stimulation.GetStimParameterModelGroupD(ref theSummit);
                    break;
                default:
                    break;
            }
            return stimParam;
        }
        #endregion

        #region Connection for Left,  Right
        private bool SummitConnectLeft(SummitManager theSummitManager)
        {
            TurnCTMYellowLeft();
            TurnINSGrayLeft();
            CTMLeftBatteryLevel = "Connecting";
            if (!connectLeft.ConnectCTM(theSummitManager, ref theSummitLeft, senseLeftConfigModel, appConfigModel, _log))
            {
                return false;
            }
            else
            {
                CTMLeftBatteryLevel = "Connected";
                TurnCTMGreenLeft();
                TurnINSYellowLeft();
                INSLeftBatteryLevel = "Connecting";
            }
            if(!connectLeft.ConnectINS(ref theSummitLeft, _log))
            {
                return false;
            }
            else
            {
                TurnINSGreenLeft();
                INSLeftBatteryLevel = "Connected";
            }
            return true;
        }
        private bool SummitConnectRight(SummitManager theSummitManager)
        {
            TurnCTMYellowRight();
            TurnINSGrayRight();
            CTMRightBatteryLevel = "Connecting";
            if (!connectRight.ConnectCTM(theSummitManager, ref theSummitRight, senseRightConfigModel, appConfigModel, _log))
            {
                return false;
            }
            else
            {
                CTMRightBatteryLevel = "Connected";
                TurnCTMGreenRight();
                TurnINSYellowRight();
                INSRightBatteryLevel = "Connecting";
            }
            if (!connectRight.ConnectINS(ref theSummitRight, _log))
            {
                return false;
            }
            else
            {
                TurnINSGreenRight();
                INSRightBatteryLevel = "Connected";
            }
            return true;
        }
        #endregion

        #region Medtronic Data Listeners, register and control UI for left, right
        private bool RegisterDataListeners(SummitSystem theLocalSummit, bool isLeft)
        {
            TimeSpan interval = new TimeSpan(0, 0, 2);
            if (isLeft)
            {
                try
                {
                    theLocalSummit.DataReceivedTDHandler += theSummit_DataReceived_TD_Left;
                    theLocalSummit.DataReceivedPowerHandler += theSummit_DataReceived_Power_Left;
                    theLocalSummit.DataReceivedFFTHandler += theSummit_DataReceived_FFT_Left;
                    theLocalSummit.DataReceivedAccelHandler += theSummit_DataReceived_Accel_Left;
                    flagForStreamingPacketsLeft = true;
                    aTimerLeft.Interval = interval.TotalMilliseconds;
                    // Hook up the event handler for the Elapsed event.
                    aTimerLeft.Elapsed += OnTimedEventLeft;
                    // Only raise the event the first time Interval elapses.
                    aTimerLeft.AutoReset = false;
                    aTimerLeft.Enabled = true;
                    TurnStreamingGreenLeft();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                    return false;
                }
            }
            else
            {
                try
                {
                    theLocalSummit.DataReceivedTDHandler += theSummit_DataReceived_TD_Right;
                    theLocalSummit.DataReceivedPowerHandler += theSummit_DataReceived_Power_Right;
                    theLocalSummit.DataReceivedFFTHandler += theSummit_DataReceived_FFT_Right;
                    theLocalSummit.DataReceivedAccelHandler += theSummit_DataReceived_Accel_Right;
                    flagForStreamingPacketsRight = true;
                    aTimerRight.Interval = interval.TotalMilliseconds;
                    // Hook up the event handler for the Elapsed event.
                    aTimerRight.Elapsed += OnTimedEventRight;
                    // Only raise the event the first time Interval elapses.
                    aTimerRight.AutoReset = false;
                    aTimerRight.Enabled = true;
                    TurnStreamingGreenRight();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Handle the Elapsed event.
        /// If packet hasn't been sent within time limit (shown by the events constantly resetting the time), then turn status gray.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnTimedEventLeft(object source, ElapsedEventArgs e)
        {
            flagForStreamingPacketsLeft = false;
            Console.WriteLine("************Packet stopped. Packet not sent within time limit************");
            TurnStreamingGrayLeft();
            try
            {
                stopWatchLeft.Stop();
                dispatcherTimerLeft.Stop();
            }
            catch (Exception error)
            {
                _log.Error(error);
            }
        }

        /// <summary>
        /// Resets timer
        /// </summary>
        public void ResetTimerLeft()
        {
            if (aTimerLeft != null)
            {
                try
                {
                    aTimerLeft.Stop();
                    aTimerLeft.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
        }

        /// <summary>
        /// Handle the Elapsed event.
        /// If packet hasn't been sent within time limit (shown by the events constantly resetting the time), then turn status gray.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnTimedEventRight(object source, ElapsedEventArgs e)
        {
            flagForStreamingPacketsRight = false;
            Console.WriteLine("************Packet stopped. Packet not sent within time limit************");
            TurnStreamingGrayRight();
            try
            {
                stopWatchRight.Stop();
                dispatcherTimerRight.Stop();
            }
            catch (Exception error)
            {
                _log.Error(error);
            }
        }

        /// <summary>
        /// Resets timer
        /// </summary>
        public void ResetTimerRight()
        {
            if (aTimerRight != null)
            {
                try
                {
                    aTimerRight.Stop();
                    aTimerRight.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
        }
        #endregion

        #region Medtronic Data Listeners, get data for left, right
        /// <summary>
        /// Sensing data received event handlers
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="TdSenseEvent"></param>
        private void theSummit_DataReceived_TD_Left(object sender, SensingEventTD TdSenseEvent)
        {
            // Announce that packet was received by handler
            //Console.WriteLine("TD Packet Received, Global SeqNum:" + TdSenseEvent.Header.GlobalSequence.ToString());
            // Log some information about the received packet out to file
            //theSummit.LogCustomEvent(TdSenseEvent.GenerationTimeEstimate, DateTime.Now, "TdPacketReceived", TdSenseEvent.Header.GlobalSequence.ToString());
            //this if for the ui. If packet has been received and green status isn't on, then turn it on.
            if (!flagForStreamingPacketsLeft)
            {
                flagForStreamingPacketsLeft = true;
                TurnStreamingGreenLeft();
                try
                {
                    stopWatchLeft.Start();
                    dispatcherTimerLeft.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
            //reset timer to show that packet has been sent.
            ResetTimerLeft();
        }

        private void theSummit_DataReceived_FFT_Left(object sender, SensingEventFFT FftSenseEvent)
        {
            // Announce that packet was received by handler 
            //Console.WriteLine("FFT Packet Received, Global SeqNum:" + FftSenseEvent.Header.GlobalSequence.ToString());
            // Log some information about the received packet out to file
            //theSummit.LogCustomEvent(FftSenseEvent.GenerationTimeEstimate, DateTime.Now, "TdPacketReceived", FftSenseEvent.Header.GlobalSequence.ToString());
            ResetTimerLeft();
            if (!flagForStreamingPacketsLeft)
            {
                flagForStreamingPacketsLeft = true;
                TurnStreamingGreenLeft();
                try
                {
                    stopWatchLeft.Start();
                    dispatcherTimerLeft.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
        }

        private void theSummit_DataReceived_Power_Left(object sender, SensingEventPower PowerSenseEvent)
        {
            // Announce that packet was received by handler

            //Console.WriteLine("Power Packet Received, Global SeqNum:" + PowerSenseEvent.Header.GlobalSequence.ToString());
            // Log some information about the received packet out to file
            //theSummit.LogCustomEvent(PowerSenseEvent.GenerationTimeEstimate, DateTime.Now, "TdPacketReceived", PowerSenseEvent.Header.GlobalSequence.ToString());
            ResetTimerLeft();
            if (!flagForStreamingPacketsLeft)
            {
                flagForStreamingPacketsLeft = true;
                TurnStreamingGreenLeft();
                try
                {
                    stopWatchLeft.Start();
                    dispatcherTimerLeft.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
        }

        private void theSummit_DataReceived_Accel_Left(object sender, SensingEventAccel AccelSenseEvent)
        {
            // Announce that packet was received by handler
            //Console.WriteLine("AccelPacket Received, Global SeqNum:" + AccelSenseEvent.Header.GlobalSequence.ToString());
            // Log some information about the received packet out to file
            //theSummit.LogCustomEvent(AccelSenseEvent.GenerationTimeEstimate, DateTime.Now, "TdPacketReceived", AccelSenseEvent.Header.GlobalSequence.ToString());
            ResetTimerLeft();
            if (!flagForStreamingPacketsLeft)
            {
                flagForStreamingPacketsLeft = true;
                TurnStreamingGreenLeft();
                try
                {
                    stopWatchLeft.Start();
                    dispatcherTimerLeft.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
        }

        /// <summary>
        /// Sensing data received event handlers
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="TdSenseEvent"></param>
        private void theSummit_DataReceived_TD_Right(object sender, SensingEventTD TdSenseEvent)
        {
            // Announce that packet was received by handler
            //Console.WriteLine("TD Packet Received, Global SeqNum:" + TdSenseEvent.Header.GlobalSequence.ToString());
            // Log some information about the received packet out to file
            //theSummit.LogCustomEvent(TdSenseEvent.GenerationTimeEstimate, DateTime.Now, "TdPacketReceived", TdSenseEvent.Header.GlobalSequence.ToString());
            //this if for the ui. If packet has been received and green status isn't on, then turn it on.
            if (!flagForStreamingPacketsRight)
            {
                flagForStreamingPacketsRight = true;
                TurnStreamingGreenRight();
                try
                {
                    stopWatchRight.Start();
                    dispatcherTimerRight.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
            //reset timer to show that packet has been sent.
            ResetTimerRight();
        }

        private void theSummit_DataReceived_FFT_Right(object sender, SensingEventFFT FftSenseEvent)
        {
            // Announce that packet was received by handler 
            //Console.WriteLine("FFT Packet Received, Global SeqNum:" + FftSenseEvent.Header.GlobalSequence.ToString());
            // Log some information about the received packet out to file
            //theSummit.LogCustomEvent(FftSenseEvent.GenerationTimeEstimate, DateTime.Now, "TdPacketReceived", FftSenseEvent.Header.GlobalSequence.ToString());
            ResetTimerRight();
            if (!flagForStreamingPacketsRight)
            {
                flagForStreamingPacketsRight = true;
                TurnStreamingGreenRight();
                try
                {
                    stopWatchRight.Start();
                    dispatcherTimerRight.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
        }

        private void theSummit_DataReceived_Power_Right(object sender, SensingEventPower PowerSenseEvent)
        {
            // Announce that packet was received by handler

            //Console.WriteLine("Power Packet Received, Global SeqNum:" + PowerSenseEvent.Header.GlobalSequence.ToString());
            // Log some information about the received packet out to file
            //theSummit.LogCustomEvent(PowerSenseEvent.GenerationTimeEstimate, DateTime.Now, "TdPacketReceived", PowerSenseEvent.Header.GlobalSequence.ToString());
            ResetTimerRight();
            if (!flagForStreamingPacketsRight)
            {
                flagForStreamingPacketsRight = true;
                TurnStreamingGreenRight();
                try
                {
                    stopWatchRight.Start();
                    dispatcherTimerRight.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
        }

        private void theSummit_DataReceived_Accel_Right(object sender, SensingEventAccel AccelSenseEvent)
        {
            // Announce that packet was received by handler
            //Console.WriteLine("AccelPacket Received, Global SeqNum:" + AccelSenseEvent.Header.GlobalSequence.ToString());
            // Log some information about the received packet out to file
            //theSummit.LogCustomEvent(AccelSenseEvent.GenerationTimeEstimate, DateTime.Now, "TdPacketReceived", AccelSenseEvent.Header.GlobalSequence.ToString());
            ResetTimerRight();
            if (!flagForStreamingPacketsRight)
            {
                flagForStreamingPacketsRight = true;
                TurnStreamingGreenRight();
                try
                {
                    stopWatchRight.Start();
                    dispatcherTimerRight.Start();
                }
                catch (Exception error)
                {
                    _log.Error(error);
                }
            }
        }
        #endregion

        #region Gets Log and Mirror info and write to file
        private void GetApplicationLogInfo(SummitSystem localSummit, string logPath)
        {
            List<LogEntry> entries = null;
            APIReturnInfo? result;
            try
            {
                result = localSummit?.FlashLogReadLogEntry(FlashLogTypes.Application, ushort.MaxValue, out entries, true, 0x04, 0x00);
            }
            catch (Exception e)
            {
                _log.Error(e);
                MessageBox.Show("Could not write adaptive log data to file. Please check filepath for directories and restart application to try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                WriteEventLog(localSummit, "Adaptive Event Log Data Unsuccessful", "Could not log Adaptive Log data success in event log. Please report error to clinician.");
                return;
            }
            if (WriteLogDataToFile(logPath, entries))
            {
                _log.Info("Write adaptive log data to file successful");
                WriteEventLog(localSummit, "Adaptive Event Log Data Success", "Could not log Adaptive Log data success in event log. Please report error to clinician.");
            }
            else
            {
                WriteEventLog(localSummit, "Adaptive Event Log Data Unsuccessful", "Could not log Adaptive Log data success in event log. Please report error to clinician.");
                _log.Warn("Write adaptive log data to file unsuccessful");
            }
        }

        private void GetApplicationMirrorData(SummitSystem localSummit, string logPath, AppModel localAppModel)
        {
            APIReturnInfo? result;
            //Get the mirror log for ld states
            ReadFlashMirror rfm = new ReadFlashMirror(FlashMirrorTypes.LdDetectorStateDiagnostic);
            FlashMirrorData mirrorData = null;
            try
            {
                result = localSummit?.FlashReadMirror(FlashMirrorTypes.LdDetectorStateDiagnostic, out mirrorData);
            }
            catch (Exception e)
            {
                _log.Error(e);
                MessageBox.Show("Could not write mirror log data to file. Please check filepath for directories and restart application to try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                WriteEventLog(localSummit, "Mirror Event Log Unsuccessful", "Could not log mirror log info success in event log. Please report error to clinician.");
                return;
            }
            if (WriteFlashDataToFile(logPath, mirrorData))
            {
                _log.Info("Write flash data to file successful");
                WriteEventLog(localSummit, "Mirror Event Log Success", "Could not log mirror log info success in event log. Please report error to clinician.");
            }
            else
            {
                WriteEventLog(localSummit, "Mirror Event Log Unsuccessful", "Could not log mirror log info success in event log. Please report error to clinician.");
                _log.Warn("Write flash data to file unsuccessful");
            }
        }

        private bool WriteLogDataToFile(string filepath, List<LogEntry> entries)
        {
            bool success = false;
            if (entries == null)
            {
                _log.Warn("Could not log entries to file because list was null");
                MessageBox.Show("Could not write adaptive log data to file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                return success;
            }
            //if directory doesn't exits, create it
            try
            {
                FileInfo fileInfo = new FileInfo(filepath);
                if (!fileInfo.Exists)
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
                if (!Directory.Exists(fileInfo.Directory.FullName))
                {
                    MessageBox.Show("Could not create directory for writing adaptive log files. Please be sure the application_config.json BasePathToJSONFiles has the same path as DataDirectory in the Registry Editor for the path Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Medtronic\\ORCA. Please fix and restart application to log the files", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return success;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not create directory for writing adaptive log files. Please be sure the application_config.json BasePathToJSONFiles has the same path as DataDirectory in the Registry Editor for the path Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Medtronic\\ORCA. Please fix and restart application to log the files", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Error(e);
                return success;
            }
            try
            {
                StreamWriter outputFile = new StreamWriter(Path.Combine(filepath, DateTime.Now.ToString("yyyy-dd-M_HH-mm-ss") + "LOG.txt"));
                foreach (LogEntry log in entries)
                {
                    outputFile.WriteLine(log.ToString());
                }
                success = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not write adaptive log data to file at the path: " + filepath + ". Please check filepath for directories and restart application to try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Error(e);
            }
            return success;
        }

        private bool WriteFlashDataToFile(string filepath, FlashMirrorData mirrorData)
        {
            bool success = false;
            if (mirrorData == null)
            {
                _log.Warn("Could not log mirror data to file because it was null");
                MessageBox.Show("Could not write flash data to file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                return success;
            }
            //if directory doesn't exits, create it
            try
            {
                FileInfo fileInfo = new FileInfo(filepath);
                if (!fileInfo.Exists)
                {
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
                    if (!Directory.Exists(fileInfo.Directory.FullName))
                    {
                        MessageBox.Show("Could not create directory for writing mirror log files. Please be sure the application_config.json BasePathToJSONFiles has the same path as DataDirectory in the Registry Editor for the path Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Medtronic\\ORCA. Please fix and restart application to log the files", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return success;
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not create directory for writing mirror log files. Please be sure the application_config.json BasePathToJSONFiles has the same path as DataDirectory in the Registry Editor for the path Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Medtronic\\ORCA. Please fix and restart application to log the files", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Error(e);
                return success;
            }
            try
            {
                StreamWriter outputFile = new StreamWriter(Path.Combine(filepath, DateTime.Now.ToString("yyyy-dd-M_HH-mm-ss") + "MIRROR.txt"));
                outputFile.WriteLine(mirrorData);
                success = true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not write mirror log data to file at the path: " + filepath + ". Please check filepath for directories and restart application to try again.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Error(e);
            }
            return success;
        }

        /// <summary>
        /// Gets the path to the medtronic json files directory base on projectID, deviceID, and patientID
        /// Sets this path to filepath variable for use for later.
        /// This method is used in the constructor when creating the class
        /// </summary>
        private string GetDirectoryPathForCurrentSession(SummitSystem localSummit, string projectID, string patientID, string deviceID)
        {
            string filepath = null;
            try
            {
                
                //This gets the directories in the summitData directory and sort it
                //This is because we want the most recent directory and directories in there are sorted by linux timestamp
                //once sorted, we can find the most recent one (last one) and return the name of that directory to add to the filepath
                if (appConfigModel.BasePathToJSONFiles != null)
                {
                    string[] folders = Directory.GetDirectories(appConfigModel.BasePathToJSONFiles + "\\SummitData\\" + projectID + "\\" + patientID + "\\");
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
                MessageBox.Show("Could not find current session directory path. Please be sure the application_config.json BasePathToJSONFiles has the same path as DataDirectory in the Registry Editor for the path Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Medtronic\\ORCA. Please fix and restart application to try again", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                WriteEventLog(localSummit, "Could not find current session directory path. Please be sure the application_config.json BasePathToJSONFiles has the same path as DataDirectory in the Registry Editor for the path Computer\\HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Medtronic\\ORCA. Please fix and restart application to try again", "Could not log previous error message");
                _log.Error(e);
            }
            return filepath;
        }
        #endregion

        #region Open web page, validate uri
        /// <summary>
        /// Checks that the uri is correct for a web page
        /// </summary>
        /// <param name="uri">uri</param>
        /// <returns>true if valid or false if not</returns>
        public static bool IsValidUri(string uri)
        {
            if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                return false;
            Uri tmp;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out tmp))
                return false;
            return tmp.Scheme == Uri.UriSchemeHttp || tmp.Scheme == Uri.UriSchemeHttps;
        }
        /// <summary>
        /// Opens the web page
        /// </summary>
        /// <param name="uri">uri</param>
        /// <returns>true if success or false if not.</returns>
        public static bool OpenUri(string uri)
        {
            if (!IsValidUri(uri))
                return false;
            System.Diagnostics.Process.Start(uri);
            return true;
        }
        #endregion

        #region Helper Methods - Packet Loss check
        /// <summary>
        /// Checks the packet loss with Sense setting to see if the packet loss is going to be a major problem. This can be if settings set too high.
        /// </summary>
        /// <param name="localSenseModel">The local sense model.</param>
        /// <returns>True if there is no packet loss or false if there was an error calculating due to config file error or over the packet loss amount</returns>
        private bool CheckPacketLoss(SenseModel localSenseModel)
        {
            //Calculate number of time domain channels enabled
            int numberOfTDChannels = 0;
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    if (localSenseModel.Sense.TimeDomains[i].IsEnabled)
                    {
                        numberOfTDChannels++;
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    return false;
                }

            }
            //Calculate timedomain if stream is set to true.  Otherwise leave it at 0
            double TD = 0;
            try
            {
                if (localSenseModel.StreamEnables.TimeDomain)
                {
                    TD = (1000 / localSenseModel.Sense.Misc.StreamingRate * 14 + (numberOfTDChannels * 2 * localSenseModel.Sense.TDSampleRate));
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }

            //Calculate FFT if stream is set to true. Otherwise leave at 0
            double FFT = 0;
            try
            {
                if (localSenseModel.StreamEnables.FFT)
                {
                    FFT = ((14 + localSenseModel.Sense.FFT.FftSize) * 1000 / localSenseModel.Sense.FFT.FftInterval);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }

            //Calculate Power if stream is set to true. Otherwise leave at 0
            double Power = 0;
            try
            {
                if (localSenseModel.StreamEnables.Power)
                {
                    Power = (46 * (1000 / localSenseModel.Sense.FFT.FftInterval));
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }

            //Calculate Detection if stream is set to true. Otherwise leave at 0
            double Detection = 0;
            try
            {
                if (localSenseModel.StreamEnables.AdaptiveTherapy)
                {
                    Detection = (89 * (1000 / localSenseModel.Sense.FFT.FftInterval));
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }

            //Calculate Acceleromotry if stream is set to true. Otherwise leave at 0
            double ACC = 0;
            try
            {
                if (localSenseModel.StreamEnables.Accelerometry)
                {
                    ACC = (78 * localSenseModel.Sense.Accelerometer.SampleRate / 8);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }

            //Calculate Time if stream is set to true. Otherwise leave at 0
            double TimeStamp = 0;
            try
            {
                if (localSenseModel.StreamEnables.TimeStamp)
                {
                    TimeStamp = 14;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }

            double total = TD + FFT + Power + Detection + ACC + TimeStamp;

            //If mode 3 - max is 4500; if mode 4 - max is 6000
            try
            {
                if (localSenseModel.Mode != 3 && localSenseModel.Mode != 4)
                {
                    _log.Warn("Checking packet loss method.  Variable in config file: Mode - not set to 3 or 4. Variable set to: " + localSenseModel.Mode);
                    return false;
                }
                if (localSenseModel.Mode == 3 && total >= 4500)
                {
                    return false;
                }
                else if (localSenseModel.Mode == 4 && total >= 6000)
                {
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
        #endregion

        #region Error Handling
        private bool WriteEventLog(SummitSystem localSummit, string successLogMessage, string unsuccessfulMessageBoxMessage)
        {
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
            catch(Exception e)
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
        /// <param name="errorLocation">The location where the error is being check. Can be turning stim on, changing group, etc</param>
        /// <returns>True if there was an error or false if no error</returns>
        private bool CheckForReturnError(APIReturnInfo info, string errorLocation)
        {
            if (info.RejectCode != 0)
            {
                string messageBoxText = "Error Location: " + errorLocation + ". Reject Description: " + info.Descriptor + ". Reject Code: " + info.RejectCode;
                string caption = "ERROR";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Error;
                MessageBox.Show(messageBoxText, caption, button, icon);
                return false;
            }
            else
            {
                return true;
            }
        }

        private void ShowMessageBox(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    MessageBox.Show(Application.Current.MainWindow, message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception e)
                {
                    _log.Warn("MessageBox.Show crashed while trying to let user know about detection being on when turning sense on");
                    _log.Error(e);
                }
            });
        }
        #endregion
    }
}
