﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCBS.Models
{
    class PowerBandModel
    {
        /// <summary>
        /// Lower index for band 0
        /// </summary>
        public ushort lowerIndexBand0 { get; set; }
        /// <summary>
        /// Upper index for band 0
        /// </summary>
        public ushort upperIndexBand0 { get; set; }
        /// <summary>
        /// Lower index for band 1
        /// </summary>
        public ushort lowerIndexBand1 { get; set; }
        /// <summary>
        /// Upper index for band 1
        /// </summary>
        public ushort upperIndexBand1 { get; set; }
        /// <summary>
        /// Actual value in Hz for lower power band 0
        /// </summary>
        public double lowerActualValueHzBand0 { get; set; }
        /// <summary>
        /// Actual value in Hz for upper power band 0
        /// </summary>
        public double UpperActualValueHzBand0 { get; set; }
        /// <summary>
        /// Actual value in Hz for lower power band 1
        /// </summary>
        public double lowerActualValueHzBand1 { get; set; }
        /// <summary>
        /// Actual value in Hz for upper power band 1
        /// </summary>
        public double upperActualValueHzBand1 { get; set; }

    }
}
