using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    /// <summary>
    /// Model for Stim Sweep
    /// </summary>
    public class StimSweepModel
    {
        /// <summary>
        /// Comment giving directions for the config file
        /// </summary>
        public string Comment { get; set; }
        /// <summary>
        /// LeftINSOrUnilateral object
        /// </summary>
        public LeftINSOrUnilateral LeftINSOrUnilateral { get; set; }
        /// <summary>
        /// RightINS object
        /// </summary>
        public RightINS RightINS { get; set; }
        /// <summary>
        /// Time to run each stim sweep in milliseconds
        /// </summary>
        public List<uint> TimeToRunInMilliSeconds { get; set; }
        /// <summary>
        /// Amount of time in milliseconds to put the event start and event stop event marker
        /// </summary>
        public int EventMarkerDelayTimeInMilliSeconds { get; set; }
        /// <summary>
        /// Current index to start the stim sweep at
        /// </summary>
        public int CurrentIndex { get; set; }
    }

    /// <summary>
    /// LeftINSOrUnilateral Object
    /// </summary>
    public class LeftINSOrUnilateral
    {
        /// <summary>
        /// Sets the group to switch to run stim sweep
        /// </summary>
        public string GroupToRunStimSweep { get; set; }
        /// <summary>
        /// Rate
        /// </summary>
        public List<double> RateInHz { get; set; }
        /// <summary>
        /// Program 0-3
        /// </summary>
        public List<int> Program { get; set; }
        /// <summary>
        /// Amp
        /// </summary>
        public List<double> AmpInmA { get; set; }
        /// <summary>
        /// Pulse width
        /// </summary>
        public List<int> PulseWidthInMicroSeconds { get; set; }
    }
    /// <summary>
    /// RightINS Object
    /// </summary>
    public class RightINS
    {
        /// <summary>
        /// Sets the group to switch to run stim sweep
        /// </summary>
        public string GroupToRunStimSweep { get; set; }
        /// <summary>
        /// Rate
        /// </summary>
        public List<double> RateInHz { get; set; }
        /// <summary>
        /// Program 0-3
        /// </summary>
        public List<int> Program { get; set; }
        /// <summary>
        /// Amp
        /// </summary>
        public List<double> AmpInmA { get; set; }
        /// <summary>
        /// Pulse width
        /// </summary>
        public List<int> PulseWidthInMicroSeconds { get; set; }
    }
}
