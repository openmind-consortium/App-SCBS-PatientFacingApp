﻿using Medtronic.NeuroStim.Olympus.DataTypes.Therapy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    /// <summary>
    /// Model for Stim Parameters Object
    /// Easier to pass around object than to pass multiple values
    /// </summary>
    class StimParameterModel
    {
        /// <summary>
        /// Empty constructor
        /// </summary>
        public StimParameterModel()
        {

        }
        /// <summary>
        /// Constructor to set values for stimulation data for pulse width, stim rate and stim amp
        /// </summary>
        /// <param name="pulsewidth">Sets the pulseWidth value</param>
        /// <param name="stimrate">Sets the stimRate value</param>
        /// <param name="stimamp">Sets the stimAmp value</param>
        /// <param name="electrodesStim">Sets the electrode that is stimming</param>
        /// <param name="therapyElectrodes">Therapy electrodes</param>
        public StimParameterModel(string pulsewidth, string stimrate, string stimamp, string electrodesStim, TherapyElectrodes therapyElectrodes)
        {
            PulseWidth = pulsewidth;
            StimRate = stimrate;
            StimAmp = stimamp;
            StimElectrodes = electrodesStim;
            TherapyElectrodes = therapyElectrodes;
        }
        /// <summary>
        /// Constructor to set values for stimulation data for pulse width, stim rate and stim amp
        /// </summary>
        /// <param name="pulsewidth">Sets the pulseWidth value</param>
        /// <param name="stimrate">Sets the stimRate value</param>
        /// <param name="stimamp">Sets the stimAmp value</param>
        /// <param name="electrodesStim">Sets the electrode that is stimming</param>
        /// <param name="therapyElectrodes">Therapy electrodes</param>
        /// <param name="therapyGroup">Therapy group with all settings</param>
        public StimParameterModel(string pulsewidth, string stimrate, string stimamp, string electrodesStim, TherapyElectrodes therapyElectrodes, TherapyGroup therapyGroup)
        {
            PulseWidth = pulsewidth;
            StimRate = stimrate;
            StimAmp = stimamp;
            StimElectrodes = electrodesStim;
            TherapyElectrodes = therapyElectrodes;
            TherapyGroup = therapyGroup;
        }
        /// <summary>
        /// Constructor to set values for stimulation data for pulse width, stim rate and stim amp
        /// </summary>
        /// <param name="pulsewidth">Sets the pulseWidth value</param>
        /// <param name="stimrate">Sets the stimRate value</param>
        /// <param name="stimamp">Sets the stimAmp value</param>
        /// <param name="electrodesStim">Sets the electrode that is stimming</param>
        /// <param name="therapyElectrodes">Therapy electrodes</param>
        /// <param name="therapyGroup">Therapy group with all settings</param>
        public StimParameterModel(string pulsewidth, string stimrate, string stimamp, string electrodesStim, TherapyElectrodes therapyElectrodes, TherapyGroup therapyGroup, AmplitudeLimits ampLimits)
        {
            PulseWidth = pulsewidth;
            StimRate = stimrate;
            StimAmp = stimamp;
            StimElectrodes = electrodesStim;
            TherapyElectrodes = therapyElectrodes;
            TherapyGroup = therapyGroup;
            AmpLimits = ampLimits;
        }
        /// <summary>
        /// Constructor to set values for stimulation data for pulse width, stim rate and stim amp
        /// </summary>
        /// <param name="pulsewidth">Sets the pulseWidth value</param>
        /// <param name="stimrate">Sets the stimRate value</param>
        /// <param name="stimamp">Sets the stimAmp value</param>
        /// <param name="electrodesStim">Sets the electrode that is stimming</param>
        /// <param name="therapyElectrodes">Therapy electrodes</param>
        /// <param name="therapyGroup">Therapy group with all settings</param>
        /// <param name="ampLimits">Amp Limits</param>
        /// <param name="rateValue">Rate value as double</param>
        /// <param name="ampValue">Amp value as double</param>
        /// <param name="pwValue">Pulse width value as double</param>
        public StimParameterModel(string pulsewidth, string stimrate, string stimamp, string electrodesStim, TherapyElectrodes therapyElectrodes, TherapyGroup therapyGroup, AmplitudeLimits ampLimits, double rateValue, double ampValue, int pwValue)
        {
            PulseWidth = pulsewidth;
            StimRate = stimrate;
            StimAmp = stimamp;
            StimElectrodes = electrodesStim;
            TherapyElectrodes = therapyElectrodes;
            TherapyGroup = therapyGroup;
            AmpLimits = ampLimits;
            StimAmpValue = ampValue;
            StimRateValue = rateValue;
            PulseWidthValue = pwValue;
        }
        /// <summary>
        /// Pulse Width
        /// </summary>
        public string PulseWidth { get; set; }
        /// <summary>
        /// Stim rate in Hz
        /// </summary>
        public string StimRate { get; set; }
        /// <summary>
        /// Stim amp in mA
        /// </summary>
        public string StimAmp { get; set; }
        /// <summary>
        /// Pulse Width
        /// </summary>
        public int PulseWidthValue { get; set; }
        /// <summary>
        /// Stim rate in Hz
        /// </summary>
        public double StimRateValue { get; set; }
        /// <summary>
        /// Stim amp in mA
        /// </summary>
        public double StimAmpValue { get; set; }
        /// <summary>
        /// Stim Electrodes
        /// </summary>
        public string StimElectrodes { get; set; }
        /// <summary>
        /// Stim Electrodes in an array
        /// </summary>
        public TherapyElectrodes TherapyElectrodes { get; set; }
        /// <summary>
        /// Therapy group with all stim settings for group
        /// </summary>
        public TherapyGroup TherapyGroup { get; set; }
        /// <summary>
        /// Amplitude Limits
        /// </summary>
        public AmplitudeLimits AmpLimits { get; set; }
    }
}
