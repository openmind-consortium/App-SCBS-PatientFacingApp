using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    /// <summary>
    /// Model for the switch config files for left and right.  Contains config files to load for switch.
    /// </summary>
    public class MasterSwitchModel
    {
        /// <summary>
        /// Comment in the config file
        /// </summary>
        public string Comment { get; set; }
        /// <summary>
        /// Date and time of last switch run in UTC time
        /// </summary>
        public string DateTimeLastSwitch { get; set; }
        /// <summary>
        /// Number of minutes until user can run the switch again
        /// </summary>
        public int WaitTimeInMinutes { get; set; }
        /// <summary>
        /// Enable the min number of minutes required from last switch to be able to run again. Disable allows them to run it anytime.
        /// </summary>
        public bool WaitTimeIsEnabled { get; set; }
        /// <summary>
        /// Current index of config file to load
        /// </summary>
        public int CurrentIndex { get; set; }
        /// <summary>
        /// List of names of the adaptive config files for the Switch functionality. Used for both left_default and right
        /// </summary>
        public List<string> ConfigNames { get; set; }
    }
}
