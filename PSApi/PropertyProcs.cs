/////////////////////////////////////////////////////////////////////////////////
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

/* Adapted from PIGeneral.h
 * Copyright (c) 1992-1998, Adobe Systems Incorporated.
 * All rights reserved.
*/

using System;
using System.Runtime.InteropServices;

namespace PSFilterLoad.PSApi
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate short GetPropertyProc(uint signature, uint key, int index, ref IntPtr simpleProperty, ref IntPtr complexProperty);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate short SetPropertyProc(uint signature, uint key, int index, IntPtr simpleProperty, IntPtr complexProperty);


    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1049:TypesThatOwnNativeResourcesShouldBeDisposable"), StructLayout(LayoutKind.Sequential)]
    internal struct PropertyProcs
    {
        public short propertyProcsVersion;
        public short numPropertyProcs;
        public IntPtr getPropertyProc;
        public IntPtr setPropertyProc;
    } 
}
