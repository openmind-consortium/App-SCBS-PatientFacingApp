using Caliburn.Micro;
using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using Medtronic.SummitAPI.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SCBS.Services
{
    class HelperFunctions
    {

        public bool CheckGroupIsCorrectFormat(string group)
        {
            if (String.IsNullOrEmpty(group))
            {
                return false;
            }
            return group == "A" || group == "B" || group == "C" || group == "D";
        }
        public ActiveGroup ConvertStimModelGroupToAPIGroup(string groupValue)
        {
            switch (groupValue)
            {
                case "A":
                    return ActiveGroup.Group0;
                case "B":
                    return ActiveGroup.Group1;
                case "C":
                    return ActiveGroup.Group2;
                case "D":
                    return ActiveGroup.Group3;
            }
            return ActiveGroup.Group0;
        }

        public static bool WriteEventLog(SummitSystem localSummit, string successLogMessage, string successLogMessageSubType, string unsuccessfulMessageBoxMessage, ILog _log)
        {
            int counter = 5;
            APIReturnInfo bufferReturnInfo;
            try
            {
                do
                {
                    bufferReturnInfo = localSummit.LogCustomEvent(DateTime.Now, DateTime.Now, successLogMessage, successLogMessageSubType);
                    counter--;
                } while (bufferReturnInfo.RejectCode != 0 && counter > 0);
                if (counter == 0)
                {
                    ShowMessageBox.Show(unsuccessfulMessageBoxMessage, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
                ShowMessageBox.Show("Error calling summit system while logging event.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return true;
        }
    }
}
