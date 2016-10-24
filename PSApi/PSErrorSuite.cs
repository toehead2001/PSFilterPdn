﻿/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop-compatible filter host Effect plugin for Paint.NET
// http://psfilterpdn.codeplex.com/
//
// This software is provided under the Microsoft Public License:
//   Copyright (C) 2010-2016 Nicholas Hayes
// 
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

/* Adapted from PIErrorSuite.h
 * Copyright (c) 1992-1998, Adobe Systems Incorporated.
 * All rights reserved.
*/

using System;
using System.Runtime.InteropServices;

namespace PSFilterLoad.PSApi
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ErrorSuiteSetErrorFromPString(IntPtr str);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ErrorSuiteSetErrorFromCString(IntPtr str);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ErrorSuiteSetErrorFromZString(IntPtr str);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PSErrorSuite1
    {
        public IntPtr SetErrorFromPString;
        public IntPtr SetErrorFromCString;
        public IntPtr SetErrorFromZString;
    }
}