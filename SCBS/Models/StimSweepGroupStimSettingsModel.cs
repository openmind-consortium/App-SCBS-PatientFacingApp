using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    class StimSweepGroupStimSettingsModel
    {
        /// <summary>
        /// Stim rate in Hz
        /// </summary>
        public double StimRate { get; set; }
        /// <summary>
        /// Stim amp in mA Program 0
        /// </summary>
        public double StimAmpProgram0 { get; set; }
        /// <summary>
        /// Stim amp in mA Program 1
        /// </summary>
        public double StimAmpProgram1 { get; set; }
        /// <summary>
        /// Stim amp in mA Program 2
        /// </summary>
        public double StimAmpProgram2 { get; set; }
        /// <summary>
        /// Stim amp in mA Program 3
        /// </summary>
        public double StimAmpProgram3 { get; set; }
        /// <summary>
        /// Pulse Width Program 0
        /// </summary>
        public int PulseWidthProgram0 { get; set; }
        /// <summary>
        /// Pulse Width Program 1
        /// </summary>
        public int PulseWidthProgram1 { get; set; }
        /// <summary>
        /// Pulse Width Program 2
        /// </summary>
        public int PulseWidthProgram2 { get; set; }
        /// <summary>
        /// Pulse Width Program 3
        /// </summary>
        public int PulseWidthProgram3 { get; set; }

    }
}
