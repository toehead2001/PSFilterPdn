﻿/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop-compatible filter host Effect plugin for Paint.NET
// http://psfilterpdn.codeplex.com/
//
// This software is provided under the Microsoft Public License:
//   Copyright (C) 2010-2015 Nicholas Hayes
// 
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace PSFilterLoad.PSApi
{
    [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl), System.Security.SuppressUnmanagedCodeSecurity]
    internal delegate void pluginEntryPoint(FilterSelector selector, IntPtr pluginParamBlock, ref IntPtr pluginData, ref short result);
 
#pragma warning disable 0649
   
    /// <summary>
    /// The class that encapsulates an Adobe® Photoshop® filter plugin
    /// </summary>
    [DataContract()]
    internal sealed class PluginData
    {
        [DataMember]
        public string fileName;
        [DataMember]
        public string entryPoint;
        [DataMember]
        public string category;
        [DataMember]
        public string title;
        [DataMember]
        public FilterCaseInfo[] filterInfo;
        /// <summary>
        /// The structure containing the plugin entrypoint
        /// </summary>
        public PIEntrypoint module;
        /// <summary>
        /// Used to run 32-bit plugins in 64-bit Paint.NET
        /// </summary>
        public bool runWith32BitShim;
        [DataMember]
        public AETEData aete;
        [DataMember]
        public string[] moduleEntryPoints;
    }
#pragma warning restore

    internal struct PIEntrypoint
    {
        /// <summary>
        /// The pointer to the dll module handle
        /// </summary>
        public SafeLibraryHandle dll;
        /// <summary>
        /// The entrypoint for the FilterParmBlock and AboutRecord
        /// </summary>
        public pluginEntryPoint entryPoint;
    }

}
