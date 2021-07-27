using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    /// <summary>
    /// Model for Patient Stim Control configs
    /// </summary>
    public class PatientStimControlModel
    {
        /// <summary>
        /// Comment giving directions for the config file
        /// </summary>
        public string comment { get; set; }
        /// <summary>
        /// Hides the Stim On button
        /// </summary>
        public bool HideStimOnButton { get; set; }
        /// <summary>
        /// Hides the Stim Off button
        /// </summary>
        public bool HideStimOffButton { get; set; }
        /// <summary>
        /// Card one
        /// </summary>
        public Card Card1 { get; set; }
        /// <summary>
        /// Card one
        /// </summary>
        public Card Card2 { get; set; }
        /// <summary>
        /// Card one
        /// </summary>
        public Card Card3 { get; set; }
        /// <summary>
        /// Card one
        /// </summary>
        public Card Card4 { get; set; }
    }

    /// <summary>
    /// Card Object
    /// </summary>
    public class Card
    {
        /// <summary>
        /// Comment
        /// </summary>
        public string comment { get; set; }
        /// <summary>
        /// Hides the Card from view
        /// </summary>
        public bool HideCard { get; set; }
        /// <summary>
        /// Group Letter A, B, C or D
        /// </summary>
        public string Group { get; set; }
        /// <summary>
        /// Custom text limited to 15 chars or less
        /// </summary>
        public string CustomText { get; set; }
        /// <summary>
        /// Display Settings
        /// </summary>
        public DisplaySettings DisplaySettings { get; set; }
        /// <summary>
        /// Stim Control Settings
        /// </summary>
        public StimControl StimControl { get; set; }
    }

    /// <summary>
    /// Display Settings Object
    /// </summary>
    public class DisplaySettings
    {
        /// <summary>
        /// Hides the group in the display
        /// </summary>
        public bool HideGroupDisplay { get; set; }
        /// <summary>
        /// Hides the site in the display
        /// </summary>
        public bool HideSiteDisplay { get; set; }
        /// <summary>
        /// Hides the amp in the display
        /// </summary>
        public bool HideAmpDisplay { get; set; }
        /// <summary>
        /// Hides the rate in the display
        /// </summary>
        public bool HideRateDisplay { get; set; }
        /// <summary>
        /// Hides the pulse width in the display
        /// </summary>
        public bool HidePulseWidthDisplay { get; set; }
    }

    /// <summary>
    /// Stim Control Object
    /// </summary>
    public class StimControl
    {
        /// <summary>
        /// Comment
        /// </summary>
        public string comment { get; set; }
        /// <summary>
        /// StimControlType set to 0-amp, 1-rate, 2-pulse width or 3-None. 
        /// </summary>
        public uint StimControlType { get; set; }
        /// <summary>
        /// Hides the current value being changed in the display
        /// </summary>
        public bool HideCurrentValue { get; set; }
        /// <summary>
        /// Hides the current value units being changed in the display
        /// </summary>
        public bool HideCurrentValueUnits { get; set; }
        /// <summary>
        /// Program set to 0-3 for which program to change when changing amp and pulsewidth 
        /// </summary>
        public int Program { get; set; }
        /// <summary>
        /// Amp Settings
        /// </summary>
        public Amp Amp { get; set; }
        /// <summary>
        /// Rate Settings
        /// </summary>
        public Rate Rate { get; set; }
        /// <summary>
        /// Pulse Width Settings
        /// </summary>
        public PulseWidth PulseWidth { get; set; }
    }

    /// <summary>
    /// Amp Settings Object
    /// </summary>
    public class Amp
    {
        /// <summary>
        /// Comment
        /// </summary>
        public string comment { get; set; }
        /// <summary>
        /// Amp to change to when current card is selected
        /// </summary>
        public double TargetAmp { get; set; }
        /// <summary>
        /// List of values that can be moved to
        /// </summary>
        public List<double> AmpValues { get; set; }
    }

    /// <summary>
    /// Rate Settings Object
    /// </summary>
    public class Rate
    {
        /// <summary>
        /// Comment
        /// </summary>
        public string comment { get; set; }
        /// <summary>
        /// Rate to change to when current card is selected
        /// </summary>
        public double TargetRate { get; set; }
        /// <summary>
        /// List of values that can be moved to
        /// </summary>
        public List<double> RateValues { get; set; }
        /// <summary>
        /// Allows you to select the sense friendly rate closest to the given rate
        /// </summary>
        public bool SenseFriendly { get; set; }
    }

    /// <summary>
    /// PulseWidth Settings Object
    /// </summary>
    public class PulseWidth
    {
        /// <summary>
        /// Comment
        /// </summary>
        public string comment { get; set; }
        /// <summary>
        /// PulseWidth to change to when current card is selected
        /// </summary>
        public int TargetPulseWidth { get; set; }
        /// <summary>
        /// List of values that can be moved to
        /// </summary>
        public List<int> PulseWidthValues { get; set; }
    }
}
