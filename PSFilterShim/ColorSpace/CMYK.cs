// This file is from Manipulating colors in .NET - Part 1
// http://www.codeproject.com/Articles/19045/Manipulating-colors-in-NET-Part-1/
//
// It is distributed under the terms of the Code Project Open License
// http://www.codeproject.com/info/cpol10.aspx

using System;

namespace Devcorp.Controls.Design
{
	/// <summary>
	/// Structure to define CMYK.
	/// </summary>
	internal struct CMYK 
	{
		/// <summary>
		/// Gets an empty CMYK structure;
		/// </summary>
		public readonly static CMYK Empty = new CMYK();

		#region Fields
		private double c; 
		private double m; 
		private double y; 
		private double k;
		#endregion

		#region Operators
		public static bool operator ==(CMYK item1, CMYK item2)
		{
			return (
				item1.Cyan == item2.Cyan 
				&& item1.Magenta == item2.Magenta 
				&& item1.Yellow == item2.Yellow
				&& item1.Black == item2.Black
				);
		}

		public static bool operator !=(CMYK item1, CMYK item2)
		{
			return (
				item1.Cyan != item2.Cyan 
				|| item1.Magenta != item2.Magenta 
				|| item1.Yellow != item2.Yellow
				|| item1.Black != item2.Black
				);
		}


		#endregion

		#region Accessors
		public double Cyan
		{ 
			get
			{
				return c;
			} 

		} 

		public double Magenta
		{ 
			get
			{
				return m;
			} 
		} 

		public double Yellow
		{ 
			get
			{
				return y;
			} 

		} 

		public double Black 
		{ 
			get
			{
				return k;
			} 
		} 
		#endregion

		/// <summary>
		/// Creates an instance of a CMYK structure.
		/// </summary>
		public CMYK(double c, double m, double y, double k) 
		{
			this.c = c;
			this.m = m;
			this.y = y;
			this.k = k;
		}

		#region Methods
		public override bool Equals(Object obj) 
		{
			if(obj==null || GetType()!=obj.GetType()) return false;

			return (this == (CMYK)obj);
		}

		public override int GetHashCode() 
		{
			return Cyan.GetHashCode() ^ Magenta.GetHashCode() ^ Yellow.GetHashCode() ^ Black.GetHashCode();
		}

		#endregion
	} 
}
