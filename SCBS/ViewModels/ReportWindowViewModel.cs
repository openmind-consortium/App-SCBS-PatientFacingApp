using Medtronic.SummitAPI.Classes;
using SCBS.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Windows;
using System.IO;
using SCBS.Services;

namespace SCBS.ViewModels
{
    class ReportWindowViewModel : Screen
    {
        private static readonly string reportFilePath = @"C:\SCBS\report_config.json";
        private ILog _log;
        private SummitSystem theSummitLeft, theSummitRight;
        //MEDICATION used for storing in event log that we are storing medication data
        private readonly static string MEDICATION = "medication";
        private readonly static string CONDITIONS = "conditions";
        private readonly static string EXTRA_COMMENTS = "extra_comments";
        private static ReportModel reportModel = null;
        private JSONService jSONService;
        //variable to add additional comments from the report box. Used in AdditionalCommentsForReportBox
        private string _additionalCommentsForReportBox;
        //Binding for the medication list and condition list selectable boxes
        private ObservableCollection<MedicationCheckBoxClass> _medicationList = new ObservableCollection<MedicationCheckBoxClass>();
        private ObservableCollection<ConditionCheckBoxClass> _conditionList = new ObservableCollection<ConditionCheckBoxClass>();
        /// <summary>
        /// Binding for the Medication time
        /// </summary>
        public string MedicationTime { get; set; }

        public ReportWindowViewModel(ref SummitSystem theSummitLeft, ref SummitSystem theSummitRight, ILog _log)
        {
            this._log = _log;
            this.theSummitLeft = theSummitLeft;
            this.theSummitRight = theSummitRight;

            jSONService = new JSONService(_log);
            //Medication and Condition list for report window.
            //Both of these collection data come from the same json file
            MedicationList = new ObservableCollection<MedicationCheckBoxClass>();
            ConditionList = new ObservableCollection<ConditionCheckBoxClass>();
            //These two methods are implemented in ReportViewModel.cs
            reportModel = jSONService?.GetReportModelFromFile(reportFilePath);
            if (reportModel == null)
            {
                return;
            }
            GetListOfMedicationsConditionsFromModel();
        }

