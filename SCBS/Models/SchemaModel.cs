using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    /// <summary>
    /// Class that contains the Schema for Adaptive, Sense, and Report Config JSON File
    /// This class is used to get the schema for each config file to validate them
    /// Validation is done in JSONService
    /// </summary>
    public class SchemaModel
    {
        private string senseSchema = @"{
                      'type': 'object',
                      'required': [],
                      'properties': {
                        'eventType': {
                          'type': 'object',
                          'required': [],
                          'properties': {
                            'comment': {
                              'type': 'string'
                            },
                            'type': {
                              'type': 'string'
                            }
                          }
                        },
                        'Mode': {
                          'type': 'number'
                        },
                        'Ratio': {
                          'type': 'number'
                        },
                        'SenseOptions': {
                          'type': 'object',
                          'required': [],
                          'properties': {
                            'comment': {
                              'type': 'string'
                            },
                            'TimeDomain': {
                              'type': 'boolean'
                            },
                            'FFT': {
                              'type': 'boolean'
                            },
                            'Power': {
                              'type': 'boolean'
                            },
                            'LD0': {
                              'type': 'boolean'
                            },
                            'LD1': {
                              'type': 'boolean'
                            },
                            'AdaptiveState': {
                              'type': 'boolean'
                            },
                            'LoopRecording': {
                              'type': 'boolean'
                            },
                            'Unused': {
                              'type': 'boolean'
                            }
                          }
                        },
                        'StreamEnables': {
                          'type': 'object',
                          'required': [],
                          'properties': {
                            'comment': {
                              'type': 'string'
                            },
                            'TimeDomain': {
                              'type': 'boolean'
                            },
                            'FFT': {
                              'type': 'boolean'
                            },
                            'Power': {
                              'type': 'boolean'
                            },
                            'Accelerometry': {
                              'type': 'boolean'
                            },
                            'AdaptiveTherapy': {
                              'type': 'boolean'
                            },
                            'AdaptiveState': {
                              'type': 'boolean'
                            },
                            'EventMarker': {
                              'type': 'boolean'
                            },
                            'TimeStamp': {
                              'type': 'boolean'
                            }
                          }
                        },
                        'Sense': {
                          'type': 'object',
                          'required': [],
                          'properties': {
                            'commentTDChannelDefinitions': {
                              'type': 'string'
                            },
                            'commentFilters': {
                              'type': 'string'
                            },
                            'TDSampleRate': {
                              'type': 'number'
                            },
                            'TimeDomains': {
                              'type': 'array',
                              'items': {
                                'type': 'object',
                                'required': [],
                                'properties': {
                                  'ch0': {
                                    'type': 'string'
                                  },
                                  'IsEnabled': {
                                    'type': 'boolean'
                                  },
                                  'Hpf': {
                                    'type': 'number'
                                  },
                                  'Lpf1': {
                                    'type': 'number'
                                  },
                                  'Lpf2': {
                                    'type': 'number'
                                  },
                                  'Inputs': {
                                    'type': 'array',
                                    'items': {
                                      'type': 'number'
                                    }
                                  }
                                }
                              }
                            },
                            'FFT': {
                              'type': 'object',
                              'required': [],
                              'properties': {
                                'commentFFTParameters': {
                                  'type': 'string'
                                },
                                'Channel': {
                                  'type': 'number'
                                },
                                'FftSize': {
                                  'type': 'number'
                                },
                                'FftInterval': {
                                  'type': 'number'
                                },
                                'WindowLoad': {
                                  'type': 'number'
                                },
                                'StreamSizeBins': {
                                  'type': 'number'
                                },
                                'StreamOffsetBins': {
                                  'type': 'number'
                                },
                                'WindowEnabled': {
                                  'type': 'boolean'
                                }
                              }
                            },
                            'Power': {
                              'type': 'array',
                              'items': {
                                'type': 'object',
                                'required': [],
                                'properties': {
                                  'comment': {
                                    'type': 'string'
                                  },
                                  'ChannelPowerBand': {
                                    'type': 'array',
                                    'items': {
                                      'type': 'number'
                                    }
                                  },
                                  'IsEnabled': {
                                    'type': 'boolean'
                                  }
                                }
                              }
                            },
                            'Accelerometer': {
                              'type': 'object',
                              'required': [],
                              'properties': {
                                'commentAcc': {
                                  'type': 'string'
                                },
                                'SampleRateDisabled': {
                                  'type': 'boolean'
                                },
                                'SampleRate': {
                                  'type': 'number'
                                }
                              }
                            },
                              'Misc' : {
                                'type': 'object',
                              'required': [],
                              'properties': {
                                'commentMiscParameters': {
                                  'type': 'string'
                                },
                                  'StreamingRate': {
                                    'type': 'number'
                                  },
                                    'LoopRecordingTriggersState':{
                                      'type': 'number'
                                    },
                                'LoopRecordingTriggersIsEnabled':{
                                  'type': 'boolean'
                                },
                                  'LoopRecordingPostBufferTime' :{
                                    'type': 'number'
                                  },
                                    'Bridging':{
                                      'type': 'number'
                                    }
                              }
                              }
                          }
                        }
                      }
                    }";

        private string adaptiveSchema = @"{
                              'type': 'object',
                              'required': [],
                              'properties': {
                                'eventType': {
                                  'type': 'object',
                                  'required': [],
                                  'properties': {
                                    'comment': {
                                      'type': 'string'
                                    }
                                  }
                                },
                                'Detection': {
                                  'type': 'object',
                                  'required': [],
                                  'properties': {
                                    'LD0': {
                                      'type': 'object',
                                      'required': [],
                                      'properties': {
                                        'comment': {
                                          'type': 'string'
                                        },
                                        'B0': {
                                          'type': 'number'
                                        },
                                        'B1': {
                                          'type': 'number'
                                        },
                                        'UpdateRate': {
                                          'type': 'number'
                                        },
                                        'OnsetDuration': {
                                          'type': 'number'
                                        },
                                        'TerminationDuration': {
                                          'type': 'number'
                                        },
                                        'HoldOffOnStartupTime': {
                                          'type': 'number'
                                        },
                                        'StateChangeBlankingUponStateChange': {
                                          'type': 'number'
                                        },
                                        'FractionalFixedPointValue': {
                                          'type': 'number'
                                        },
                                        'DualThreshold': {
                                          'type': 'boolean'
                                        },
                                        'BlankBothLD': {
                                          'type': 'boolean'
                                        },
                                        'Inputs': {
                                          'type': 'object',
                                          'required': [],
                                          'properties': {
                                            'Ch0Band0': {
                                              'type': 'boolean'
                                            },
                                            'Ch0Band1': {
                                              'type': 'boolean'
                                            },
                                            'Ch1Band0': {
                                              'type': 'boolean'
                                            },
                                            'Ch1Band1': {
                                              'type': 'boolean'
                                            },
                                            'Ch2Band0': {
                                              'type': 'boolean'
                                            },
                                            'Ch2Band1': {
                                              'type': 'boolean'
                                            },
                                            'Ch3Band0': {
                                              'type': 'boolean'
                                            },
                                            'Ch3Band1': {
                                              'type': 'boolean'
                                            }
                                          }
                                        },
                                        'WeightVector': {
                                          'type': 'array',
                                          'items': {
                                            'type': 'number'
                                          }
                                        },
                                        'NormalizationMultiplyVector': {
                                          'type': 'array',
                                          'items': {
                                            'type': 'number'
                                          }
                                        },
                                        'NormalizationSubtractVector': {
                                          'type': 'array',
                                          'items': {
                                            'type': 'number'
                                          }
                                        }
                                      }
                                    },
                                    'LD1': {
                                      'type': 'object',
                                      'required': [],
                                      'properties': {
                                        'comment': {
                                          'type': 'string'
                                        },
                                        'B0': {
                                          'type': 'number'
                                        },
                                        'B1': {
                                          'type': 'number'
                                        },
                                        'UpdateRate': {
                                          'type': 'number'
                                        },
                                        'OnsetDuration': {
                                          'type': 'number'
                                        },
                                        'TerminationDuration': {
                                          'type': 'number'
                                        },
                                        'HoldOffOnStartupTime': {
                                          'type': 'number'
                                        },
                                        'StateChangeBlankingUponStateChange': {
                                          'type': 'number'
                                        },
                                        'FractionalFixedPointValue': {
                                          'type': 'number'
                                        },
                                        'DualThreshold': {
                                          'type': 'boolean'
                                        },
                                        'BlankBothLD': {
                                          'type': 'boolean'
                                        },
                                        'Inputs': {
                                          'type': 'object',
                                          'required': [],
                                          'properties': {
                                            'Ch0Band0': {
                                              'type': 'boolean'
                                            },
                                            'Ch0Band1': {
                                              'type': 'boolean'
                                            },
                                            'Ch1Band0': {
                                              'type': 'boolean'
                                            },
                                            'Ch1Band1': {
                                              'type': 'boolean'
                                            },
                                            'Ch2Band0': {
                                              'type': 'boolean'
                                            },
                                            'Ch2Band1': {
                                              'type': 'boolean'
                                            },
                                            'Ch3Band0': {
                                              'type': 'boolean'
                                            },
                                            'Ch3Band1': {
                                              'type': 'boolean'
                                            }
                                          }
                                        },
                                        'WeightVector': {
                                          'type': 'array',
                                          'items': {
                                            'type': 'number'
                                          }
                                        },
                                        'NormalizationMultiplyVector': {
                                          'type': 'array',
                                          'items': {
                                            'type': 'number'
                                          }
                                        },
                                        'NormalizationSubtractVector': {
                                          'type': 'array',
                                          'items': {
                                            'type': 'number'
                                          }
                                        }
                                      }
                                    }
                                  }
                                },
                                'Adaptive': {
                                  'type': 'object',
                                  'required': [],
                                  'properties': {
                                    'Program0': {
                                      'type': 'object',
                                      'required': [],
                                      'properties': {
		                              'comment': {
                                          'type': 'string'
                                        },
                                        'RiseTimes': {
                                          'type': 'number'
                                        },
                                        'FallTimes': {
                                          'type': 'number'
                                        },
                                        'RateTargetInHz': {
                                          'type': 'number'
                                        },
                                        'State0AmpInMilliamps': {
                                          'type': 'number'
                                        },
                                        'State1AmpInMilliamps': {
                                          'type': 'number'
                                        },
                                        'State2AmpInMilliamps': {
                                          'type': 'number'
                                        },
                                          'State3AmpInMilliamps': {
                                          'type': 'number'
                                        },
                                        'State4AmpInMilliamps': {
                                          'type': 'number'
                                        },
                                        'State5AmpInMilliamps': {
                                          'type': 'number'
                                        },
                                          'State6AmpInMilliamps': {
                                          'type': 'number'
                                        },
                                        'State7AmpInMilliamps': {
                                          'type': 'number'
                                        },
                                        'State8AmpInMilliamps': {
                                          'type': 'number'
                                        }
                                      }
                                    }
                                  }
                                }
                              }
                            }";

        private string reportSchema = @"{
                          'type': 'object',
                          'required': [],
                          'properties': {
                            'comment': {
                              'type': 'string'
                            },
                            'Medications': {
                              'type': 'array',
                              'items': {
                                'type': 'string'
                              }
                            },
                            'Symptoms': {
                              'type': 'array',
                              'items': {
                                'type': 'string'
                              }
                            }
                          }
                        }";

        private string applicationSchema = @"{
                                      'type': 'object',
                                      'required': [],
                                      'properties': {
                                        'comment': {
                                          'type': 'string'
                                        },
                                        'BasePathToJSONFiles': {
                                          'type': 'string'
                                        },
                                        'Bilateral': {
                                          'type': 'boolean'
                                        },
                                        'Switch': {
                                          'type': 'boolean'
                                        },
                                        'Align': {
                                          'type': 'boolean'
                                        },
                                        'Montage': {
                                          'type': 'boolean'
                                        },
                                        'StimSweep': {
                                          'type': 'boolean'
                                        },
                                        'NewSession': {
                                          'type': 'boolean'
                                        },
                                        'HideReportButton': {
                                          'type': 'boolean'
                                        },
                                        'GetAdaptiveLogInfo': {
                                          'type': 'boolean'
                                        },
                                        'GetAdaptiveMirrorInfo': {
                                          'type': 'boolean'
                                        },
                                        'VerboseLogOnForMedtronic': {
                                          'type': 'boolean'
                                        },
                                        'LogBeepEvent': {
                                          'type': 'boolean'
                                        },
                                        'CTMBeepEnables': {
                                          'type': 'object',
                                          'required': [],
                                          'properties': {
                                            'comment': {
                                              'type': 'string'
                                            },
                                            'None': {
                                              'type': 'boolean'
                                            },
                                            'GeneralAlert': {
                                              'type': 'boolean'
                                            },
                                            'TelMCompleted': {
                                              'type': 'boolean'
                                            },
                                            'DeviceDiscovered': {
                                              'type': 'boolean'
                                            },
                                            'NoDeviceDiscovered': {
                                              'type': 'boolean'
                                            },
                                            'TelMLost': {
                                              'type': 'boolean'
                                            }
                                          }
                                        },
                                        'WebPageButtons': {
                                          'type': 'object',
                                          'required': [],
                                          'properties': {
                                            'comment': {
                                              'type': 'string'
                                            },
                                            'OpenWithoutBeingConnected': {
                                              'type': 'boolean'
                                            },
                                            'WebPageOneButtonEnabled': {
                                              'type': 'boolean'
                                            },
                                            'WebPageOneURL': {
                                              'type': 'string'
                                            },
                                            'WebPageOneButtonText': {
                                              'type': 'string'
                                            },
                                            'WebPageTwoButtonEnabled': {
                                              'type': 'boolean'
                                            },
                                            'WebPageTwoURL': {
                                              'type': 'string'
                                            },
                                            'WebPageTwoButtonText': {
                                              'type': 'string'
                                            }
                                          }
                                        },
                                          'MoveGroupButton': {
                                          'type': 'object',
                                          'required': [],
                                          'properties': {
                                            'comment': {
                                              'type': 'string'
                                            },
                                            'MoveGroupButtonText': {
                                              'type': 'string'
                                            },
                                            'MoveGroupButtonEnabled': {
                                              'type': 'boolean'
                                            },
                                            'GroupToMoveToLeftUnilateral': {
                                              'type': 'string'
                                            },
                                            'GroupToMoveToRight': {
                                              'type': 'string'
                                            },
                                          }
                                        }
                                      }
                                    }";

        private string masterSwitchSchema = @"{
                          'type': 'object',
                          'required': [],
                          'properties': {
                            'comment': {
                              'type': 'string'
                            },
                            'DateTimeLastSwitch': {
                              'type': 'string'
                            },
                            'WaitTimeInMinutes': {
                              'type': 'number'
                            },
                            'WaitTimeIsEnabled': {
                              'type': 'boolean'
                            },
                            'CurrentIndex': {
                              'type': 'number'
                            },
                            'ConfigNames': {
                              'type': 'array',
                              'items': {
                                'type': 'string'
                              }
                            }
                          }
                        }";

        private string montageSchema = @"{
                                  'MontageFiles': {
                                    'type': 'object',
                                    'required': [],
                                    'properties': {
                                      'comment': {
                                        'type': 'string'
                                      },
                                      'Instructions': {
                                        'type': 'string'
                                      },
                                      'type': 'array',
                                      'items': {
                                        'type': 'object',
                                        'required': [],
                                        'properties': {
                                          'Filename': {
                                            'type': 'string'
                                          },
                                          'TimeToRunInSeconds': {
                                            'type': 'number'
                                          }
                                        }
                                      }
}
                                  }
                                }";

        private string stimSweepSchema = @"{
                                      'type': 'object',
                                      'required': [],
                                      'properties': {
                                        'comment': {
                                          'type': 'string'
                                        },
                                        'LeftINSOrUnilateral': {
                                          'type': 'object',
                                          'required': [],
                                          'properties': {
                                            'GroupToRunStimSweep': {
                                              'type': 'string'
                                            },
                                            'RateInHz': {
                                              'type': 'array',
                                              'items': {
                                                'type': 'number'
                                              }
                                            },
                                            'Program': {
                                              'type': 'array',
                                              'items': {
                                                'type': 'number'
                                              }
                                            },
                                            'AmpInmA': {
                                              'type': 'array',
                                              'items': {
                                                'type': 'number'
                                              }
                                            },
                                            'PulseWidthInMicroSeconds': {
                                              'type': 'array',
                                              'items': {
                                                'type': 'number'
                                              }
                                            }
                                          }
                                        },
                                        'RightINS': {
                                          'type': 'object',
                                          'required': [],
                                          'properties': {
                                            'GroupToRunStimSweep': {
                                              'type': 'string'
                                            },
                                            'RateInHz': {
                                              'type': 'array',
                                              'items': {
                                                'type': 'number'
                                              }
                                            },
                                            'Program': {
                                              'type': 'array',
                                              'items': {
                                                'type': 'number'
                                              }
                                            },
                                            'AmpInmA': {
                                              'type': 'array',
                                              'items': {
                                                'type': 'number'
                                              }
                                            },
                                            'PulseWidthInMicroSeconds': {
                                              'type': 'array',
                                              'items': {
                                                'type': 'number'
                                              }
                                            }
                                          }
                                        },
                                        'TimeToRunInSeconds': {
                                          'type': 'array',
                                          'items': {
                                            'type': 'number'
                                          }
                                        },
                                        'EventMarkerDelayTimeInSeconds': {
                                          'type': 'number'
                                        },
                                        'CurrentIndex': {
                                          'type': 'number'
                                        }
                                      }
                                    }";

        /// <summary>
        /// Gets the Schema for Sense
        /// </summary>
        /// <returns>string that correlates to the sense schema</returns>
        public string GetSenseSchema()
        {
            return senseSchema;
        }

        /// <summary>
        /// Gets the Schema for Report
        /// </summary>
        /// <returns>string that correlates to the report schema</returns>
        public string GetReportSchema()
        {
            return reportSchema;
        }

        /// <summary>
        /// Gets the schema for application settings
        /// </summary>
        /// <returns>string that correlates to the application schema</returns>
        public string GetApplicationSchema()
        {
            return applicationSchema;
        }

        /// <summary>
        /// Gets the schema for master switch config files
        /// </summary>
        /// <returns>string that correlates to the master switch schema</returns>
        public string GetMasterSwitchSchema()
        {
            return masterSwitchSchema;
        }

        /// <summary>
        /// Gets the Schema for Adaptive
        /// </summary>
        /// <returns>string that correlates to the Adaptive schema</returns>
        public string GetAdaptiveSchema()
        {
            return adaptiveSchema;
        }

        /// <summary>
        /// Gets the Schema for Montage
        /// </summary>
        /// <returns>string that correlates to the montage schema</returns>
        public string GetMontageSchema()
        {
            return montageSchema;
        }

        /// <summary>
        /// Gets the Schema for Stim Sweep
        /// </summary>
        /// <returns>string that correlates to the stim sweep schema</returns>
        public string GetStimSweepSchema()
        {
            return stimSweepSchema;
        }
    }
}
