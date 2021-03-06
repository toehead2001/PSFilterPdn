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

/* Adapted from PITypes.h
 * Copyright (c) 1992-1998, Adobe Systems Incorporated.
 * All rights reserved.
*/

using System.Runtime.InteropServices;

namespace PSFilterLoad.PSApi
{
#pragma warning disable 0659
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2218:OverrideGetHashCodeOnOverridingEquals"), StructLayout(LayoutKind.Sequential)]
    internal struct Rect16
    {
        public short top;
        public short left;
        public short bottom;
        public short right;

        public override bool Equals(object obj)
        {
            if (obj is Rect16)
            {
                Rect16 rect = (Rect16)obj;
                return Equals(rect);
            }
            else
            {
                return false;
            }

        }
        public bool Equals(Rect16 rect)
        {
            return (this.left == rect.left && this.top == rect.top && this.right == rect.right && this.bottom == rect.bottom);
        }

#if DEBUG
        public override string ToString()
        {
            return ("Top=" + this.top.ToString() + ",Bottom=" + this.bottom.ToString() + ",Left=" + this.left.ToString() + ",Right=" + this.right.ToString()); 
        }
#endif
        public static readonly Rect16 Empty = new Rect16();

    }
#pragma warning restore 0659
}
