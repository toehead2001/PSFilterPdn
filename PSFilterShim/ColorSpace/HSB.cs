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
	/// Structure to define HSB.
	/// </summary>
	internal struct HSB
	{
		/// <summary>
		/// Gets an empty HSB structure;
		/// </summary>
		public static readonly HSB Empty = new HSB();

		#region Fields
		private double hue;
		private double saturation;
		private double brightness;
		#endregion

		#region Operators
		public static bool operator ==(HSB item1, HSB item2)
		{
			return (
				item1.Hue == item2.Hue 
				&& item1.Saturation == item2.Saturation 
				&& item1.Brightness == item2.Brightness
				);
		}

		public static bool operator !=(HSB item1, HSB item2)
		{
			return (
				item1.Hue != item2.Hue 
				|| item1.Saturation != item2.Saturation 
				|| item1.Brightness != item2.Brightness
				);
		}

		#endregion

		#region Accessors
		/// <summary>
		/// Gets or sets the hue component.
		/// </summary>
		[Description("Hue component"),]
		public double Hue 
		{ 
			get
			{
				return hue;
			} 
		} 

		/// <summary>
		/// Gets or sets saturation component.
		/// </summary>
		[Description("Saturation component"),]
		public double Saturation 
		{ 
			get
			{
				return saturation;
			} 
		} 

		/// <summary>
		/// Gets or sets the brightness component.
		/// </summary>
		[Description("Brightness component"),]
		public double Brightness 
		{ 
			get
			{
				return brightness;
			} 
		} 
		#endregion

		/// <summary>
		/// Creates an instance of a HSB structure.
		/// </summary>
		/// <param name="h">Hue value.</param>
		/// <param name="s">Saturation value.</param>
		/// <param name="b">Brightness value.</param>
		public HSB(double h, double s, double b) 
		{
			hue = (h>360)? 360 : ((h<0)?0:h); 
			saturation = (s>1)? 1 : ((s<0)?0:s);
			brightness = (b>1)? 1 : ((b<0)?0:b);
		}

		#region Methods
		public override bool Equals(Object obj) 
		{
			if(obj==null || GetType()!=obj.GetType()) return false;

			return (this == (HSB)obj);
		}

		public override int GetHashCode() 
		{
			return Hue.GetHashCode() ^ Saturation.GetHashCode() ^ Brightness.GetHashCode();
		}

		#endregion
	}
}
