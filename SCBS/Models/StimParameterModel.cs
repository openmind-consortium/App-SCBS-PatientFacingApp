using Medtronic.NeuroStim.Olympus.DataTypes.Therapy;
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
        /// Constructor to set values for stimulation data for pulse width, stim rate and stim amp
        /// </summary>
        /// <param name="pulsewidth">Sets the pulseWidth value</param>
        /// <param name="stimrate">Sets the stimRate value</param>
        /// <param name="stimamp">Sets the stimAmp value</param>
        /// <param name="electrodesStim">Sets the electrode that is stimming</param>
        public StimParameterModel(string pulsewidth, string stimrate, string stimamp, string electrodesStim, TherapyElectrodes therapyElectrodes)
        {
            PulseWidth = pulsewidth;
            StimRate = stimrate;
            StimAmp = stimamp;
            StimElectrodes = electrodesStim;
            TherapyElectrodes = therapyElectrodes;
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
        /// Stim Electrodes
        /// </summary>
        public string StimElectrodes { get; set; }
        /// <summary>
        /// Stim Electrodes in an array
        /// </summary>
        public TherapyElectrodes TherapyElectrodes { get; set; }
    }
}
