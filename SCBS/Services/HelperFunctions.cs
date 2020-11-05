using Medtronic.NeuroStim.Olympus.DataTypes.DeviceManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
