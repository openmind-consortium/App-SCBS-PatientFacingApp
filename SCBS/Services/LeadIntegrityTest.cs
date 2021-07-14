using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.Measurement;
using Medtronic.SummitAPI.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Services
{
    class LeadIntegrityTest
    {
        private byte caseValue = 16;
        private ILog _log;
        public LeadIntegrityTest(ILog log)
        {
            _log = log;
        }
        /// <summary>
        /// Run a flattened lead integrity test
        /// </summary>
        public async Task RunLeadIntegrityTest(SummitSystem theSummit)
        {
            await Task.Run(() => RunLeadIntegrity(theSummit));
        }

        private void RunLeadIntegrity(SummitSystem theSummit)
        {
            if (theSummit != null && !theSummit.IsDisposed)
            {
                try
                {
                    LeadIntegrityTestResult testResultBuffer;
                    APIReturnInfo testReturnInfo;
                    testReturnInfo = theSummit.LeadIntegrityTest(
                                    new List<Tuple<byte, byte>> {
                                            new Tuple<byte, byte>(0, caseValue),
                                            new Tuple<byte, byte>(1, caseValue),
                                            new Tuple<byte, byte>(2, caseValue),
                                            new Tuple<byte, byte>(3, caseValue),
                                            new Tuple<byte, byte>(0, 1),
                                            new Tuple<byte, byte>(0, 2),
                                            new Tuple<byte, byte>(0, 3),
                                            new Tuple<byte, byte>(1, 2),
                                            new Tuple<byte, byte>(1, 3),
                                            new Tuple<byte, byte>(2, 3)
                },
                out testResultBuffer);
                    // Make sure returned structure isn't null
                    if (testResultBuffer != null && testReturnInfo.RejectCode == 0)
                    {
                        // Write out result to the console
                        //Messages.Add("Test Result Impedance (0, " + caseValue + "): " + testResultBuffer.PairResults[0].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(0," + caseValue + ")", testResultBuffer.PairResults[0].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (1, " + caseValue + "): " + testResultBuffer.PairResults[1].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(1," + caseValue + ")", testResultBuffer.PairResults[1].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (2, " + caseValue + "): " + testResultBuffer.PairResults[2].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(2," + caseValue + ")", testResultBuffer.PairResults[2].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (3, " + caseValue + "): " + testResultBuffer.PairResults[3].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(3," + caseValue + ")", testResultBuffer.PairResults[3].Impedance.ToString());
                        //Messages.Add("Test Result Impedance (0, 1): " + testResultBuffer.PairResults[4].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(0,1)", testResultBuffer.PairResults[4].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (0, 2): " + testResultBuffer.PairResults[5].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(0,2)", testResultBuffer.PairResults[5].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (0, 3): " + testResultBuffer.PairResults[6].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(0,3)", testResultBuffer.PairResults[6].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (1, 2): " + testResultBuffer.PairResults[7].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(1,2)", testResultBuffer.PairResults[7].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (1, 3): " + testResultBuffer.PairResults[8].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(1,3)", testResultBuffer.PairResults[8].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (2, 3): " + testResultBuffer.PairResults[9].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(2,3)", testResultBuffer.PairResults[9].Impedance.ToString());
                    }
                    else
                    {
                        ShowMessageBox.Show("ERROR from Medtronic API. Reject Description: " + testReturnInfo.Descriptor + ". Reject Code: " + testReturnInfo.RejectCode);
                    }
                }
                catch (Exception e)
                {
                    ShowMessageBox.Show("ERROR: Could not run Lead Integrity Test. Please try again");
                    _log.Error(e);
                    return;
                }

                try
                {
                    LeadIntegrityTestResult testResultBuffer;
                    APIReturnInfo testReturnInfo;
                    testReturnInfo = theSummit.LeadIntegrityTest(
                                    new List<Tuple<byte, byte>> {
                                            new Tuple<byte, byte>(8, caseValue),
                                            new Tuple<byte, byte>(9, caseValue),
                                            new Tuple<byte, byte>(10, caseValue),
                                            new Tuple<byte, byte>(11, caseValue),
                                            new Tuple<byte, byte>(8, 9),
                                            new Tuple<byte, byte>(8, 10),
                                            new Tuple<byte, byte>(8, 11),
                                            new Tuple<byte, byte>(9, 10),
                                            new Tuple<byte, byte>(9, 11),
                                            new Tuple<byte, byte>(10, 11)
                },
                out testResultBuffer);
                    // Make sure returned structure isn't null
                    if (testResultBuffer != null && testReturnInfo.RejectCode == 0)
                    {
                        // Write out result to the console
                        //Messages.Add("Test Result Impedance: (8, " + caseValue + "): " + testResultBuffer.PairResults[0].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(8," + caseValue + ")", testResultBuffer.PairResults[0].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (9, " + caseValue + "): " + testResultBuffer.PairResults[1].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(9," + caseValue + ")", testResultBuffer.PairResults[1].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (10, " + caseValue + "): " + testResultBuffer.PairResults[2].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(10," + caseValue + ")", testResultBuffer.PairResults[2].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (11, " + caseValue + "): " + testResultBuffer.PairResults[3].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(11," + caseValue + ")", testResultBuffer.PairResults[3].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (8, 9): " + testResultBuffer.PairResults[4].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(8,9)", testResultBuffer.PairResults[4].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (8, 10): " + testResultBuffer.PairResults[5].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(8,10)", testResultBuffer.PairResults[5].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (8, 11): " + testResultBuffer.PairResults[6].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(8,11)", testResultBuffer.PairResults[6].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (9, 10): " + testResultBuffer.PairResults[7].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(9,10)", testResultBuffer.PairResults[7].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (9, 11): " + testResultBuffer.PairResults[8].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(9,11)", testResultBuffer.PairResults[8].Impedance.ToString());
                        //Messages.Add("Test Result Impedance: (10, 11): " + testResultBuffer.PairResults[9].Impedance.ToString());
                        LogLeadIntegrityAsEvent(theSummit, "(10,11)", testResultBuffer.PairResults[9].Impedance.ToString());
                    }
                    else
                    {
                        ShowMessageBox.Show("ERROR from Medtronic API. Reject Description: " + testReturnInfo.Descriptor + ". Reject Code: " + testReturnInfo.RejectCode);
                    }
                }
                catch (Exception e)
                {
                    ShowMessageBox.Show("ERROR: Could not run Lead Integrity Test. Please try again");
                    _log.Error(e);
                    return;
                }
            }
        }
        private void LogLeadIntegrityAsEvent(SummitSystem theSummit, string pairs, string result)
        {
            APIReturnInfo bufferReturnInfo;
            try
            {
                bufferReturnInfo = theSummit.LogCustomEvent(DateTime.Now, DateTime.Now, "Lead Integrity", pairs + " --- " + result);
                if (bufferReturnInfo.RejectCode != 0)
                {
                    ShowMessageBox.Show("Could not log event. If you would like all lead integrity results logged, please try again.", "Error Logging");
                    _log.Warn("Could not log lead integrity event. Reject code: " + bufferReturnInfo.RejectCode + ". Reject description: " + bufferReturnInfo.Descriptor);
                }
            }
            catch (Exception e)
            {
                ShowMessageBox.Show("Could not log event. If you would like all lead integrity results logged, please try again.", "Error Logging");
                _log.Error(e);
            }
        }
    }
}

