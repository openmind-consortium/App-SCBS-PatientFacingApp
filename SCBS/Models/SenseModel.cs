﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    /// <summary>
    /// Model for Sense Config JSON File
    /// This class is used to convert the json file into class data
    /// </summary>
    public class SenseModel
    {
        /// <summary>
        /// Sets the Mode
        /// </summary>
        public ushort Mode { get; set; }
        /// <summary>
        /// Sets the Ratio
        /// </summary>
        public byte Ratio { get; set; }
        /// <summary>
        /// SenseOptions Object
        /// </summary>
        public SenseOptions SenseOptions { get; set; }
        /// <summary>
        /// StreamEnables Object
        /// </summary>
        public StreamEnables StreamEnables { get; set; }
        /// <summary>
        /// Sense Object
        /// </summary>
        public Sense Sense { get; set; }
    }
    /// <summary>
    /// SenseOption Object
    /// </summary>
    public class SenseOptions
    {
        /// <summary>
        /// Turns on or off timedomain for sense
        /// </summary>
        public bool TimeDomain { get; set; }
        /// <summary>
        /// Turns on or off fft for sense
        /// </summary>
        public bool FFT { get; set; }
        /// <summary>
        /// Turns on or off power for sense
        /// </summary>
        public bool Power { get; set; }
        /// <summary>
        /// Turns on or off LD0 for sense
        /// </summary>
        public bool LD0 { get; set; }
        /// <summary>
        /// Turns on or off LD1 for sense
        /// </summary>
        public bool LD1 { get; set; }
        /// <summary>
        /// Turns on or off Adaptive state for sense
        /// </summary>
        public bool AdaptiveState { get; set; }
        /// <summary>
        /// Turns on or off Loop Recording for sense
        /// </summary>
        public bool LoopRecording { get; set; }
        /// <summary>
        /// Unused. Valid Medtronic API call that is unused
        /// </summary>
        public bool Unused { get; set; }
    }
    /// <summary>
    /// StreamEnables Object
    /// </summary>
    public class StreamEnables
    {
        /// <summary>
        /// Turns streaming on for TimeDomain
        /// </summary>
        public bool TimeDomain { get; set; }
        /// <summary>
        /// Turns streaming on for FFT
        /// </summary>
        public bool FFT { get; set; }
        /// <summary>
        /// Turns streaming on for Power
        /// </summary>
        public bool Power { get; set; }
        /// <summary>
        /// Turns streaming on for Accelerometry
        /// </summary>
        public bool Accelerometry { get; set; }
        /// <summary>
        /// Turns streaming on for Adaptive Therapy
        /// </summary>
        public bool AdaptiveTherapy { get; set; }
        /// <summary>
        /// Turns streaming on for Adaptive State
        /// </summary>
        public bool AdaptiveState { get; set; }
        /// <summary>
        /// Turns streaming on for Event Marker
        /// </summary>
        public bool EventMarker { get; set; }
        /// <summary>
        /// Turns streaming on for Time Stamp
        /// </summary>
        public bool TimeStamp { get; set; }
    }
    /// <summary>
    /// Sense Object
    /// </summary>
    public class Sense
    {
        /// <summary>
        /// Time domain sample rate in Hz
        /// </summary>
        public int TDSampleRate { get; set; }
        /// <summary>
        /// Time domain Channel List
        /// </summary>
        public List<TimeDomain> TimeDomains { get; set; }
        /// <summary>
        /// FFT Object
        /// </summary>
        public FFT FFT { get; set; }
        /// <summary>
        /// Power band channel list
        /// </summary>
        public List<Power> PowerBands { get; set; }
        /// <summary>
        /// Accelerometer Object
        /// </summary>
        public Accelerometer Accelerometer { get; set; }
        /// <summary>
        /// Misc Object
        /// </summary>
        public Misc Misc { get; set; }
    }
    /// <summary>
    /// Time Domain Object
    /// </summary>
    public class TimeDomain
    {
        /// <summary>
        /// Sets if the channel is enabled or not
        /// </summary>
        public bool IsEnabled { get; set; }
        /// <summary>
        /// Sets the High Pass filter
        /// </summary>
        public double Hpf { get; set; }
        /// <summary>
        /// Sets the Low Pass filter 1
        /// </summary>
        public int Lpf1 { get; set; }
        /// <summary>
        /// Sets the Low Pass filter 2
        /// </summary>
        public int Lpf2 { get; set; }
        /// <summary>
        /// Anode and Cathode Inputs for the channel
        /// </summary>
        public List<int> Inputs { get; set; }
    }
    /// <summary>
    /// FFT Object
    /// </summary>
    public class FFT
    {
        /// <summary>
        /// channel to stream FFT
        /// </summary>
        public int Channel { get; set; }
        /// <summary>
        /// FFT size
        /// </summary>
        public int FftSize { get; set; }
        /// <summary>
        /// FFT Interval
        /// </summary>
        public ushort FftInterval { get; set; }
        /// <summary>
        /// Window load
        /// </summary>
        public int WindowLoad { get; set; }
        /// <summary>
        /// Stream Size for bin
        /// </summary>
        public ushort StreamSizeBins { get; set; }
        /// <summary>
        /// Stream offset Bin
        /// </summary>
        public ushort StreamOffsetBins { get; set; }
        /// <summary>
        /// Window enabled or not
        /// </summary>
        public bool WindowEnabled { get; set; }
    }
    /// <summary>
    /// Power Object
    /// </summary>
    public class Power
    {
        /// <summary>
        /// Lower power band in 0 index and upper power band in 1 index
        /// </summary>
        public List<ushort> ChannelPowerBand { get; set; }
        /// <summary>
        /// If the power channel is enabled or not
        /// </summary>
        public bool IsEnabled { get; set; }
    }
    /// <summary>
    /// Accelerometer Object
    /// </summary>
    public class Accelerometer
    {
        /// <summary>
        /// If the accelerometer is enabled or not
        /// </summary>
        public bool SampleRateDisabled { get; set; }
        /// <summary>
        /// Sample rate for accelerometer
        /// </summary>
        public int SampleRate { get; set; }
    }
    /// <summary>
    /// Misc Object
    /// </summary>
    public class Misc
    {
        /// <summary>
        /// Streaming Rate
        /// </summary>
        public int StreamingRate { get; set; }
        /// <summary>
        /// Loop Recording Trigger State
        /// </summary>
        public int LoopRecordingTriggersState { get; set; }
        /// <summary>
        /// Loop Recording Trigger is enabled or not
        /// </summary>
        public bool LoopRecordingTriggersIsEnabled { get; set; }
        /// <summary>
        /// Loop recording post buffer time
        /// </summary>
        public ushort LoopRecordingPostBufferTime { get; set; }
        /// <summary>
        /// Bridging
        /// </summary>
        public int Bridging { get; set; }
    }
}