        #region Button Bindings for Report, Reset and Exit Button Clicks
        /// <summary>
        /// Report button click that reports data to Event log in medtronic json file
        /// </summary>
        public void ReportClick()
        {
            //this adds conditions to string
            string conditions = "";
            foreach (ConditionCheckBoxClass itemToAddIfChecked in ConditionList)
            {
                if (itemToAddIfChecked.IsSelected)
                    conditions += itemToAddIfChecked.Condition + ", ";
            }
            //This adds medications to string
            string medications = "";
            foreach (MedicationCheckBoxClass itemToCheckIfChecked in MedicationList)
            {
                if (itemToCheckIfChecked.IsSelected)
                    medications += itemToCheckIfChecked.Medication + ", ";
            }
            //This adds both medications and conditions to event log  
            //Make sure there is a medication checked before writing. No need to write if nothing there.
            if (!string.IsNullOrEmpty(medications))
            {
                //add time to medications only if available
                if (MedicationTime != null)
                {
                    medications += " --- " + MedicationTime.ToString();
                    medications = medications.Replace('/', '_');
                }
                APIReturnInfo result;
                //Adds data to event log
                if (theSummitLeft != null)
                {
                    if (!theSummitLeft.IsDisposed)
                    {
                        try
                        {
                            result = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, MEDICATION, medications);
                            CheckRejectCode(result, "Logging Medications");
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                            MessageBox.Show("Error Logging Medications", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                if(theSummitRight != null)
                {
                    if (!theSummitRight.IsDisposed)
                    {
                        try
                        {
                            result = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, MEDICATION, medications);
                            CheckRejectCode(result, "Logging Medications");
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                            MessageBox.Show("Error Logging Medications", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            //Make sure there is data for conditions checked
            //If no data, then nothing to write
            if (!string.IsNullOrEmpty(conditions))
            {
                //Adds data to event log
                APIReturnInfo result;
                if (theSummitLeft != null)
                {
                    if (!theSummitLeft.IsDisposed)
                    {
                        try
                        {
                            result = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, CONDITIONS, conditions);
                            CheckRejectCode(result, "Logging Conditions");
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                            MessageBox.Show("Error Logging conditions", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                if(theSummitRight != null)
                {
                    if (!theSummitRight.IsDisposed)
                    {
                        try
                        {
                            result = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, CONDITIONS, conditions);
                            CheckRejectCode(result, "Logging Conditions");
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                            MessageBox.Show("Error Logging conditions", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            //Make sure there is data inside the additional comments box
            //If no data, then nothing to write
            if (!string.IsNullOrEmpty(AdditionalCommentsForReportBox))
            {
                //Adds data to event log
                APIReturnInfo result;
                if (theSummitLeft != null)
                {
                    if (!theSummitLeft.IsDisposed)
                    {
                        try
                        {
                            result = theSummitLeft.LogCustomEvent(DateTime.Now, DateTime.Now, EXTRA_COMMENTS, AdditionalCommentsForReportBox);
                            CheckRejectCode(result, "Logging additional comments");
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                            MessageBox.Show("Error Logging additional Comments", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                if (theSummitRight != null)
                {
                    if (!theSummitRight.IsDisposed)
                    {
                        try
                        {
                            result = theSummitRight.LogCustomEvent(DateTime.Now, DateTime.Now, EXTRA_COMMENTS, AdditionalCommentsForReportBox);
                            CheckRejectCode(result, "Logging additional comments");
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                            MessageBox.Show("Error Logging additional Comments", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            TryClose();
        }

        /// <summary>
        /// Reset button click resets info and unchecks all data
        /// This also reloads the report_config.json file in case there are any changes
        /// //Allows for real-time update of report lists for medications and conditions
        /// </summary>
        public void ResetClick()
        {
            //Reset all values and gets the data from the report config file again in case there was a change
            //Allows for real-time update of report lists for medications and conditions
            AdditionalCommentsForReportBox = "";
            ConditionList.Clear();
            MedicationList.Clear();
            reportModel = jSONService?.GetReportModelFromFile(reportFilePath);
            if (reportModel == null)
            {
                return;
            }
            GetListOfMedicationsConditionsFromModel();
        }

        public void ExitClick()
        {
            var result = MessageBox.Show("You are about to exit without saving. Would you like to continue to exit?", "Report", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.OK)
            {
                TryClose();
            }
            else if(result == MessageBoxResult.Cancel)
            {
                return;
            }
        }
        #endregion

        #region Check RejectCode
        private void CheckRejectCode(APIReturnInfo info, string location)
        {
            if (info.RejectCode != 0)
            {
                MessageBox.Show("Error from Medtronic device. Could not report. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Warn("Error from medtronic device for Report. Location of Error: " + location + ". Reject Code: " + info.RejectCode + ". Reject Description: " + info.Descriptor);
            }
        }
        #endregion

        #region Gets Data from Report Config file
        /// <summary>
        /// Fills the MedicationList and ConditionList with the data from the reportModel.Medications and reportCofig.Symptoms variables, respectively.
        /// reportModel variable is set from GetReportJSONFile()
        /// </summary>
        public void GetListOfMedicationsConditionsFromModel()
        {
            //add to list of medications and conditions from the config file
            int index = 1;

            foreach (string medication in reportModel?.Medications)
            {
                MedicationList.Add(new MedicationCheckBoxClass { Medication = medication, Index = index, IsSelected = false });
                index++;
            }
            index = 1;
            foreach (string condition in reportModel?.Symptoms)
            {
                ConditionList.Add(new ConditionCheckBoxClass { Condition = condition, Index = index, IsSelected = false });
                index++;
            }
        }
        #endregion

        #region Binds Collections for MedicationList, ConditionList and Additional Comments
        /// <summary>
        /// The list of medications that are displayed to user
        /// </summary>
        public ObservableCollection<MedicationCheckBoxClass> MedicationList
        {
            get { return _medicationList; }
            set
            {
                _medicationList = value;
                NotifyOfPropertyChange(() => MedicationList);
            }
        }
        /// <summary>
        /// The list of conditions that are displayed to user
        /// </summary>
        public ObservableCollection<ConditionCheckBoxClass> ConditionList
        {
            get { return _conditionList; }
            set
            {
                _conditionList = value;
                NotifyOfPropertyChange(() => ConditionList);
            }
        }
        /// <summary>
        /// Text box for any additional information user wants to input
        /// </summary>
        public string AdditionalCommentsForReportBox
        {
            get { return _additionalCommentsForReportBox; }
            set
            {
                _additionalCommentsForReportBox = value;
                NotifyOfPropertyChange(() => AdditionalCommentsForReportBox);
            }
        }
        #endregion
    }

    #region ConditionCheckBoxClass, MedicationCheckBoxClass
    /// <summary>
    /// Classes for the conditions list. Allows them to be selectable
    /// </summary>
    public class ConditionCheckBoxClass
    {
        /// <summary>
        /// Condition name
        /// </summary>
        public string Condition { get; set; }
        /// <summary>
        /// What number in the list it is
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// If it is selected or not
        /// </summary>
        public bool IsSelected { get; set; }
    }
    /// <summary>
    /// Classes for the medications list. Allows them to be selectable
    /// </summary>
    public class MedicationCheckBoxClass
    {
        /// <summary>
        /// Medication name
        /// </summary>
        public string Medication { get; set; }
        /// <summary>
        /// What number in the list it is
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// If it is selected or not
        /// </summary>
        public bool IsSelected { get; set; }
    }
    #endregion
}
