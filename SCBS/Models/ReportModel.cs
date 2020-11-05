using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    /// <summary>
    /// Model for Report Config JSON File
    /// This class is used to convert the json file into class data
    /// </summary>
    public class ReportModel
    {
        /// <summary>
        /// Medications
        /// </summary>
        public List<string> Medications { get; set; }
        /// <summary>
        /// Symptoms
        /// </summary>
        public List<string> Symptoms { get; set; }
    }
}
