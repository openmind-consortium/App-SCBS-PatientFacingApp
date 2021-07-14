using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.Sensing;
using Medtronic.SummitAPI.Classes;
using Medtronic.SummitAPI.Events;
using SCBS.Models;
using SCBS.Services;
using SciChart.Charting.Model.ChartSeries;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.Axes;
using SciChart.Data.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.ViewModels
{
    /// <summary>
    /// Visualizes the fft
    /// </summary>
    public class FFTVisualizerViewModel : Screen
    {
        private const string FFT_AUTOSCALE_CHART_OPTION = "AutoScale";
        private const string FFT_NONE_CHART_OPTION = "None";
        private const string FFT_LOG10_CHART_OPTION = "Log10";
        /// <summary>
        /// FFT y axes
        /// </summary>
        public ObservableCollection<IAxisViewModel> YAxesFFT { get { return _yAxesFFT; } }
        private ObservableCollection<IAxisViewModel> _yAxesFFT = new ObservableCollection<IAxisViewModel>();
        private IDataSeries<double, double> _fFTChartLeftUnilateral = new XyDataSeries<double, double>();
        private IDataSeries<double, double> _fFTChartRight = new XyDataSeries<double, double>();
        private BindableCollection<string> _fftScaleOptions = new BindableCollection<string>();
        private string _selectedFFTScaleOption, _selectedFFTLog10Option;
        private SummitSystem theSummitLeft, theSummitRight;
        private readonly int FONTSIZE = 20;
        private SenseModel senseLeftModelConfig;
        private SenseModel senseRightModelConfig;
        private List<double> fftBinsLeft = new List<double>();
        private List<double> fftBinsRight = new List<double>();
        private ILog _log;
        private bool _fFTChannelZeroLeft, _fFTChannelOneLeft, _fFTChannelTwoLeft, _fFTChannelThreeLeft;
        private bool _fFTChannelZeroRight, _fFTChannelOneRight, _fFTChannelTwoRight, _fFTChannelThreeRight;
        private string _fFTChannelZeroTextLeft, _fFTChannelOneTextLeft, _fFTChannelTwoTextLeft, _fFTChannelThreeTextLeft,
            _fFTChannelZeroTextRight, _fFTChannelOneTextRight, _fFTChannelTwoTextRight, _fFTChannelThreeTextRight;
        private BindableCollection<string> _fFTLog10Options = new BindableCollection<string>();
        private string _fFTMean;
        private int userInputForFFTMean = 10;

        private List<List<double>> rollingMeanLeft = new List<List<double>>();
        private int initialCountForRollingMeanLeft = 0;
        private List<List<double>> rollingMeanRight = new List<List<double>>();
        private int initialCountForRollingMeanRight = 0;

        /// <summary>
        /// Constructor for fft visualizer
        /// </summary>
        /// <param name="summitLeft">Summit system for left</param>
        /// <param name="summitRight">Summit system for right</param>
        /// <param name="senseLeftModelConfig">Sense model for left</param>
        /// <param name="senseRightModelConfig">Sense model for right</param>
        /// <param name="_log">Caliburn micro log</param>
        public FFTVisualizerViewModel(SummitSystem summitLeft, SummitSystem summitRight, SenseModel senseLeftModelConfig, SenseModel senseRightModelConfig, ILog _log)
        {
            theSummitLeft = summitLeft;
            theSummitRight = summitRight;
            if(theSummitLeft != null)
            {
                theSummitLeft.DataReceivedFFTHandler += theSummit_DataReceived_FFT_Left;
            }
            if(theSummitRight != null)
            {
                theSummitRight.DataReceivedFFTHandler += theSummit_DataReceived_FFT_Right;
            }
            
            FFTChartLeftUnilateral.SeriesName = "FFT Left/Unilateral";
            FFTChartRight.SeriesName = "FFT Right";
            YAxesFFT.Add(new NumericAxisViewModel()
            {
                AutoRange = AutoRange.Always,
                VisibleRange = new DoubleRange(0, 0.5),
                GrowBy = new DoubleRange(0.1, 0.1),
                AxisTitle = "FFT Values",
                Id = "FFTID",
                FontSize = FONTSIZE,
                AxisAlignment = AxisAlignment.Left,
            });
            this._log = _log;
            this.senseLeftModelConfig = senseLeftModelConfig;
            this.senseRightModelConfig = senseRightModelConfig;
            UpdateFFTSettings(this.senseLeftModelConfig, this.senseRightModelConfig);
            FFTScaleOptions.Add(FFT_AUTOSCALE_CHART_OPTION);
            FFTScaleOptions.Add(FFT_NONE_CHART_OPTION);
            SelectedFFTScaleOption = FFT_AUTOSCALE_CHART_OPTION;

            FFTMean = userInputForFFTMean.ToString();
            FFTLog10Options.Add(FFT_LOG10_CHART_OPTION);
            FFTLog10Options.Add(FFT_NONE_CHART_OPTION);
            SelectedFFTLog10Option = FFT_NONE_CHART_OPTION;

            UpdateLeftUnilateralFFTChannelRadioButtons();
            if(theSummitRight != null)
            {
                UpdateRightFFTChannelRadioButtons();
            }
        }

        /// <summary>
        /// Updates the fft chart with the new sense config files
        /// </summary>
        public void UpdateFFTSettings(SenseModel localSenseLeftModelConfig, SenseModel localSenseRightModelConfig)
        {
            if (localSenseLeftModelConfig != null && localSenseLeftModelConfig.Sense != null && localSenseLeftModelConfig.Sense.FFT != null)
            {
                senseLeftModelConfig = localSenseLeftModelConfig;
                UpdateLeftUnilateralFFTChannelRadioButtons();
                CalculatePowerBins calculatePowerBins = new CalculatePowerBins(_log);
                fftBinsLeft = calculatePowerBins.CalculateFFTBins(ConfigConversions.FftSizesConvert(localSenseLeftModelConfig.Sense.FFT.FftSize), ConfigConversions.TDSampleRateConvert(localSenseLeftModelConfig.Sense.TDSampleRate));
                if (localSenseLeftModelConfig.Sense.FFT.StreamSizeBins != 0)
                {
                    CalculateNewFFTBinsLeft();
                }
            }
            if (localSenseRightModelConfig != null && localSenseRightModelConfig.Sense != null && localSenseRightModelConfig.Sense.FFT != null)
            {
                senseRightModelConfig = localSenseRightModelConfig;
                UpdateRightFFTChannelRadioButtons();
                CalculatePowerBins calculatePowerBins = new CalculatePowerBins(_log);
                fftBinsRight = calculatePowerBins.CalculateFFTBins(ConfigConversions.FftSizesConvert(localSenseRightModelConfig.Sense.FFT.FftSize), ConfigConversions.TDSampleRateConvert(localSenseRightModelConfig.Sense.TDSampleRate));
                if (localSenseRightModelConfig.Sense.FFT.StreamSizeBins != 0)
                {
                    CalculateNewFFTBinsRight();
                }
            }
        }

        /// <summary>
        /// Button to set the FFT mean value
        /// </summary>
        public void FFTMeanButton()
        {
            //i is the acutal number of data points assuming the user gave an integer
            int i = 0;
            bool result = int.TryParse(FFTMean, out i);

            if (result)
            {
                initialCountForRollingMeanLeft = 0;
                rollingMeanLeft.Clear();
                initialCountForRollingMeanRight = 0;
                rollingMeanRight.Clear();
                userInputForFFTMean = i;
            }
        }

        #region FFT Chart Bindings
        /// <summary>
        /// Radio button text for FFT Channel 0
        /// </summary>
        public string FFTChannelZeroTextLeft
        {
            get { return _fFTChannelZeroTextLeft; }
            set
            {
                _fFTChannelZeroTextLeft = value;
                NotifyOfPropertyChange("FFTChannelZeroTextLeft");
            }
        }
        /// <summary>
        /// Radio button text for FFT Channel 1
        /// </summary>
        public string FFTChannelOneTextLeft
        {
            get { return _fFTChannelOneTextLeft; }
            set
            {
                _fFTChannelOneTextLeft = value;
                NotifyOfPropertyChange("FFTChannelOneTextLeft");
            }
        }
        /// <summary>
        /// Radio button text for FFT Channel 2
        /// </summary>
        public string FFTChannelTwoTextLeft
        {
            get { return _fFTChannelTwoTextLeft; }
            set
            {
                _fFTChannelTwoTextLeft = value;
                NotifyOfPropertyChange("FFTChannelTwoTextLeft");
            }
        }
        /// <summary>
        /// Radio button text for FFT Channel 3
        /// </summary>
        public string FFTChannelThreeTextLeft
        {
            get { return _fFTChannelThreeTextLeft; }
            set
            {
                _fFTChannelThreeTextLeft = value;
                NotifyOfPropertyChange("FFTChannelThreeTextLeft");
            }
        }
        /// <summary>
        /// Radio button text for FFT Channel 0
        /// </summary>
        public string FFTChannelZeroTextRight
        {
            get { return _fFTChannelZeroTextRight; }
            set
            {
                _fFTChannelZeroTextRight = value;
                NotifyOfPropertyChange("FFTChannelZeroTextRight");
            }
        }
        /// <summary>
        /// Radio button text for FFT Channel 1
        /// </summary>
        public string FFTChannelOneTextRight
        {
            get { return _fFTChannelOneTextRight; }
            set
            {
                _fFTChannelOneTextRight = value;
                NotifyOfPropertyChange("FFTChannelOneTextRight");
            }
        }
        /// <summary>
        /// Radio button text for FFT Channel 2
        /// </summary>
        public string FFTChannelTwoTextRight
        {
            get { return _fFTChannelTwoTextRight; }
            set
            {
                _fFTChannelTwoTextRight = value;
                NotifyOfPropertyChange("FFTChannelTwoTextRight");
            }
        }
        /// <summary>
        /// Radio button text for FFT Channel 3
        /// </summary>
        public string FFTChannelThreeTextRight
        {
            get { return _fFTChannelThreeTextRight; }
            set
            {
                _fFTChannelThreeTextRight = value;
                NotifyOfPropertyChange("FFTChannelThreeTextRight");
            }
        }
        /// <summary>
        /// Radio button for FFT Channel 0
        /// </summary>
        public bool FFTChannelZeroLeft
        {
            get { return _fFTChannelZeroLeft; }
            set
            {
                _fFTChannelZeroLeft = value;
                NotifyOfPropertyChange("FFTChannelZeroLeft");
                if (_fFTChannelZeroLeft)
                {
                    ChangeFFTChannelCode(SenseTimeDomainChannel.Ch0, theSummitLeft, senseLeftModelConfig);
                }
            }
        }
        /// <summary>
        /// Radio button for FFT Channel 1
        /// </summary>
        public bool FFTChannelOneLeft
        {
            get { return _fFTChannelOneLeft; }
            set
            {
                _fFTChannelOneLeft = value;
                NotifyOfPropertyChange("FFTChannelOneLeft");
                if (_fFTChannelOneLeft)
                {
                    ChangeFFTChannelCode(SenseTimeDomainChannel.Ch1, theSummitLeft, senseLeftModelConfig);
                }
            }
        }
        /// <summary>
        /// Radio button for FFT Channel 2
        /// </summary>
        public bool FFTChannelTwoLeft
        {
            get { return _fFTChannelTwoLeft; }
            set
            {
                _fFTChannelTwoLeft = value;
                NotifyOfPropertyChange("FFTChannelTwoLeft");
                if (_fFTChannelTwoLeft)
                {
                    ChangeFFTChannelCode(SenseTimeDomainChannel.Ch2, theSummitLeft, senseLeftModelConfig);
                }
            }
        }
        /// <summary>
        /// Radio button for FFT Channel 3
        /// </summary>
        public bool FFTChannelThreeLeft
        {
            get { return _fFTChannelThreeLeft; }
            set
            {
                _fFTChannelThreeLeft = value;
                NotifyOfPropertyChange("FFTChannelThreeLeft");
                if (_fFTChannelThreeLeft)
                {
                    ChangeFFTChannelCode(SenseTimeDomainChannel.Ch3, theSummitLeft, senseLeftModelConfig);
                }
            }
        }
        /// <summary>
        /// Radio button for FFT Channel 0
        /// </summary>
        public bool FFTChannelZeroRight
        {
            get { return _fFTChannelZeroRight; }
            set
            {
                _fFTChannelZeroRight = value;
                NotifyOfPropertyChange("FFTChannelZeroRight");
                if (_fFTChannelZeroRight)
                {
                    ChangeFFTChannelCode(SenseTimeDomainChannel.Ch0, theSummitRight, senseRightModelConfig);
                }
            }
        }
        /// <summary>
        /// Radio button for FFT Channel 1
        /// </summary>
        public bool FFTChannelOneRight
        {
            get { return _fFTChannelOneRight; }
            set
            {
                _fFTChannelOneRight = value;
                NotifyOfPropertyChange("FFTChannelOneRight");
                if (_fFTChannelOneRight)
                {
                    ChangeFFTChannelCode(SenseTimeDomainChannel.Ch1, theSummitRight, senseRightModelConfig);
                }
            }
        }
        /// <summary>
        /// Radio button for FFT Channel 2
        /// </summary>
        public bool FFTChannelTwoRight
        {
            get { return _fFTChannelTwoRight; }
            set
            {
                _fFTChannelTwoRight = value;
                NotifyOfPropertyChange("FFTChannelTwoRight");
                if (_fFTChannelTwoRight)
                {
                    ChangeFFTChannelCode(SenseTimeDomainChannel.Ch2, theSummitRight, senseRightModelConfig);
                }
            }
        }
        /// <summary>
        /// Radio button for FFT Channel 3
        /// </summary>
        public bool FFTChannelThreeRight
        {
            get { return _fFTChannelThreeRight; }
            set
            {
                _fFTChannelThreeRight = value;
                NotifyOfPropertyChange("FFTChannelThreeRight");
                if (_fFTChannelThreeRight)
                {
                    ChangeFFTChannelCode(SenseTimeDomainChannel.Ch3, theSummitRight, senseRightModelConfig);
                }
            }
        }
        /// <summary>
        /// Binding for the line chart for FFTChart Left or Unilateral
        /// </summary>
        public IDataSeries<double, double> FFTChartLeftUnilateral
        {
            get { return _fFTChartLeftUnilateral; }
            set
            {
                _fFTChartLeftUnilateral = value;
                NotifyOfPropertyChange("FFTChartLeftUnilateral");
            }
        }
        /// <summary>
        /// Binding for the line chart for FFTChart Right
        /// </summary>
        public IDataSeries<double, double> FFTChartRight
        {
            get { return _fFTChartRight; }
            set
            {
                _fFTChartRight = value;
                NotifyOfPropertyChange("FFTChartRight");
            }
        }
        /// <summary>
        /// Binding for the drop down menu for fft options
        /// </summary>
        public BindableCollection<string> FFTScaleOptions
        {
            get { return _fftScaleOptions; }
            set
            {
                _fftScaleOptions = value;
                NotifyOfPropertyChange(() => FFTScaleOptions);
            }
        }
        /// <summary>
        /// Binding for selected option for the drop down menu for FFT options
        /// </summary>
        public string SelectedFFTScaleOption
        {
            get { return _selectedFFTScaleOption; }
            set
            {
                _selectedFFTScaleOption = value;
                NotifyOfPropertyChange(() => SelectedFFTScaleOption);
                if (SelectedFFTScaleOption.Equals(FFT_AUTOSCALE_CHART_OPTION))
                {
                    YAxesFFT[0].AutoRange = AutoRange.Always;
                }
                else if (SelectedFFTScaleOption.Equals(FFT_NONE_CHART_OPTION))
                {
                    YAxesFFT[0].AutoRange = AutoRange.Never;
                }
            }
        }
        /// <summary>
        /// Binding for the drop down menu for fft log 10
        /// </summary>
        public BindableCollection<string> FFTLog10Options
        {
            get { return _fFTLog10Options; }
            set
            {
                _fFTLog10Options = value;
                NotifyOfPropertyChange(() => FFTLog10Options);
            }
        }
        /// <summary>
        /// Binding for selected option for the drop down menu for FFT log 10 options
        /// </summary>
        public string SelectedFFTLog10Option
        {
            get { return _selectedFFTLog10Option; }
            set
            {
                _selectedFFTLog10Option = value;
                NotifyOfPropertyChange(() => SelectedFFTLog10Option);
                if (SelectedFFTLog10Option.Equals(FFT_LOG10_CHART_OPTION))
                {
                    SelectedFFTScaleOption = FFT_NONE_CHART_OPTION;
                }
            }
        }
        /// <summary>
        /// The input should be an integer. This is the actual value for the fft mean. If not an integer, then nothing will happen.
        /// </summary>
        public string FFTMean
        {
            get { return _fFTMean; }
            set
            {
                _fFTMean = value;
                NotifyOfPropertyChange(() => FFTMean);
            }
        }
        #endregion

        private void theSummit_DataReceived_FFT_Left(object sender, SensingEventFFT FftSenseEvent)
        {
            rollingMeanLeft.Add(FftSenseEvent.FftOutput);
            if (initialCountForRollingMeanLeft < (userInputForFFTMean - 1))
            {
                initialCountForRollingMeanLeft++;
            }
            else
            {
                try
                {
                    List<double> tempList = new List<double>();
                    //find mean
                    for (int i = 0; i < FftSenseEvent.FftOutput.Count(); i++)
                    {
                        double mean = 0;
                        for (int j = 0; j < userInputForFFTMean; j++)
                        {
                            mean += rollingMeanLeft[j][i];
                        }
                        mean = mean / userInputForFFTMean;
                        tempList.Add(mean);
                    }
                    rollingMeanLeft.RemoveAt(0);
                    if(fftBinsLeft.Count == FftSenseEvent.FftOutput.Count)
                    {
                        if (SelectedFFTLog10Option.Equals(FFT_LOG10_CHART_OPTION))
                        {
                            //YAxesFFT[0].AxisTitle = "Log10mV^2/Hz";
                            //go through templist and change to log10 for each value
                            for (int i = 0; i < tempList.Count; i++)
                            {
                                tempList[i] = Math.Log10(tempList[i]);
                            }
                        }
                        lock (FFTChartLeftUnilateral.SyncRoot)
                        {
                            FFTChartLeftUnilateral.Clear();
                            FFTChartLeftUnilateral.Append(fftBinsLeft, tempList);
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }
        }

        private void theSummit_DataReceived_FFT_Right(object sender, SensingEventFFT FftSenseEvent)
        {
            rollingMeanRight.Add(FftSenseEvent.FftOutput);
            if (initialCountForRollingMeanRight < (userInputForFFTMean - 1))
            {
                initialCountForRollingMeanRight++;
            }
            else
            {
                try
                {
                    List<double> tempList = new List<double>();
                    //find mean
                    for (int i = 0; i < FftSenseEvent.FftOutput.Count(); i++)
                    {
                        double mean = 0;
                        for (int j = 0; j < userInputForFFTMean; j++)
                        {
                            mean += rollingMeanRight[j][i];
                        }
                        mean = mean / userInputForFFTMean;
                        tempList.Add(mean);
                    }
                    rollingMeanRight.RemoveAt(0);
                    if (fftBinsRight.Count == FftSenseEvent.FftOutput.Count)
                    {
                        if (SelectedFFTLog10Option.Equals(FFT_LOG10_CHART_OPTION))
                        {
                            //YAxesFFT[0].AxisTitle = "Log10mV^2/Hz";
                            //go through templist and change to log10 for each value
                            for (int i = 0; i < tempList.Count; i++)
                            {
                                tempList[i] = Math.Log10(tempList[i]);
                            }
                        }
                        lock (FFTChartRight.SyncRoot)
                        {
                            FFTChartRight.Clear();
                            FFTChartRight.Append(fftBinsRight, tempList);
                        }
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }
        }

        #region Helpers
        private void CalculateNewFFTBinsLeft()
        {
            List<double> tempFFTBins = new List<double>();
            int valueToGoUpTo = senseLeftModelConfig.Sense.FFT.StreamOffsetBins + senseLeftModelConfig.Sense.FFT.StreamSizeBins;
            for (int i = senseLeftModelConfig.Sense.FFT.StreamOffsetBins; i < valueToGoUpTo; i++)
            {
                tempFFTBins.Add(fftBinsLeft[i]);
            }
            fftBinsLeft.Clear();
            fftBinsLeft.AddRange(tempFFTBins);
        }
        private void CalculateNewFFTBinsRight()
        {
            List<double> tempFFTBins = new List<double>();
            int valueToGoUpTo = senseRightModelConfig.Sense.FFT.StreamOffsetBins + senseRightModelConfig.Sense.FFT.StreamSizeBins;
            for (int i = senseRightModelConfig.Sense.FFT.StreamOffsetBins; i < valueToGoUpTo; i++)
            {
                tempFFTBins.Add(fftBinsRight[i]);
            }
            fftBinsRight.Clear();
            fftBinsRight.AddRange(tempFFTBins);
        }

        private bool ChangeFFTChannelCode(SenseTimeDomainChannel channel, SummitSystem localSummit, SenseModel localSenseModel)
        {
            SummitSensing summitSensing = new SummitSensing(_log);
            if (localSummit != null && !localSummit.IsDisposed && localSenseModel != null)
            {
                try
                {
                    if (!summitSensing.StopSensing(localSummit, false))
                    {
                        return false;
                    }
                    localSenseModel.Sense.FFT.Channel = (int)channel;
                    if (!summitSensing.StartSensingAndStreaming(localSummit, localSenseModel, false))
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    return false;
                }
            }
            else
            {
                //error occurred
                return false;
            }
            return true;
        }

        private void UpdateLeftUnilateralFFTChannelRadioButtons()
        {
            switch (senseLeftModelConfig.Sense.FFT.Channel)
            {
                case 0:
                    FFTChannelZeroLeft = true;
                    FFTChannelOneLeft = false;
                    FFTChannelTwoLeft = false;
                    FFTChannelThreeLeft = false;
                    break;
                case 1:
                    FFTChannelZeroLeft = false;
                    FFTChannelOneLeft = true;
                    FFTChannelTwoLeft = false;
                    FFTChannelThreeLeft = false;
                    break;
                case 2:
                    FFTChannelZeroLeft = false;
                    FFTChannelOneLeft = false;
                    FFTChannelTwoLeft = true;
                    FFTChannelThreeLeft = false;
                    break;
                case 3:
                    FFTChannelZeroLeft = false;
                    FFTChannelOneLeft = false;
                    FFTChannelTwoLeft = false;
                    FFTChannelThreeLeft = true;
                    break;
                default:
                    FFTChannelZeroLeft = false;
                    FFTChannelOneLeft = false;
                    FFTChannelTwoLeft = false;
                    FFTChannelThreeLeft = false;
                    break;
            }
            FFTChannelZeroTextLeft = "Ch 0 (+" + senseLeftModelConfig.Sense.TimeDomains[0].Inputs[0] + "-" + senseLeftModelConfig.Sense.TimeDomains[0].Inputs[1] + ")";
            FFTChannelOneTextLeft = "Ch 1 (+" + senseLeftModelConfig.Sense.TimeDomains[1].Inputs[0] + "-" + senseLeftModelConfig.Sense.TimeDomains[1].Inputs[1] + ")";
            FFTChannelTwoTextLeft = "Ch 2 (+" + senseLeftModelConfig.Sense.TimeDomains[2].Inputs[0] + "-" + senseLeftModelConfig.Sense.TimeDomains[2].Inputs[1] + ")";
            FFTChannelThreeTextLeft = "Ch 3 (+" + senseLeftModelConfig.Sense.TimeDomains[3].Inputs[0] + "-" + senseLeftModelConfig.Sense.TimeDomains[3].Inputs[1] + ")";

        }

        private void UpdateRightFFTChannelRadioButtons()
        {
            switch (senseRightModelConfig.Sense.FFT.Channel)
            {
                case 0:
                    FFTChannelZeroRight = true;
                    FFTChannelOneRight = false;
                    FFTChannelTwoRight = false;
                    FFTChannelThreeRight = false;
                    break;
                case 1:
                    FFTChannelZeroRight = false;
                    FFTChannelOneRight = true;
                    FFTChannelTwoRight = false;
                    FFTChannelThreeRight = false;
                    break;
                case 2:
                    FFTChannelZeroRight = false;
                    FFTChannelOneRight = false;
                    FFTChannelTwoRight = true;
                    FFTChannelThreeRight = false;
                    break;
                case 3:
                    FFTChannelZeroRight = false;
                    FFTChannelOneRight = false;
                    FFTChannelTwoRight = false;
                    FFTChannelThreeRight = true;
                    break;
                default:
                    FFTChannelZeroRight = false;
                    FFTChannelOneRight = false;
                    FFTChannelTwoRight = false;
                    FFTChannelThreeRight = false;
                    break;
            }
            FFTChannelZeroTextRight = "Ch 0 (+" + senseRightModelConfig.Sense.TimeDomains[0].Inputs[0] + "-" + senseRightModelConfig.Sense.TimeDomains[0].Inputs[1] + ")";
            FFTChannelOneTextRight = "Ch 1 (+" + senseRightModelConfig.Sense.TimeDomains[1].Inputs[0] + "-" + senseRightModelConfig.Sense.TimeDomains[1].Inputs[1] + ")";
            FFTChannelTwoTextRight = "Ch 2 (+" + senseRightModelConfig.Sense.TimeDomains[2].Inputs[0] + "-" + senseRightModelConfig.Sense.TimeDomains[2].Inputs[1] + ")";
            FFTChannelThreeTextRight = "Ch 3 (+" + senseRightModelConfig.Sense.TimeDomains[3].Inputs[0] + "-" + senseRightModelConfig.Sense.TimeDomains[3].Inputs[1] + ")";

        }
        #endregion
    }
}
