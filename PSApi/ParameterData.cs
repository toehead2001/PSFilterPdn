﻿/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop-compatible filter host Effect plugin for Paint.NET
// https://github.com/0xC0000054/PSFilterPdn
//
// This software is provided under the Microsoft Public License:
//   Copyright (C) 2010-2017 Nicholas Hayes
// 
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace PSFilterLoad.PSApi
{
    /// <summary>
    /// The class that holds the saved filter parameter and scripting data.
    /// </summary>
    [Serializable]
    public sealed class ParameterData 
    {
        private GlobalParameters globalParameters;
        private Dictionary<uint, AETEValue> aeteDictonary;

        public GlobalParameters GlobalParameters
        {
            get 
            {
                return globalParameters;
            }
        }

        public Dictionary<uint, AETEValue> AETEDictionary
        {
            get
            {
                return aeteDictonary;
            }
        }

        public ParameterData(GlobalParameters globals, Dictionary<uint, AETEValue> aete)
        {
            this.globalParameters = globals;

            if (aete != null)
            {
                this.aeteDictonary = new Dictionary<uint, AETEValue>(aete);
            }
            else
            {
                this.aeteDictonary = null;
            }
        }
       
    }
}
