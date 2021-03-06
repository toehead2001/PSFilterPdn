// This file is from Manipulating colors in .NET - Part 1
// http://www.codeproject.com/Articles/19045/Manipulating-colors-in-NET-Part-1/
//
// It is distributed under the terms of the Code Project Open License
// http://www.codeproject.com/info/cpol10.aspx

using System;
using System.ComponentModel;

namespace Devcorp.Controls.Design
{
	/// <summary>
	/// Structure to define RGB.
	/// </summary>
	internal struct RGB
	{
		/// <summary>
		/// Gets an empty RGB structure;
		/// </summary>
		public static readonly RGB Empty = new RGB();

		#region Fields
		private int red;
		private int green;
		private int blue;

		#endregion

		#region Operators
		public static bool operator ==(RGB item1, RGB item2)
		{
			return (
				item1.Red == item2.Red 
				&& item1.Green == item2.Green 
				&& item1.Blue == item2.Blue
				);
		}

		public static bool operator !=(RGB item1, RGB item2)
		{
			return (
				item1.Red != item2.Red 
				|| item1.Green != item2.Green 
				|| item1.Blue != item2.Blue
				);
		}

		#endregion

		#region Accessors
		[Description("Red component."),]
		public int Red
		{
			get
			{
				return red;
			}
		}

		[Description("Green component."),]
		public int Green
		{
			get
			{
				return green;
			}
		}

		[Description("Blue component."),]
		public int Blue
		{
			get
			{
				return blue;
			}
		}
		#endregion

		public RGB(int R, int G, int B) 
		{
			red = (R>255)? 255 : ((R<0)?0 : R);
			green = (G>255)? 255 : ((G<0)?0 : G);
			blue = (B>255)? 255 : ((B<0)?0 : B);
		}

		#region Methods
		public override bool Equals(Object obj) 
		{
			if(obj==null || GetType()!=obj.GetType()) return false;

			return (this == (RGB)obj);
		}

		public override int GetHashCode() 
		{
			return Red.GetHashCode() ^ Green.GetHashCode() ^ Blue.GetHashCode();
		}

		#endregion
	} 
}
