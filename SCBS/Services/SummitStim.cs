using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.Commands;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using Medtronic.NeuroStim.Olympus.DataTypes.PowerManagement;
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
    class SummitStim
    {
        private ILog _log;
        private SummitSensing summitSensing;
        public SummitStim(ILog _log)
        {
            this._log = _log;
            summitSensing = new SummitSensing(_log);
        }
        /// <summary>
        /// Changes the active group to the group specified.
        /// </summary>
        /// <param name="localSummit">Summit System</param>
        /// <param name="groupToChangeTo">Medtronic Active Group Format to change to</param>
        /// <param name="localSenseModel">Sense Model that uses the streaming parameters for turning stream on and off</param>
        /// <returns>Tuple with bool true if success or false if not. string give error message</returns>
        public async Task<Tuple<bool, string>> ChangeActiveGroup(SummitSystem localSummit, ActiveGroup groupToChangeTo, SenseModel localSenseModel)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, "Error: Summit null or disposed.");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                int counter = 5;
                summitSensing.StopStreaming(localSummit, true);
                do
                {
                    bufferReturnInfo = await Task.Run(() => localSummit.StimChangeActiveGroup(groupToChangeTo));
                    if (counter < 5)
                    {
                        Thread.Sleep(400);
                    }
                    counter--;
                } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                //Start streaming
                summitSensing.StartStreaming(localSummit, localSenseModel, true);
                if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                {
                    _log.Warn(":: Error: Medtronic API return error changing active group: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);

                    return Tuple.Create(false, "Error: Medtronic API return error changing active group: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, "Error: Could not change groups");
            }
            return Tuple.Create(true, "Successfully changed groups");
        }

        #region Stim Therapy On/Off
        /// <summary>
        /// Turns stim therapy on. Resets POR if needed
        /// </summary>
        /// <param name="localSummit">Summit System</param>
        /// <returns>Tuple with bool true if success or false if not. string give error message</returns>
        public async Task<Tuple<bool, string>> ChangeStimTherapyON(SummitSystem localSummit)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, "Error: Summit null or disposed.");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                bufferReturnInfo = await Task.Run(() => localSummit.StimChangeTherapyOn());
                if (bufferReturnInfo.RejectCodeType == typeof(MasterRejectCode)
                    && (MasterRejectCode)bufferReturnInfo.RejectCode == MasterRejectCode.ChangeTherapyPor)
                {
                    ResetPOR(localSummit);
                    bufferReturnInfo = await Task.Run(() => localSummit.StimChangeTherapyOn());
                    _log.Info("Turn stim therapy on after resetPOR success");
                }

                if (bufferReturnInfo.RejectCode != 0)
                {
                    _log.Warn(":: Error: Medtronic API return error turning stim therapy on: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                    return Tuple.Create(false, "Error: Medtronic API return error turning stim therapy on: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, "Error: Could not turn stim therapy on");
            }
            return Tuple.Create(true, "Successfully turned stim therapy on");
        }

        /// <summary>
        /// Turns stim therapy off.
        /// </summary>
        /// <param name="localSummit">Summit System</param>
        /// <param name="withRamp">withRamp	Inidicates if soft start parameters should be used to ramp down stim if true, or if should just jump to off if false</param>
        /// <returns>Tuple with bool true if success or false if not. string give error message</returns>
        public async Task<Tuple<bool, string>> ChangeStimTherapyOFF(SummitSystem localSummit, bool withRamp = false)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, "Error: Summit null or disposed.");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                bufferReturnInfo = await Task.Run(() => localSummit.StimChangeTherapyOff(withRamp));
                if (bufferReturnInfo.RejectCode != 0)
                {
                    _log.Warn(":: Error: Medtronic API return error turning stim therapy off: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                    return Tuple.Create(false, "Error: Medtronic API return error turning stim therapy off: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, "Error: Could not turn stim therapy off");
            }
            return Tuple.Create(true, "Successfully turned stim therapy off");
        }

        #endregion

        #region Change Stim Amp
        /// <summary>
        /// Increment or decrement the stim amp by step value
        /// </summary>
        /// <param name="localSummit">Summit system</param>
        /// <param name="programNumber">Program 0-3</param>
        /// <param name="ampStepValue">Value to adjust. Plus for increment or minus for decrement</param>
        /// <returns>bool for success/failure and double for new stim amp. string give error message</returns>
        public async Task<Tuple<bool, double?, string>> ChangeStimAmpStep(SummitSystem localSummit, byte programNumber, double ampStepValue)
        {
            double? updatedStimAmp = 0;
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, updatedStimAmp, "Summit Disposed");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                int counter = 5;
                do
                {
                    bufferReturnInfo = await Task.Run(() => localSummit.StimChangeStepAmp(programNumber, ampStepValue, out updatedStimAmp));
                    if (counter < 5)
                    {
                        Thread.Sleep(400);
                    }
                    counter--;
                } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                {
                    _log.Warn(":: Error: Medtronic API return error changing stim step amp: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                    return Tuple.Create(false, updatedStimAmp, "Error: Medtronic API return error changing stim step amp: " + bufferReturnInfo.Descriptor + ".Reject Code: " + bufferReturnInfo.RejectCode);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, updatedStimAmp, "Error changing stim step amp");
            }
            return Tuple.Create(true, updatedStimAmp, "Success");
        }

        /// <summary>
        /// Change stim amp to specific value.
        /// </summary>
        /// <param name="localSummit">Summit system</param>
        /// <param name="programNumber">Program 0-3</param>
        /// <param name="ampValueToChangeTo">Value to set stim amp to.</param>
        /// <param name="currentAmpValue">Value of the current stim amp.</param>
        /// <returns>bool for success/failure and double for new stim amp</returns>
        public async Task<Tuple<bool, double?, string>> ChangeStimAmpToValue(SummitSystem localSummit, byte programNumber, double ampValueToChangeTo, double currentAmpValue)
        {
            double? updatedStimAmp = 0;
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, updatedStimAmp, "Summit Disposed");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                double ampToChangeTo = 0;
                ampToChangeTo = Math.Round(ampValueToChangeTo + (-1 * currentAmpValue), 1);
                if (ampToChangeTo != 0)
                {
                    int counter = 5;
                    do
                    {
                        bufferReturnInfo = await Task.Run(() => localSummit.StimChangeStepAmp(programNumber, ampToChangeTo, out updatedStimAmp));
                        if (counter < 5)
                        {
                            Thread.Sleep(400);
                        }
                        counter--;
                    } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                    if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                    {
                        _log.Warn(":: Error: Medtronic API return error changing stim amp to value: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        return Tuple.Create(false, updatedStimAmp, "Error: Medtronic API return error changing stim amp to value: " + bufferReturnInfo.Descriptor + ".Reject Code: " + bufferReturnInfo.RejectCode);
                    }
                }
                else
                {
                    return Tuple.Create(false, updatedStimAmp, "Could not change stim amp to value");
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, updatedStimAmp, "Error changing stim step amp to value");
            }
            return Tuple.Create(true, updatedStimAmp, "Success");
        }
        #endregion

        #region Change Stim Rate
        /// <summary>
        /// Increment or decrement the stim rate by step value
        /// </summary>
        /// <param name="localSummit">Summit system</param>
        /// <param name="senseFriendly">True for sense friendly values or false for not</param>
        /// <param name="rateStepValue">Value to adjust. Plus for increment or minus for decrement</param>
        /// <returns>bool for success/failure and double for new stim rate. string give error message</returns>
        public async Task<Tuple<bool, double?, string>> ChangeStimRateStep(SummitSystem localSummit, bool senseFriendly, double rateStepValue)
        {
            double? updatedStimRate = 0;
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, updatedStimRate, "Summit Disposed");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                int counter = 5;
                do
                {
                    bufferReturnInfo = await Task.Run(() => localSummit.StimChangeStepFrequency(rateStepValue, senseFriendly, out updatedStimRate));
                    if (counter < 5)
                    {
                        Thread.Sleep(400);
                    }
                    counter--;
                } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                {
                    _log.Warn(":: Error: Medtronic API return error changing stim step rate: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                    return Tuple.Create(false, updatedStimRate, "Error: Medtronic API return error changing stim step rate: " + bufferReturnInfo.Descriptor + ".Reject Code: " + bufferReturnInfo.RejectCode);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, updatedStimRate, "Error changing stim step rate");
            }
            return Tuple.Create(true, updatedStimRate, "Success");
        }

        /// <summary>
        /// Change stim rate to specific value.
        /// </summary>
        /// <param name="localSummit">Summit system</param>
        /// <param name="senseFriendly">True for sense friendly values or false for not</param>
        /// <param name="rateValueToChangeTo">Value to set stim rate to.</param>
        /// <param name="currentRateValue">Value of the current stim rate.</param>
        /// <returns>bool for success/failure and double for new stim rate. string give error message</returns>
        public async Task<Tuple<bool, double?, string>> ChangeStimRateToValue(SummitSystem localSummit, bool senseFriendly, double rateValueToChangeTo, double currentRateValue)
        {
            double? updatedStimRate = 0;
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, updatedStimRate, "Summit Disposed");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                double rateToChangeTo = 0;
                rateToChangeTo = Math.Round(rateValueToChangeTo + (-1 * currentRateValue), 1);
                if (rateToChangeTo != 0)
                {
                    int counter = 5;
                    do
                    {
                        bufferReturnInfo = await Task.Run(() => localSummit.StimChangeStepFrequency(rateToChangeTo, senseFriendly, out updatedStimRate));
                        if (counter < 5)
                        {
                            Thread.Sleep(400);
                        }
                        counter--;
                    } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                    if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                    {
                        _log.Warn(":: Error: Medtronic API return error changing stim rate to value: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        return Tuple.Create(false, updatedStimRate, "Error: Medtronic API return error changing stim rate to value: " + bufferReturnInfo.Descriptor + ".Reject Code: " + bufferReturnInfo.RejectCode);
                    }
                }
                else
                {
                    return Tuple.Create(false, updatedStimRate, "Could not change stim rate to value");
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, updatedStimRate, "Error changing stim rate to value");
            }
            return Tuple.Create(true, updatedStimRate, "Success");
        }

        #endregion

        #region Change Stim Pulse Width
        /// <summary>
        /// Increment or decrement the stim pulse width by step value
        /// </summary>
        /// <param name="localSummit">Summit system</param>
        /// <param name="programNumber">Program 0-3</param>
        /// <param name="pwStepValue">Value to adjust. Plus for increment or minus for decrement</param>
        /// <returns>bool for success/failure and int for new stim pulse width. string give error message</returns>
        public async Task<Tuple<bool, int?, string>> ChangeStimPulseWidthStep(SummitSystem localSummit, byte programNumber, int pwStepValue)
        {
            int? updatedStimPW = 0;
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, updatedStimPW, "Summit Disposed");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                int counter = 5;
                do
                {
                    bufferReturnInfo = await Task.Run(() => localSummit.StimChangeStepPW(programNumber, pwStepValue, out updatedStimPW));
                    if (counter < 5)
                    {
                        Thread.Sleep(400);
                    }
                    counter--;
                } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                {
                    _log.Warn(":: Error: Medtronic API return error changing stim step pulse width: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                    return Tuple.Create(false, updatedStimPW, "Error: Medtronic API return error changing stim step pulse width: " + bufferReturnInfo.Descriptor + ".Reject Code: " + bufferReturnInfo.RejectCode);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, updatedStimPW, "Error changing stim step pulse width");
            }
            return Tuple.Create(true, updatedStimPW, "Success");
        }

        /// <summary>
        /// Change stim pulse width to specific value.
        /// </summary>
        /// <param name="localSummit">Summit system</param>
        /// <param name="programNumber">Program 0-3</param>
        /// <param name="pwValueToChangeTo">Value to set stim pulse width to.</param>
        /// <param name="currentPWValue">Value of the current stim pulse width.</param>
        /// <returns>bool for success/failure and int for new stim pulse width. string give error message</returns>
        public async Task<Tuple<bool, int?, string>> ChangeStimPulseWidthToValue(SummitSystem localSummit, byte programNumber, int pwValueToChangeTo, int currentPWValue)
        {
            int? updatedStimPW = 0;
            if (localSummit == null || localSummit.IsDisposed)
            {
                return Tuple.Create(false, updatedStimPW, "Summit Disposed");
            }
            APIReturnInfo bufferReturnInfo;
            try
            {
                int pwToChangeTo = 0;
                pwToChangeTo = pwValueToChangeTo + (-1 * currentPWValue);
                if (pwToChangeTo != 0)
                {
                    int counter = 5;
                    do
                    {
                        bufferReturnInfo = await Task.Run(() => localSummit.StimChangeStepPW(programNumber, pwToChangeTo, out updatedStimPW));
                        if (counter < 5)
                        {
                            Thread.Sleep(400);
                        }
                        counter--;
                    } while ((bufferReturnInfo.RejectCode != 0) && counter > 0);
                    if ((bufferReturnInfo.RejectCode != 0) && counter == 0)
                    {
                        _log.Warn(":: Error: Medtronic API return error changing stim pulse width to value: " + bufferReturnInfo.Descriptor + ". Reject Code: " + bufferReturnInfo.RejectCode);
                        return Tuple.Create(false, updatedStimPW, "Error: Medtronic API return error changing stim pulse width to value: " + bufferReturnInfo.Descriptor + ".Reject Code: " + bufferReturnInfo.RejectCode);
                    }
                }
                else
                {
                    return Tuple.Create(false, updatedStimPW, "Could not change stim pulse width to value");
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                return Tuple.Create(false, updatedStimPW, "Error changing stim pulse width to value");
            }
            return Tuple.Create(true, updatedStimPW, "Success");
        }

        #endregion

        /// <summary>
        /// Resets the POR bit if it was set
        /// </summary>
        /// <param name="localSummit">SummitSystem for the api call</param>
        private void ResetPOR(SummitSystem localSummit)
        {
            if (localSummit == null || localSummit.IsDisposed)
            {
                _log.Warn("Summit Disposed");
                return;
            }
            _log.Info("POR was set, resetting...");
            APIReturnInfo bufferReturnInfo;
            try
            {
                // reset POR
                bufferReturnInfo = localSummit.ResetErrorFlags(Medtronic.NeuroStim.Olympus.DataTypes.Core.StatusBits.Por);
                if (bufferReturnInfo.RejectCode != 0)
                {
                    return;
                }

                // check battery
                BatteryStatusResult theStatus;
                localSummit.ReadBatteryLevel(out theStatus);
                if (bufferReturnInfo.RejectCode != 0)
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
                _log.Error(e);
            }
        }
    }
}
