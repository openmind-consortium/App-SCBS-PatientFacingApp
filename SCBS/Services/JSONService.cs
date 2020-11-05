using Caliburn.Micro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using SCBS.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SCBS.Services
{
    /// <summary>
    /// Gets and writes from the json config files to and from the respective Models
    /// </summary>
    public class JSONService
    {
        private ILog _log;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_log">Caliburn Micro Logger</param>
        public JSONService(ILog _log)
        {
            this._log = _log;
        }
        
        #region Get config files and convert to Model
        /// <summary>
        /// Gets the file from the filepath, validates the file, and converts it to the sense model
        /// </summary>
        /// <param name="filePath">File path for the sense_config.json file to be used to convert</param>
        /// <returns>SenseModel if successful or null if unsuccessful</returns>
        public SenseModel GetSenseModelFromFile(string filePath)
        {
            SenseModel model = null;
            string json = null;
            try
            {
                //read sense config file into string
                using (StreamReader sr = new StreamReader(filePath))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("The sense config file could not be read from the file. Please check that it exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                return model;
            }
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("Sense JSON file is empty. Please check that the sense config is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Warn("Sense JSON file is empty after loading file.");
                return model;
            }
            else
            {
                SchemaModel schemaModel = new SchemaModel();
                if (ValidateJSON(json, schemaModel.GetSenseSchema()))
                {
                    //if correct json format, write it into SenseModel
                    try
                    {
                        model = JsonConvert.DeserializeObject<SenseModel>(json);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Could not convert sense config file. Please be sure that sense config file is correct.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _log.Error(e);
                        return model;
                    }
                }
                else
                {
                    MessageBox.Show("Could not validate sense config file. Please be sure that sense config file is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _log.Warn("Could not validate sense config file.");
                    return model;
                }
            }
            return model;
        }

        /// <summary>
        /// Gets the file from the filepath, validates the file, and converts it to the adaptive model
        /// </summary>
        /// <param name="filePath">File path for the adaptive_config.json file to be used to convert</param>
        /// <returns>AdaptiveModel if successful or null if unsuccessful</returns>
        public AdaptiveModel GetAdaptiveModelFromFile(string filePath)
        {
            AdaptiveModel model = null;
            string json = null;
            try
            {
                //read sense config file into string
                using (StreamReader sr = new StreamReader(filePath))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("The adaptive config file could not be read from the file. Please check that it exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                return model;
            }
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("Adaptive JSON file is empty. Please check that the adaptive config is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Warn("Adaptive JSON file is empty after loading file.");
                return model;
            }
            else
            {
                SchemaModel schemaModel = new SchemaModel();
                if (ValidateJSON(json, schemaModel.GetAdaptiveSchema()))
                {
                    //if correct json format, write it into SenseModel
                    try
                    {
                        model = JsonConvert.DeserializeObject<AdaptiveModel>(json);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Could not convert adaptive config file. Please be sure that adaptive config file is correct.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _log.Error(e);
                        return model;
                    }
                }
                else
                {
                    MessageBox.Show("Could not validate adaptive config file. Please be sure that adaptive config file is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _log.Warn("Could not validate adaptive config file.");
                    return model;
                }
            }
            return model;
        }

        /// <summary>
        /// Gets the file from the filepath, validates the file, and converts it to the report model
        /// </summary>
        /// <param name="filePath">File path for the report_config.json file to be used to convert</param>
        /// <returns>ReportModel if successful or null if unsuccessful</returns>
        public ReportModel GetReportModelFromFile(string filePath)
        {
            ReportModel model = null;
            string json = null;
            try
            {
                //read report config file into string
                using (StreamReader sr = new StreamReader(filePath))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("The report config file could not be read from the file. Please check that it exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                return model;
            }
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("Report JSON file is empty. Please check that the report config is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Warn("Report JSON file is empty after loading file.");
                return model;
            }
            else
            {
                SchemaModel schemaModel = new SchemaModel();
                if (ValidateJSON(json, schemaModel.GetReportSchema()))
                {
                    //if correct json format, write it into reportModel
                    try
                    {
                        model = JsonConvert.DeserializeObject<ReportModel>(json);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Could not convert report config file. Please be sure that report config file is correct.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _log.Error(e);
                        return model;
                    }
                }
                else
                {
                    MessageBox.Show("Could not validate report config file. Please be sure that report config file is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _log.Warn("Could not validate report config file.");
                    return model;
                }
            }
            return model;
        }

        /// <summary>
        /// Gets the file from the filepath, validates the file, and converts it to the application model
        /// </summary>
        /// <param name="filePath">File path for the application_config.json file to be used to convert</param>
        /// <returns>ApplicationModel if successful or null if unsuccessful</returns>
        public AppModel GetApplicationModelFromFile(string filePath)
        {
            AppModel model = null;
            string json = null;
            try
            {
                //read application config file into string
                using (StreamReader sr = new StreamReader(filePath))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("The application config file could not be read from the file. Please check that it exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                return model;
            }
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("Application JSON file is empty. Please check that the application config is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Warn("Application JSON file is empty after loading file.");
                return model;
            }
            else
            {
                SchemaModel schemaModel = new SchemaModel();
                if (ValidateJSON(json, schemaModel.GetApplicationSchema()))
                {
                    //if correct json format, write it into applicationModel
                    try
                    {
                        model = JsonConvert.DeserializeObject<AppModel>(json);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Could not convert application config file. Please be sure that application config file is correct.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _log.Error(e);
                        return model;
                    }
                }
                else
                {
                    MessageBox.Show("Could not validate application config file. Please be sure that application config file is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _log.Warn("Could not validate application config file.");
                    return model;
                }
            }
            return model;
        }

        /// <summary>
        /// Gets the file from the filepath, validates the file, and converts it to the master switch model
        /// </summary>
        /// <param name="filePath">File path for the master switch_config.json file to be used to convert</param>
        /// <returns>MasterSwitchModel if successful or null if unsuccessful</returns>
        public MasterSwitchModel GetMasterSwitchModelFromFile(string filePath)
        {
            MasterSwitchModel model = null;
            string json = null;
            try
            {
                //read master switch config file into string
                using (StreamReader sr = new StreamReader(filePath))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("The master switch config file could not be read from the file. Please check that it exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                return model;
            }
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("Master switch JSON file is empty. Please check that the master switch config is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Warn("Master switch JSON file is empty after loading file.");
                return model;
            }
            else
            {
                SchemaModel schemaModel = new SchemaModel();
                if (ValidateJSON(json, schemaModel.GetMasterSwitchSchema()))
                {
                    //if correct json format, write it into master switchModel
                    try
                    {
                        model = JsonConvert.DeserializeObject<MasterSwitchModel>(json);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Could not convert master switch config file. Please be sure that master switch config file is correct.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _log.Error(e);
                        return model;
                    }
                }
                else
                {
                    MessageBox.Show("Could not validate master switch config file. Please be sure that master switch config file is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _log.Warn("Could not validate master switch config file.");
                    return model;
                }
            }
            return model;
        }

        /// <summary>
        /// Gets the montage model from the montage config
        /// </summary>
        /// <param name="filePath">File path to the montage config file</param>
        /// <returns>Montage model if success or null if unsuccessful</returns>
        public MontageModel GetMontageModelFromFile(string filePath)
        {
            MontageModel model = null;
            string json = null;
            try
            {
                //read MontageModel config file into string
                using (StreamReader sr = new StreamReader(filePath))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("The montage config file could not be read from the file. Please check that it exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                return model;
            }
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("Montage JSON file is empty. Please check that the Montage config is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Warn("Montage JSON file is empty after loading file.");
                return model;
            }
            else
            {
                SchemaModel schemaModel = new SchemaModel();
                if (ValidateJSON(json, schemaModel.GetMontageSchema()))
                {
                    //if correct json format, write it into master switchModel
                    try
                    {
                        model = JsonConvert.DeserializeObject<MontageModel>(json);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Could not convert montage config file. Please be sure that montage config file is correct.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _log.Error(e);
                        return model;
                    }
                }
                else
                {
                    MessageBox.Show("Could not validate montage config file. Please be sure that montage config file is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _log.Warn("Could not validate montage config file.");
                    return model;
                }
            }
            return model;
        }
        /// <summary>
        /// Gets the stim sweep model from the stim sweep config
        /// </summary>
        /// <param name="filePath">File path to the stim sweep config file</param>
        /// <returns>StimSweep model if success or null if unsuccessful</returns>
        public StimSweepModel GetStimSweepModelFromFile(string filePath)
        {
            StimSweepModel model = null;
            string json = null;
            try
            {
                //read StimSweepModel config file into string
                using (StreamReader sr = new StreamReader(filePath))
                {
                    json = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("The StimSweep config file could not be read from the file. Please check that it exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _log.Error(e);
                return model;
            }
            if (string.IsNullOrEmpty(json))
            {
                MessageBox.Show("StimSweep JSON file is empty. Please check that the StimSweep config is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                _log.Warn("StimSweep JSON file is empty after loading file.");
                return model;
            }
            else
            {
                SchemaModel schemaModel = new SchemaModel();
                if (ValidateJSON(json, schemaModel.GetStimSweepSchema()))
                {
                    //if correct json format, write it into master switchModel
                    try
                    {
                        model = JsonConvert.DeserializeObject<StimSweepModel>(json);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("Could not convert StimSweep config file. Please be sure that StimSweep config file is correct.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        _log.Error(e);
                        return model;
                    }
                }
                else
                {
                    MessageBox.Show("Could not validate StimSweep config file. Please be sure that StimSweep config file is correct.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _log.Warn("Could not validate StimSweep config file.");
                    return model;
                }
            }
            return model;
        }
        #endregion

        #region Write Model back to Config Files
        /// <summary>
        /// Writes the Model class back to the json config file
        /// </summary>
        /// <param name="model">Sense, Application, Adaptive, or Master Switch Models may be used here</param>
        /// <param name="filepath">The path where the file is to be written.  The directory must exist in order for success</param>
        /// <returns>True if successful or false if unsuccessful</returns>
        public bool WriteModelBackToConfigFile(object model, string filepath)
        {
            bool success = false;
            try
            {
                //Check if path exists and if not then create the path
                if (!CheckIfDirectoryExistsOtherwiseCreateIt(filepath))
                {
                    MessageBox.Show("Directory could not be created to write file. Please check path and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return success;
                }
                using (StreamWriter file = File.CreateText(filepath))
                {
                    //write the file to path and set success
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, model);
                    success = true;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            return success;
        }
        #endregion

        #region Helper Methods for checking if path exists or creating it, Validating json
        /// <summary>
        /// Validates that the json matches the schema provided
        /// </summary>
        /// <param name="jsonToValidate">json string from the config file</param>
        /// <param name="schema">schema text from SchemaModel.cs that matches the appropriate jsonToValidate string structure</param>
        /// <returns>true is valid and false if not</returns>
        public bool ValidateJSON(string jsonToValidate, string schema)
        {
            //Set to false as default
            bool isValid = false;
            //Messages for anything wrong with json validation. Not used 
            IList<string> messages;
            try
            {
                JSchema jsonSchema = JSchema.Parse(schema);
                Newtonsoft.Json.Linq.JObject person = JObject.Parse(jsonToValidate);
                //Only set to true if json is valid
                isValid = person.IsValid(jsonSchema, out messages);
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            return isValid;
        }

        /// <summary>
        /// Checks to see if the path already. If it does not, it will write the directory path for placing files into
        /// </summary>
        /// <param name="path">This is the final path where the files will be written and checked to make sure it is valid or else writes it</param>
        public bool CheckIfDirectoryExistsOtherwiseCreateIt(string path)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
            }
            catch (Exception e)
            {
                _log.Error(e);
                return false;
            }
            return true;
        }
        #endregion
    }
}
