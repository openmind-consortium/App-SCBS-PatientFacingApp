using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    /// <summary>
    /// Model for the config file for the application. Sets whether bilateral or the switch is enabled.
    /// </summary>
    public class AppModel
    {
        /// <summary>
        /// Sets the base path to the Medtronic JSON files. 
        /// </summary>
        public string BasePathToJSONFiles { get; set; }
        /// <summary>
        /// Set to true if bilateral or false if unilateral
        /// </summary>
        public bool Bilateral { get; set; }
        /// <summary>
        /// Set to true if Switch functionality is present or false if hidden
        /// </summary>
        public bool Switch { get; set; }
        /// <summary>
        /// Set to true if Align functionality is present or false if hidden
        /// </summary>
        public bool Align { get; set; }
        /// <summary>
        /// Set to true if Montage functionality is present or false if hidden
        /// </summary>
        public bool Montage { get; set; }
        /// <summary>
        /// Set to true if stim sweep functionality is present or false if hidden
        /// </summary>
        public bool StimSweep { get; set; }
        /// <summary>
        /// Set to true if New Session button is present or false if hidden
        /// </summary>
        /// 
        public bool NewSession { get; set; }
        /// <summary>
        /// Set to true if you want the report button to be hidden or false if shown
        /// </summary>
        public bool HideReportButton { get; set; }
        /// <summary>
        /// Set to true if you want the log and mirror info for adaptive states to be added to current session directory
        /// </summary>
        public bool GetAdaptiveLogInfo { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether [get adaptive mirror information].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [get adaptive mirror information]; otherwise, <c>false</c>.
        /// </value>
        public bool GetAdaptiveMirrorInfo { get; set; }
        /// <summary>
        /// Sets whether medtronic verbose logging is on if true or off if false
        /// </summary>
        public bool VerboseLogOnForMedtronic { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether [log beep noise].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [get log beep noise]; otherwise, <c>false</c>.
        /// </value>
        public bool LogBeepEvent { get; set; }
        /// <summary>
        /// Class that allows user to change the beep noise
        /// </summary>
        public CTMBeepEnables CTMBeepEnables { get; set; }
        /// <summary>
        /// Adds the option to have 2 buttons that open web pages based on the urls
        /// </summary>
        public WebPageButtons WebPageButtons { get; set; }
        /// <summary>
        /// Adds the option to move to a group based on config file
        /// </summary>
        public MoveGroupButton MoveGroupButton { get; set; }
    }

    /// <summary>
    /// Class that allows user to change the beep noise
    /// </summary>
    public class CTMBeepEnables
    {
        /// <summary>
        /// Comment 
        /// </summary>
        public string comment { get; set; }
        /// <summary>
        /// No beep
        /// </summary>
        public bool None { get; set; }
        /// <summary>
        /// General alert beep
        /// </summary>
        public bool GeneralAlert { get; set; }
        /// <summary>
        /// Tememetry Completed Beep
        /// </summary>
        public bool TelMCompleted { get; set; }
        /// <summary>
        /// Device discovered beep
        /// </summary>
        public bool DeviceDiscovered { get; set; }
        /// <summary>
        /// No device discovered beep
        /// </summary>
        public bool NoDeviceDiscovered { get; set; }
        /// <summary>
        /// Tel lost beep
        /// </summary>
        public bool TelMLost { get; set; }
    }

    /// <summary>
    /// Adds the option to have 2 buttons that open web pages based on the urls
    /// </summary>
    public class WebPageButtons
    {
        /// <summary>
        /// Comment 
        /// </summary>
        public string comment { get; set; }
        /// <summary>
        /// True if you want the to be able to open without being connected to device or false if need connection
        /// </summary>
        public bool OpenWithoutBeingConnected { get; set; }
        /// <summary>
        /// True if you want the web page button to be displayed or false if not
        /// </summary>
        public bool WebPageOneButtonEnabled { get; set; }
        /// <summary>
        /// URL for the web page for WebPageOneButtonEnabled
        /// </summary>
        public string WebPageOneURL { get; set; }
        /// <summary>
        /// Text displayed to user for Button One
        /// </summary>
        public string WebPageOneButtonText { get; set; }
        /// <summary>
        /// True if you want the web page button to be displayed or false if not
        /// </summary>
        public bool WebPageTwoButtonEnabled { get; set; }
        /// <summary>
        /// URL for the web page for WebPageTwoButtonEnabled
        /// </summary>
        public string WebPageTwoURL { get; set; }
        /// <summary>
        /// Text displayed to user for Button Two
        /// </summary>
        public string WebPageTwoButtonText { get; set; }
    }
    /// <summary>
    /// Button to allow to move group and change text
    /// </summary>
    public class MoveGroupButton
    {
        /// <summary>
        /// Button text
        /// </summary>
        public string MoveGroupButtonText { get; set; }
        /// <summary>
        /// enable button to move group
        /// </summary>
        public bool MoveGroupButtonEnabled { get; set; }
        /// <summary>
        /// Group to move to on left or unilateral side
        /// </summary>
        public string GroupToMoveToLeftUnilateral { get; set; }
        /// <summary>
        /// Group to move to on right side
        /// </summary>
        public string GroupToMoveToRight { get; set; }
    }
}
