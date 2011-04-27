﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using PSFilterPdn.Properties;
using System.Globalization;

namespace PSFilterLoad.PSApi
{
	
	internal sealed class LoadPsFilter : IDisposable
	{

		#region EnumRes
#if DEBUG
		private static bool IS_INTRESOURCE(IntPtr value)
		{
			if (((uint)value) > ushort.MaxValue)
			{
				return false;
			}
			return true;
		}
		private static string GET_RESOURCE_NAME(IntPtr value)
		{
			if (IS_INTRESOURCE(value))
				return value.ToString();
			return Marshal.PtrToStringUni(value);
		} 
#endif

		private static string StringFromPString(IntPtr PString)
		{
			if (PString == IntPtr.Zero)
			{
				return string.Empty;
			}
			int length = (int)Marshal.ReadByte(PString);
			PString = new IntPtr(PString.ToInt64() + 1L);
			char[] data = new char[length];
			for (int i = 0; i < length; i++)
			{
				data[i] = (char)Marshal.ReadByte(PString, i);
			}

			return new string(data).Trim(new char[] { ' ', '\0' });
		}

		private static bool queryPlugin;
		private static bool EnumRes(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam)
		{
			PluginData enumData = null;
			GCHandle gch = GCHandle.FromIntPtr(lParam);
			if (!queryPlugin)
			{
				enumData = (PluginData)gch.Target;
			}
			else
			{
				enumData = new PluginData() { fileName = ((PluginData)gch.Target).fileName };
			}

			IntPtr hRes = NativeMethods.FindResource(hModule, lpszName, lpszType);
			if (hRes == IntPtr.Zero)
			{
#if DEBUG
				Debug.WriteLine(string.Format("FindResource failed for {0} in {1}", GET_RESOURCE_NAME(lpszName), enumData.fileName));
#endif    
				return true;
			}

			IntPtr loadRes = NativeMethods.LoadResource(hModule, hRes);
			if (loadRes == IntPtr.Zero)
			{
#if DEBUG
				Debug.WriteLine(string.Format("LoadResource failed for {0} in {1}", GET_RESOURCE_NAME(lpszName), enumData.fileName)); 
#endif
				return true;
			}

			IntPtr lockRes = NativeMethods.LockResource(loadRes);
			if (lockRes == IntPtr.Zero)
			{
#if DEBUG
				Debug.WriteLine(string.Format("LockResource failed for {0} in {1}", GET_RESOURCE_NAME(lpszName), enumData.fileName)); 
#endif

				return true;
			}

#if DEBUG
			short fb = Marshal.ReadInt16(lockRes); // PiPL Resources always start with 1, this seems to be Photoshop's signature
#endif			
            int version = Marshal.ReadInt32(lockRes, 2);

            if (version != 0)
            {
                enumErrorList.Add(new FilterLoadException(string.Format(CultureInfo.CurrentUICulture, Resources.InvalidPiPLVersionFormat, enumData.fileName, version)));
            }

            int count = Marshal.ReadInt32(lockRes, 6);

            long pos = (lockRes.ToInt64() + 10L);

            IntPtr propPtr = new IntPtr(pos);

            long dataOfs = Marshal.OffsetOf(typeof(PIProperty), "propertyData").ToInt64();

            for (int i = 0; i < count; i++)
            {
                PIProperty pipp = (PIProperty)Marshal.PtrToStructure(propPtr, typeof(PIProperty));
                PIPropertyID propKey = (PIPropertyID)pipp.propertyKey;
#if DEBUG
                if ((dbgFlags & DebugFlags.PiPL) == DebugFlags.PiPL)
                {
                    Debug.WriteLine(string.Format("prop = {0}", propKey.ToString("X")));
                    Debug.WriteLine(PropToString(pipp.propertyKey));
                }
#endif
                if (propKey == PIPropertyID.PIKindProperty)
                {
                    if (PropToString((uint)pipp.propertyData.ToInt64()) != "8BFM")
                    {
                        enumErrorList.Add(new FilterLoadException(string.Format(CultureInfo.CurrentUICulture, Resources.InvalidPhotoshopFilterFormat, enumData.fileName)));
                    }
                }
                else if ((IntPtr.Size == 8 && propKey == PIPropertyID.PIWin64X86CodeProperty) || propKey == PIPropertyID.PIWin32X86CodeProperty) // the entrypoint for the current platform, this filters out incomptable processors archatectures
                {
                    String ep = Marshal.PtrToStringAnsi(new IntPtr(propPtr.ToInt64() + dataOfs), pipp.propertyLength).TrimEnd('\0');
                    enumData.entryPoint = ep;
                    // If it is a 32-bit plugin on a 64-bit OS run it with the 32-bit shim.
                    enumData.runWith32BitShim = (IntPtr.Size == 8 && propKey == PIPropertyID.PIWin32X86CodeProperty);
                }
                else if (propKey == PIPropertyID.PIVersionProperty)
                {
                    int fltrversion = Marshal.ReadInt32(new IntPtr(propPtr.ToInt64() + dataOfs));
                    if (HiWord(fltrversion) > PSConstants.latestFilterVersion ||
                        (HiWord(fltrversion) == PSConstants.latestFilterVersion && LoWord(fltrversion) > PSConstants.latestFilterSubVersion))
                    {
                        enumErrorList.Add(new FilterLoadException(string.Format(CultureInfo.CurrentUICulture, Resources.UnsupportedInterfaceVersionFormat, new object[] { enumData.fileName, HiWord(fltrversion).ToString(CultureInfo.CurrentCulture), LoWord(fltrversion).ToString(CultureInfo.CurrentCulture), PSConstants.latestFilterVersion.ToString(CultureInfo.CurrentCulture), PSConstants.latestFilterSubVersion.ToString(CultureInfo.CurrentCulture) })));
                    }
                }
                else if (propKey == PIPropertyID.PIImageModesProperty)
                {
                    byte[] bytes = BitConverter.GetBytes(pipp.propertyData.ToInt64());

                    bool rgb = ((bytes[0] & PSConstants.flagSupportsRGBColor) == PSConstants.flagSupportsRGBColor);

                    if (!rgb)
                    {
                        enumErrorList.Add(new FilterLoadException(string.Format(CultureInfo.CurrentUICulture, Resources.RGBColorUnsupportedModeFormat, enumData.fileName)));
                    }
                }
                else if (propKey == PIPropertyID.PICategoryProperty)
                {
                    enumData.category = StringFromPString(new IntPtr(propPtr.ToInt64() + dataOfs));
                }
                else if (propKey == PIPropertyID.PINameProperty)
                {
                    enumData.title = StringFromPString(new IntPtr(propPtr.ToInt64() + dataOfs));
                }
                else if (propKey == PIPropertyID.PIFilterCaseInfoProperty)
                {
                    IntPtr ptr = new IntPtr((propPtr.ToInt64() + dataOfs));

                    enumData.filterInfo = new FilterCaseInfo[7];
                    for (int j = 0; j < 7; j++)
                    {
                        enumData.filterInfo[j] = (FilterCaseInfo)Marshal.PtrToStructure(ptr, typeof(FilterCaseInfo));
                        ptr = new IntPtr(ptr.ToInt64() + (long)Marshal.SizeOf(typeof(FilterCaseInfo)));
                    }

                }

                int propertyDataPaddedLength = (pipp.propertyLength + 3) & ~3;
#if DEBUG
                if ((dbgFlags & DebugFlags.PiPL) == DebugFlags.PiPL)
                {
                    Debug.WriteLine(string.Format("i = {0}, propPtr = {1}", i.ToString(), ((long)propPtr).ToString()));
                }
#endif
                pos += (long)(16 + propertyDataPaddedLength);
                propPtr = new IntPtr(pos);
            }

			
			if (queryPlugin)
			{
				AddFoundPluginData(enumData); // add each plugin found in the file to the query list
			}
            else
			{
				gch.Target = enumData; // this is used for the LoadFilter function
			}

			return true;
		}
		private static int LoWord(long dwValue)
		{
			return (int)(dwValue & 0xFFFF);
		}

		private static int HiWord(long dwValue)
		{
			return (int)(dwValue >> 16) & 0xFFFF;
		}
		private static string PropToString(uint prop)
		{
			byte[] bytes = BitConverter.GetBytes(prop);
			return new string(new char[] { (char)bytes[3], (char)bytes[2], (char)bytes[1], (char)bytes[0] });
		}
		

		#endregion

#if DEBUG
		private static DebugFlags dbgFlags;
		static void Ping(DebugFlags dbg, string message)
		{
			if ((dbgFlags & dbg) != 0)
			{
				StackFrame sf = new StackFrame(1);
				string name = sf.GetMethod().Name;
				Debug.WriteLine(string.Format("Function: {0} {1}\r\n", name, ", " + message));
			}
		} 
#endif
	   
		static bool RectNonEmpty(Rect16 rect)
		{
			return (rect.left < rect.right && rect.top < rect.bottom);
		}

		struct PSHandle
		{
			public IntPtr pointer;
			public int size;
		}

		#region CallbackDelegates
	   
		// AdvanceState
		static AdvanceStateProc advanceProc;
		// BufferProcs
		static AllocateBufferProc allocProc;
		static FreeBufferProc freeProc;
		static LockBufferProc lockProc;
		static UnlockBufferProc unlockProc;
		static BufferSpaceProc spaceProc;
		// MiscCallbacks
		static ColorServicesProc colorProc;
		static DisplayPixelsProc displayPixelsProc;
		static HostProcs hostProc;
		static ProcessEventProc processEventProc;
		static ProgressProc progressProc;
		static TestAbortProc abortProc;
		// HandleProcs 
		static NewPIHandleProc handleNewProc;
		static DisposePIHandleProc handleDisposeProc;
		static GetPIHandleSizeProc handleGetSizeProc;
		static SetPIHandleSizeProc handleSetSizeProc;
		static LockPIHandleProc handleLockProc;
		static UnlockPIHandleProc handleUnlockProc;
		static RecoverSpaceProc handleRecoverSpaceProc;
		// ImageServicesProc
#if PSSDK_3_0_4
		static PIResampleProc resample1DProc;
		static PIResampleProc resample2DProc; 
#endif
		// PropertyProcs
		static GetPropertyProc getPropertyProc;
#if PSSDK_3_0_4
		static SetPropertyProc setPropertyProc;
#endif		
		// ResourceProcs
		static CountPIResourcesProc countResourceProc;
		static GetPIResourceProc getResourceProc;
		static DeletePIResourceProc deleteResourceProc;
		static AddPIResourceProc addResourceProc;
		#endregion

		static Dictionary<long, PSHandle> handles = null; 

	   // static PluginData enumData;  
		static FilterRecord filterRecord;
		static GCHandle filterRecordPtr;

		static PlatformData platformData;
		static BufferProcs buffer_proc;
		static HandleProcs handle_procs;

#if PSSDK_3_0_4

		static ImageServicesProcs image_services_procs;
		static PropertyProcs property_procs; 
#endif
		static ResourceProcs resource_procs;
		/// <summary>
		/// The GCHandle to the PlatformData structure
		/// </summary>
		static GCHandle platFormDataPtr;

		static GCHandle buffer_procPtr;

		static GCHandle handle_procPtr;
#if PSSDK_3_0_4
		static GCHandle image_services_procsPtr;
		static GCHandle property_procsPtr; 
#endif
		static GCHandle resource_procsPtr;

		public Bitmap Dest
		{ 
			get
			{
				return dest;
			}
		}

		/// <summary>
		/// The filter progress callback.
		/// </summary>
		public ProgressProc ProgressFunc
		{
			set
			{
				if (value == null)
					throw new ArgumentNullException("value", "value is null.");
				progressFunc = value;
			}

		}

		static ProgressProc progressFunc;

		static Bitmap source = null;
		static Bitmap dest = null;
#if DEBUG
        static PluginPhase phase;
#endif
		static IntPtr data;
		static short result;

		const int bpp = 4;

		static abort abortFunc;
 
		public abort AbortFunc
		{
			set
			{
				abortFunc = value;
			}
		}

		static string errorMessage;

		public string ErrorMessage
		{
			get 
			{
				return errorMessage;
			}
		}

		static short filterCase;
	   
		static float dpiX;
		static float dpiY;
        static Region selectedRegion;

		/// <summary>
		/// Loads and runs Photoshop Filters
		/// </summary>
		/// <param name="eep">The EffectEnvironmentParameters of the plugin</param>
		/// <param name="owner">The handle of the parent window</param>
		/// <exception cref="System.ArgumentNullException">The EffectEnvironmentParameters are null.</exception>
		/// <exception cref="PSFilterLoad.PSApi.ImageSizeTooLargeException">The source image is larger than 32000 pixels in width and/or height.</exception>
		public LoadPsFilter(PaintDotNet.Effects.EffectEnvironmentParameters eep, IntPtr owner)
		{
			if (eep == null)
				throw new ArgumentNullException("eep", "eep is null.");

			if (eep.SourceSurface.Width > 32000 || eep.SourceSurface.Height > 32000)
			{
				if (eep.SourceSurface.Width > 32000 || eep.SourceSurface.Height > 32000)
				{
					string message = string.Empty;
					if (eep.SourceSurface.Width > 32000 && eep.SourceSurface.Height > 32000)
					{
						message = Resources.ImageSizeTooLarge;
					}
					else
					{
						if (eep.SourceSurface.Width > 32000)
						{
							message = Resources.ImageWidthTooLarge;
						}
						else
						{
							message = Resources.ImageHeightTooLarge;
						}
					}

					throw new ImageSizeTooLargeException(message);
				}
			}

			data = IntPtr.Zero;
#if DEBUG
            phase = PluginPhase.None; 
#endif
			errorMessage = String.Empty;
			fillOutData = true;
            disposed = false;
            frsetup = false;
            suitesSetup = false;
            sizesSetup = false;
            frValuesSetup = false;
            enumErrorList = null;
            enumResList = null;
				
			filterRecord = new FilterRecord();
			platformData = new PlatformData();
			platformData.hwnd = owner;
			platFormDataPtr = GCHandle.Alloc(platformData, GCHandleType.Pinned);
            outRect.left = outRect.top = outRect.right = outRect.bottom = 0;
            inRect.left = inRect.top = inRect.right = inRect.bottom = 0;
            maskRect.left = maskRect.right = maskRect.bottom = maskRect.top = 0;


            inDataOfs = Marshal.OffsetOf(typeof(FilterRecord), "inData").ToInt32();
            outDataOfs = Marshal.OffsetOf(typeof(FilterRecord), "outData").ToInt32();
            maskDataOfs = Marshal.OffsetOf(typeof(FilterRecord), "maskData").ToInt32();
            inRowBytesOfs = Marshal.OffsetOf(typeof(FilterRecord), "inRowBytes").ToInt32();
            outRowBytesOfs = Marshal.OffsetOf(typeof(FilterRecord), "outRowBytes").ToInt32();
            maskRowBytesOfs = Marshal.OffsetOf(typeof(FilterRecord), "maskRowBytes").ToInt32();


			source = (Bitmap)eep.SourceSurface.CreateAliasedBitmap().Clone();

			dest = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

			secondaryColor = new byte[4] { eep.SecondaryColor.R, eep.SecondaryColor.G, eep.SecondaryColor.B, 255 };
			
			primaryColor = new byte[4] { eep.PrimaryColor.R, eep.PrimaryColor.G, eep.PrimaryColor.B, 255 };

			using (Graphics gr = Graphics.FromImage(eep.SourceSurface.CreateAliasedBitmap()))
			{
				dpiX = gr.DpiX;
				dpiY = gr.DpiY;
			}

            selectedRegion = null;
			if (eep.GetSelection(eep.SourceSurface.Bounds).GetBoundsInt() == eep.SourceSurface.Bounds)
			{
				filterCase = FilterCase.filterCaseEditableTransparencyNoSelection;
			}
			else
			{
				filterCase = FilterCase.filterCaseEditableTransparencyWithSelection;
                selectedRegion = eep.GetSelection(eep.SourceSurface.Bounds).GetRegionReadOnly().Clone();
			}

#if DEBUG
			dbgFlags = DebugFlags.AdvanceState;
			dbgFlags |= DebugFlags.Call;
			dbgFlags |= DebugFlags.ColorServices;
			dbgFlags |= DebugFlags.DisplayPixels;
			dbgFlags |= DebugFlags.Error;
			dbgFlags |= DebugFlags.HandleSuite;
			dbgFlags |= DebugFlags.MiscCallbacks; // progress callback 
#endif
		}
		/// <summary>
		/// The Secondary (background) color in PDN
		/// </summary>
		static byte[] secondaryColor;
		/// <summary>
		/// The Primary (foreground) color in PDN
		/// </summary>
		static byte[] primaryColor;

		static bool ignoreAlpha;

		static bool IgnoreAlphaChannel(PluginData data)
		{
			if (data.category == "DCE Tools")
			{
                switch (filterCase)
                {
                    case FilterCase.filterCaseEditableTransparencyNoSelection:
                        filterCase = FilterCase.filterCaseFlatImageNoSelection;
                        break;
                    case FilterCase.filterCaseEditableTransparencyWithSelection:
                        filterCase = FilterCase.filterCaseFlatImageWithSelection;
                        break;
                }
				return true;
			}

			if (data.filterInfo != null)
			{
				if (data.filterInfo[(filterCase - 1)].inputHandling == FilterDataHandling.filterDataHandlingCantFilter)
				{ 
					switch (filterCase)
					{
						case FilterCase.filterCaseEditableTransparencyNoSelection:
							filterCase = FilterCase.filterCaseFlatImageNoSelection;
							break;
						case FilterCase.filterCaseEditableTransparencyWithSelection:
							filterCase = FilterCase.filterCaseFlatImageWithSelection;
							break;
					}
					return true;
				} 
			}

			return false;
		}

		static bool LoadFilter(ref PluginData pdata)
		{
			bool loaded = false;

			if ((pdata.entry.dll != null) && !pdata.entry.dll.IsInvalid)
				return true;     
			
			if (!string.IsNullOrEmpty(pdata.entryPoint)) // The filter has already been queried so take a shortcut.
			{
				pdata.entry.dll = NativeMethods.LoadLibraryEx(pdata.fileName, IntPtr.Zero, 0U);

				IntPtr entry = NativeMethods.GetProcAddress(pdata.entry.dll, pdata.entryPoint);

				if (entry != IntPtr.Zero)
				{
					pdata.entry.entry = (filterep)Marshal.GetDelegateForFunctionPointer(entry, typeof(filterep));
					loaded = true;
				}
			}

			return loaded;
		}
		
		/// <summary>
		/// Free the loaded PluginData.
		/// </summary>
		/// <param name="pdata">The PluginData to  free/</param>
		static void FreeLibrary(ref PluginData pdata)
		{
			if (!pdata.entry.dll.IsInvalid)
			{
				pdata.entry.dll.Dispose();
				pdata.entry.dll = null;
				pdata.entry.entry = null;
			}
		}
		
		static bool plugin_about(PluginData pdata)
		{

			AboutRecord about = new AboutRecord()
			{
				platformData = platFormDataPtr.AddrOfPinnedObject(),
			};

			result = PSError.noErr;

			GCHandle gch = GCHandle.Alloc(about, GCHandleType.Pinned);

			try 
			{	        
				pdata.entry.entry(FilterSelector.filterSelectorAbout, gch.AddrOfPinnedObject(), ref data, ref result);
			}
			finally
			{
				gch.Free();
			}
			

			if (result != PSError.noErr)
			{            
				FreeLibrary(ref pdata);
#if DEBUG
				Ping(DebugFlags.Error, string.Format("filterSelectorAbout returned result code {0}", result.ToString())); 
#endif
				return false;
			}

			return true;
		}

		static bool plugin_apply(PluginData pdata)
		{
#if DEBUG
            Debug.Assert(phase == PluginPhase.Prepare);
#endif 
			result = PSError.noErr;

#if DEBUG
			Ping(DebugFlags.Call, "Before FilterSelectorStart"); 
#endif

			pdata.entry.entry(FilterSelector.filterSelectorStart, filterRecordPtr.AddrOfPinnedObject(), ref data, ref result);

#if DEBUG
			Ping(DebugFlags.Call, "After FilterSelectorStart");
#endif
			filterRecord = (FilterRecord)filterRecordPtr.Target;

			if (result != PSError.noErr)
			{
				FreeLibrary(ref pdata);
				errorMessage = error_message(result);

#if DEBUG
				Ping(DebugFlags.Error, string.Format("filterSelectorStart returned result code: {0}({1})", errorMessage, result));
#endif                
				return false;
			}
			while (RectNonEmpty(filterRecord.inRect) || RectNonEmpty(filterRecord.outRect))
			{

                advance_state_proc();
				result = PSError.noErr;

#if DEBUG
				Ping(DebugFlags.Call, "Before FilterSelectorContinue"); 
#endif
				
				pdata.entry.entry(FilterSelector.filterSelectorContinue, filterRecordPtr.AddrOfPinnedObject(), ref data, ref result);

#if DEBUG
				Ping(DebugFlags.Call, "After FilterSelectorContinue"); 
#endif

				filterRecord = (FilterRecord)filterRecordPtr.Target;

				if (result != PSError.noErr)
				{
					short saved_result = result;

					result = PSError.noErr;

#if DEBUG
					Ping(DebugFlags.Call, "Before FilterSelectorFinish"); 
#endif
					
					pdata.entry.entry(FilterSelector.filterSelectorFinish, filterRecordPtr.AddrOfPinnedObject(), ref data, ref result);

#if DEBUG
					Ping(DebugFlags.Call, "After FilterSelectorFinish"); 
#endif


					FreeLibrary(ref pdata);

					errorMessage = error_message(saved_result);

#if DEBUG
					Ping(DebugFlags.Error, string.Format("filterSelectorContinue returned result code: {0}({1})", errorMessage, saved_result)); 
#endif

					return false;
				}
			}
			advance_state_proc();

			return true;
		}

		static bool plugin_parms(PluginData pdata)
		{
			result = PSError.noErr;

            /* Photoshop sets the size info before the filterSelectorParameters call even though the documentation says it does not.*/
            setup_sizes();
            SetFilterRecordValues();

#if DEBUG
			Ping(DebugFlags.Call, "Before filterSelectorParameters"); 
#endif

			pdata.entry.entry(FilterSelector.filterSelectorParameters, filterRecordPtr.AddrOfPinnedObject(), ref data, ref result);
#if DEBUG            
			Ping(DebugFlags.Call, string.Format("data = {0:X},  parameters = {1:X}", data, ((FilterRecord)filterRecordPtr.Target).parameters));


			Ping(DebugFlags.Call, "After filterSelectorParameters"); 
#endif

			filterRecord = (FilterRecord)filterRecordPtr.Target;

			if (result != PSError.noErr)
			{
				FreeLibrary(ref pdata);
				errorMessage = error_message(result);
#if DEBUG
				Ping(DebugFlags.Error, string.Format("filterSelectorParameters failed result code: {0}({1})", errorMessage, result)); 
#endif
				return false;
			}

#if DEBUG
            phase = PluginPhase.Parameters; 
#endif

			return true;
		}

        static bool frValuesSetup;
        static void SetFilterRecordValues()
        {
            if (frValuesSetup)
                return;

            frValuesSetup = true;

            filterRecord.isFloating = 0;

            if (filterCase == FilterCase.filterCaseEditableTransparencyWithSelection
            || filterCase == FilterCase.filterCaseFlatImageWithSelection)
            {
                DrawMaskBitmap();
                filterRecord.haveMask = 1;
                filterRecord.autoMask = 1;
            }
            else
            {
                filterRecord.haveMask = 0;
                filterRecord.autoMask = 0;
            }
            // maskRect
            filterRecord.maskData = IntPtr.Zero;
            filterRecord.maskRowBytes = 0;

            filterRecord.imageMode = PSConstants.plugInModeRGBColor;
            if (ignoreAlpha)
            {
                filterRecord.inLayerPlanes = 0;
                filterRecord.inTransparencyMask = 0; // Paint.NET is always PixelFormat.Format32bppArgb			
                filterRecord.inNonLayerPlanes = 3;
            }
            else
            {
                filterRecord.inLayerPlanes = 3;
                filterRecord.inTransparencyMask = 1; // Paint.NET is always PixelFormat.Format32bppArgb			
                filterRecord.inNonLayerPlanes = 0;
            }
            filterRecord.inLayerMasks = 0;
            filterRecord.inInvertedLayerMasks = 0;

            filterRecord.outLayerPlanes = filterRecord.inLayerPlanes;
            filterRecord.outTransparencyMask = filterRecord.inTransparencyMask;
            filterRecord.outLayerMasks = filterRecord.inLayerMasks;
            filterRecord.outInvertedLayerMasks = filterRecord.inInvertedLayerMasks;
            filterRecord.outNonLayerPlanes = filterRecord.inNonLayerPlanes;

            filterRecord.absLayerPlanes = filterRecord.inLayerPlanes;
            filterRecord.absTransparencyMask = filterRecord.inTransparencyMask;
            filterRecord.absLayerMasks = filterRecord.inLayerMasks;
            filterRecord.absInvertedLayerMasks = filterRecord.inInvertedLayerMasks;
            filterRecord.absNonLayerPlanes = filterRecord.inNonLayerPlanes;

            filterRecord.inPreDummyPlanes = 0;
            filterRecord.inPostDummyPlanes = 0;
            filterRecord.outPreDummyPlanes = 0;
            filterRecord.outPostDummyPlanes = 0;

            filterRecord.inColumnBytes = ignoreAlpha ? 3 : 4;
            filterRecord.inPlaneBytes = 1;
            filterRecord.outColumnBytes = filterRecord.inColumnBytes;
            filterRecord.outPlaneBytes = 1;

            filterRecordPtr.Target = filterRecord;
        }

		static bool plugin_prepare(PluginData pdata)
		{
			if (!LoadFilter(ref pdata))
			{
#if DEBUG
				Ping(DebugFlags.Error, "LoadFilter failed"); 
#endif
				return false;
			}

		   
			setup_sizes();
            SetFilterRecordValues();
			
			result = PSError.noErr;


#if DEBUG
			Ping(DebugFlags.Call, "Before filterSelectorPrepare"); 
#endif
			pdata.entry.entry(FilterSelector.filterSelectorPrepare, filterRecordPtr.AddrOfPinnedObject(), ref data, ref result);

#if DEBUG
			Ping(DebugFlags.Call, "After filterSelectorPrepare"); 
#endif
			filterRecord = (FilterRecord)filterRecordPtr.Target;
		   
			
			if (result != PSError.noErr)
			{           
				FreeLibrary(ref pdata);
				errorMessage = error_message(result);
#if DEBUG
				Ping(DebugFlags.Error, string.Format("filterSelectorParameters failed result code: {0}({1})", errorMessage, result)); 
#endif
				return false;
			}

#if DEBUG
            phase = PluginPhase.Prepare; 
#endif

			return true;
		}

		/// <summary>
		/// Runs a filter from the specified PluginData
		/// </summary>
		/// <param name="pdata">The PluginData to run</param>
		/// <param name="showAbout">Show the Filter's About Box</param>
		/// <returns>True if successful otherwise false</returns>
		/// <exception cref="PSFilterLoad.PSApi.FilterLoadException">The Exception thrown when there is a problem with loading the Filter PiPl data.</exception>
		public bool RunPlugin(PluginData pdata, bool showAbout)
		{
			if (!LoadFilter(ref pdata))
			{
#if DEBUG
				Debug.WriteLine("LoadFilter failed"); 
#endif
				return false;
			}			
			
			ignoreAlpha = IgnoreAlphaChannel(pdata);

            if (!ignoreAlpha)
            {
                DrawCheckerBoardBitmap();
            }
            else // otherwise if ignoreAlpha is true make the "destFileName" image 24-bit RGB.
            {
                dest.Dispose();
                dest = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            }

			if (pdata.filterInfo != null)
			{
				// compensate for the fact that the FilterCaseInfo array is zero indexed.
				fillOutData = ((pdata.filterInfo[(filterCase - 1)].flags1 & FilterCaseInfoFlags.PIFilterDontCopyToDestinationBit) == 0);
			}

			if (showAbout)
			{
				return plugin_about(pdata);
			}


			setup_delegates();
			setup_suites();
			setup_filter_record();
			
            if (!plugin_parms(pdata))
			{
#if DEBUG
				Ping(DebugFlags.Error, "plugin_parms failed"); 
#endif
				return false;
			}

			if (!plugin_prepare(pdata))
			{
#if DEBUG
				Ping(DebugFlags.Error, "plugin_prepare failed"); 
#endif
				return false;
			}

			if (!plugin_apply(pdata))
			{
#if DEBUG
				Ping(DebugFlags.Error, "plugin_apply failed"); 
#endif
				return false;
			}

			FreeLibrary(ref pdata);
			return true;
		}

		static List<PluginData> enumResList;
        static List<FilterLoadException> enumErrorList;

		static void AddFoundPluginData(PluginData data)
		{
			if (enumResList == null)
			{
				enumResList = new List<PluginData>();
			}
			enumResList.Add(data);
		}
        /// <summary>
        /// Querys a 8bf plugin
        /// </summary>
        /// <param name="fileName">The fileName to query.</param>
        /// <param name="pluginData">The list filters within the plugin.</param>
        /// <returns>
        /// True if succssful otherwise false
        /// </returns>
		public static bool QueryPlugin(string fileName, out List<PluginData> pluginData, out List<FilterLoadException> loadErrors)
		{
			if (String.IsNullOrEmpty(fileName))
				throw new ArgumentException("fileName is null or empty.", "fileName");

			pluginData = new List<PluginData>();
            loadErrors = new List<FilterLoadException>();

			bool result = false;

			SafeLibraryHandle dll = NativeMethods.LoadLibraryEx(fileName, IntPtr.Zero, NativeConstants.LOAD_LIBRARY_AS_DATAFILE);
			/* Use LOAD_LIBRARY_AS_DATAFILE to prevent a BadImageFormatException from being thrown if the file
			 * is a different processor architecture than the parent process.
			 */
			if (!dll.IsInvalid)
			{
				PluginData pdata = new PluginData() { fileName = fileName };
				GCHandle gch = GCHandle.Alloc(pdata);
				enumResList = null;
                enumErrorList = new List<FilterLoadException>();
                try
                {
                    if (!queryPlugin)
                    {
                        queryPlugin = true;
                    }

                    if (NativeMethods.EnumResourceNames(dll.DangerousGetHandle(), "PiPl", new EnumResNameDelegate(EnumRes), GCHandle.ToIntPtr(gch)))
                    {
                        loadErrors.AddRange(enumErrorList);
                        foreach (PluginData data in enumResList)
                        {
                            if (data.entryPoint != null) // Was the entrypoint found for the plugin.
                            {
                                pluginData.Add(data);
                                if (!result)
                                {
                                    result = true;
                                }
                            }
                        }
                    }
#if DEBUG

                    else
                    {
                        Ping(DebugFlags.Error, string.Format("EnumResourceNames(PiPL) failed for {0}", fileName));
                    }
#endif

                }
                finally
				{
					gch.Free();
					dll.Dispose();
					dll = null;
				}

			}

			return result;
		}

		static string error_message(short result)
		{
			string error = string.Empty;

			if (result == PSError.userCanceledErr || result == 1) // Many plug-ins seem to return 1 to indicate Cancel
			{
				return Resources.UserCanceledError;
			}
			else
			{
				switch (result)
				{
					case PSError.readErr:
						error = Resources.FileReadError;
						break;
					case PSError.writErr:
						error = Resources.FileWriteError;
						break;
					case PSError.openErr:
						error = Resources.FileOpenError;
						break;
					case PSError.dskFulErr:
						error = Resources.DiskFullError;
						break;
					case PSError.ioErr:
						error = Resources.FileIOError;
						break;
					case PSError.memFullErr:
						error = Resources.OutOfMemoryError;
						break;
					case PSError.nilHandleErr:
						error = Resources.NullHandleError;
						break;
					case PSError.filterBadParameters:
						error = Resources.BadParameters;
						break;
					case PSError.filterBadMode:
						error = Resources.UnsupportedImageMode;
						break;
					case PSError.errPlugInHostInsufficient:
						error = Resources.errPlugInHostInsufficient;
						break;
					case PSError.errPlugInPropertyUndefined:
						error = Resources.errPlugInPropertyUndefined;
						break;
					case PSError.errHostDoesNotSupportColStep:
						error = Resources.errHostDoesNotSupportColStep;
						break;
					case PSError.errInvalidSamplePoint:
						error = Resources.InvalidSamplePoint;
						break;
					default:
						error = string.Format(System.Globalization.CultureInfo.CurrentCulture, "error code = {0}", result);
						break;
				}

			}
			return error;
		}

		static bool abort_proc()
		{
			if (abortFunc != null)
			{
				return abortFunc();
			}

			return false;
		}

		static bool src_valid;
		static bool dst_valid;

        static int inDataOfs;
        static int outDataOfs;
        static int maskDataOfs;
        static int inRowBytesOfs;
        static int outRowBytesOfs;
        static int maskRowBytesOfs;


        static Rect16 outRect;
        static int outRowBytes;
        static int outLoPlane;
        static int outHiPlane;
        static Rect16 inRect;
        static Rect16 maskRect;

        /// <summary>
        /// Fill the output buffer with data, some plugins set this to false if they modify all the image data
        /// </summary>
        static bool fillOutData;

        static short advance_state_proc()
        {
            filterRecord = (FilterRecord)filterRecordPtr.Target;

            if (dst_valid && RectNonEmpty(outRect))
            {
                store_buf(filterRecord.outData, outRowBytes, outRect, outLoPlane, outHiPlane);
            }

#if DEBUG
            Ping(DebugFlags.AdvanceState, string.Format("Inrect = {0}, Outrect = {1}", filterRecord.inRect.ToString(), filterRecord.outRect.ToString()));
#endif
            if (filterRecord.haveMask == 1 && RectNonEmpty(filterRecord.maskRect))
            {
                if (!maskRect.Equals(filterRecord.maskRect))
                {
                    if (filterRecord.maskData != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(filterRecord.maskData);
                        filterRecord.maskData = IntPtr.Zero;
                    }

                    fill_mask(ref filterRecord.maskData, ref filterRecord.maskRowBytes, filterRecord.maskRect);
                    maskRect = filterRecord.maskRect;
                }
            }
            else
            {
                if (filterRecord.haveMask == 1)
                {
                    if (filterRecord.maskData != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(filterRecord.maskData);
                        filterRecord.maskData = IntPtr.Zero;
                    }
                    filterRecord.maskRowBytes = 0;
                    maskRect = filterRecord.maskRect;
                }
            }


            if (RectNonEmpty(filterRecord.inRect))
            {
                if (!inRect.Equals(filterRecord.inRect) || ((filterRecord.inHiPlane - filterRecord.inLoPlane) + 1) == 1)
                {
                    if (src_valid)
                    {
                        Marshal.FreeHGlobal(filterRecord.inData);
                        filterRecord.inData = IntPtr.Zero;
                        src_valid = false;
                    }

                    fill_buf(ref filterRecord.inData, ref filterRecord.inRowBytes, filterRecord.inRect, filterRecord.inLoPlane, filterRecord.inHiPlane);
                    inRect = filterRecord.inRect;
                    src_valid = true;
                }
            }
            else
            {
                if (src_valid)
                {
                    Marshal.FreeHGlobal(filterRecord.inData);
                    filterRecord.inData = IntPtr.Zero;
                    src_valid = false;
                }
                filterRecord.inRowBytes = 0;
                inRect = filterRecord.inRect;
            }
         
            if (RectNonEmpty(filterRecord.outRect))
            {
                if (fillOutData && (!outRect.Equals(filterRecord.outRect) || ((filterRecord.outHiPlane - filterRecord.outLoPlane) + 1) == 1))
                {
                    if (dst_valid)
                    {
                        Marshal.FreeHGlobal(filterRecord.outData);
                        filterRecord.outData = IntPtr.Zero;
                        dst_valid = false;
                    }

                    fill_buf(ref filterRecord.outData, ref filterRecord.outRowBytes, filterRecord.outRect, filterRecord.outLoPlane, filterRecord.outHiPlane);
                    dst_valid = true;
                }
#if DEBUG
                Debug.WriteLine(string.Format("outRowBytes = {0}", filterRecord.outRowBytes));
#endif
                // store previous values
                outRowBytes = filterRecord.outRowBytes;
                outRect = filterRecord.outRect;
                outLoPlane = filterRecord.outLoPlane;
                outHiPlane = filterRecord.outHiPlane;
            }
            else
            {
                if (dst_valid)
                {
                    Marshal.FreeHGlobal(filterRecord.outData);
                    filterRecord.outData = IntPtr.Zero;
                    dst_valid = false;
                }
                filterRecord.outRowBytes = 0;
                outRect = filterRecord.outRect;
                outRowBytes = filterRecord.outRowBytes;
                outLoPlane = filterRecord.outLoPlane;
                outHiPlane = filterRecord.outHiPlane;
            }
           
            Marshal.WriteIntPtr(filterRecordPtr.AddrOfPinnedObject(), maskDataOfs, filterRecord.maskData);
            Marshal.WriteInt32(filterRecordPtr.AddrOfPinnedObject(), maskRowBytesOfs, filterRecord.maskRowBytes);

            Marshal.WriteIntPtr(filterRecordPtr.AddrOfPinnedObject(), inDataOfs, filterRecord.inData);
            Marshal.WriteInt32(filterRecordPtr.AddrOfPinnedObject(), inRowBytesOfs, filterRecord.inRowBytes);

#if DEBUG
            Debug.WriteLine(string.Format("indata = {0:X8}, inRowBytes = {1}", filterRecord.inData.ToInt64(), filterRecord.inRowBytes));
#endif
            Marshal.WriteIntPtr(filterRecordPtr.AddrOfPinnedObject(), outDataOfs, filterRecord.outData);
            Marshal.WriteInt32(filterRecordPtr.AddrOfPinnedObject(), outRowBytesOfs, filterRecord.outRowBytes);

            return PSError.noErr;
        }


        /// <summary>
        /// Fills the input buffer with data from the source image.
        /// </summary>
        /// <param name="inData">The input buffer to fill.</param>
        /// <param name="inRowBytes">The stride of the input buffer.</param>
        /// <param name="rect">The rectangle of interest within the image.</param>
        /// <param name="loplane">The input loPlane.</param>
        /// <param name="hiplane">The input hiPlane.</param>
        static unsafe void fill_buf(ref IntPtr inData, ref int inRowBytes, Rect16 rect, int loplane, int hiplane)
        {
#if DEBUG
            Ping(DebugFlags.AdvanceState, string.Format("inRowBytes = {0}, Rect = {1}, loplane = {2}, hiplane = {3}", new object[] { inRowBytes.ToString(), Utility.RectToString(rect), loplane.ToString(), hiplane.ToString() }));
            Ping(DebugFlags.AdvanceState, string.Format("inputRate = {0}", (filterRecord.inputRate >> 16)));
#endif

            int nplanes = hiplane - loplane + 1;
            int w = (rect.right - rect.left);
            int h = (rect.bottom - rect.top);

            if (rect.left < source.Width && rect.top < source.Height)
            {
                int bmpw = w;
                int bmph = h;
                if ((rect.left + w) > source.Width)
                    bmpw = (source.Width - rect.left);

                if ((rect.top + h) > source.Height)
                    bmph = (source.Height - rect.top);

#if DEBUG
                if (bmpw != w || bmph != h)
                {
                    Ping(DebugFlags.AdvanceState, string.Format("bmpw = {0}, bpmh = {1}", bmpw, bmph));
                }
#endif
                Bitmap temp = null;
                Rectangle lockRect = Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);

                try
                {
                    int scale = (filterRecord.inputRate >> 16);
                    if (scale > 1) // Filter preview?
                    {
                        int scalew = source.Width / scale;
                        int scaleh = source.Height / scale;

                        if (lockRect.Width > scalew)
                        {
                            scalew = lockRect.Width;
                        }

                        if (lockRect.Height > scaleh)
                        {
                            scaleh = lockRect.Height;
                        }

                        temp = new Bitmap(scalew, scaleh, source.PixelFormat);

                        using (Graphics gr = Graphics.FromImage(temp))
                        {
                            gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            gr.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                            gr.DrawImage(source, new Rectangle(0, 0, scalew, scaleh));
                        }
                    }
                    else
                    {
                        temp = (Bitmap)source.Clone();
                    }

                    BitmapData data = temp.LockBits(lockRect, ImageLockMode.ReadOnly, source.PixelFormat);
                    try
                    {



                        if (!fillOutData)
                        {
                            int outLen = (h * (w * nplanes));

                            filterRecord.outData = Marshal.AllocHGlobal(outLen);
                            filterRecord.outRowBytes = (w * nplanes);
                        }

                        if (bpp == nplanes && bmpw == w)
                        {
                            int stride = (bmpw * 4);
                            int len = stride * data.Height;

                            inData = Marshal.AllocHGlobal(len);
                            inRowBytes = stride;

                            /* the stride for the source image and destination buffer will almost never match
                             * so copy the data manually swapping the pixel order along the way
                             */
                            for (int y = 0; y < data.Height; y++)
                            {
                                byte* srcRow = (byte*)data.Scan0.ToPointer() + (y * data.Stride);
                                byte* dstRow = (byte*)inData.ToPointer() + (y * stride);
                                for (int x = 0; x < data.Width; x++)
                                {
                                    dstRow[0] = srcRow[2];
                                    dstRow[1] = srcRow[1];
                                    dstRow[2] = srcRow[0];
                                    dstRow[3] = srcRow[3];

                                    srcRow += 4;
                                    dstRow += 4;
                                }
                            }
                        }
                        else
                        {
                            int dl = nplanes * w * h;

                            inData = Marshal.AllocHGlobal(dl);

                            inRowBytes = nplanes * w;
                            for (int y = 0; y < data.Height; y++)
                            {
                                byte* row = (byte*)data.Scan0.ToPointer() + (y * data.Stride);
                                for (int i = loplane; i <= hiplane; i++)
                                {
                                    int ofs = i;
                                    switch (i) // Photoshop uses RGBA pixel order so map the Red and Blue channels to BGRA order
                                    {
                                        case 0:
                                            ofs = 2;
                                            break;
                                        case 2:
                                            ofs = 0;
                                            break;
                                    }

                                    /*byte *src = row + ofs;
                                    byte *q = (byte*)inData.ToPointer() + (y - rect.top) * inRowBytes + (i - loplane);*/

#if DEBUG
                                    //                              Debug.WriteLine("y = " + y.ToString());
#endif
                                    for (int x = 0; x < data.Width; x++)
                                    {
                                        byte* p = row + (x * bpp) + ofs; // the target color channel of the target pixel
                                        byte* q = (byte*)inData.ToPointer() + (y * inRowBytes) + (x * nplanes) + (i - loplane);

                                        *q = *p;

                                    }
                                }
                            }


                        }
                    }
                    finally
                    {
                        temp.UnlockBits(data);
                    }
                }
                finally
                {
                    if (temp != null)
                    {
                        temp.Dispose();
                        temp = null;
                    }
                }


            }
        }

        /// <summary>
        /// Fills the mask buffer with data from the mask image.
        /// </summary>
        /// <param name="maskData">The mask buffer to fill.</param>
        /// <param name="maskRowBytes">The stride of the mask buffer.</param>
        /// <param name="rect">The rectangle of interest within the image.</param>
        static unsafe void fill_mask(ref IntPtr maskData, ref int maskRowBytes, Rect16 rect)
        {
#if DEBUG
            Ping(DebugFlags.AdvanceState, string.Format("maskRowBytes = {0}, Rect = {1}", new object[] { maskRowBytes.ToString(), rect.ToString() }));
            Ping(DebugFlags.AdvanceState, string.Format("maskRate = {0}", (filterRecord.maskRate >> 16)));
#endif
            int w = (rect.right - rect.left);
            int h = (rect.bottom - rect.top);

            if (rect.left < source.Width && rect.top < source.Height)
            {
                int bmpw = w;
                int bmph = h;
                if ((rect.left + w) > source.Width)
                    bmpw = (source.Width - rect.left);

                if ((rect.top + h) > source.Height)
                    bmph = (source.Height - rect.top);

#if DEBUG
                if (bmpw != w || bmph != h)
                {
                    Ping(DebugFlags.AdvanceState, string.Format("bmpw = {0}, bpmh = {1}", bmpw, bmph));
                }
#endif
                Bitmap temp = null;
                Rectangle lockRect = Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);

                try
                {
                    int scale = (filterRecord.maskRate >> 16);
                    if (scale > 1) // Filter preview?
                    {
                        int width = source.Width / scale;
                        int height = source.Height / scale;

                        if (lockRect.Width > width)
                        {
                            width = lockRect.Width;
                        }

                        if (lockRect.Height > height)
                        {
                            height = lockRect.Height;
                        }

                        temp = new Bitmap(width, height, maskBitmap.PixelFormat);

                        using (Graphics gr = Graphics.FromImage(temp))
                        {
                            gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            gr.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                            gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                            gr.DrawImage(maskBitmap, Rectangle.FromLTRB(0, 0, width, height));
                        }

                    }
                    else
                    {
                        temp = (Bitmap)maskBitmap.Clone();
                    }

                    BitmapData data = temp.LockBits(lockRect, ImageLockMode.ReadOnly, maskBitmap.PixelFormat);
                    try
                    {


                        int len = bmpw * bmph;

                        maskData = Marshal.AllocHGlobal(len);
                        maskRowBytes = bmpw;

                        /* the stride for the source image and destination buffer will almost never match
                         * so copy the data manually swapping the pixel order along the way
                         */
                        for (int y = 0; y < data.Height; y++)
                        {
                            byte* srcRow = (byte*)data.Scan0.ToPointer() + (y * data.Stride);
                            byte* dstRow = (byte*)maskData.ToPointer() + (y * bmpw);
                            for (int x = 0; x < data.Width; x++)
                            {
                                *dstRow = *srcRow;

                                srcRow += 3;
                                dstRow++;
                            }
                        }

                    }
                    finally
                    {
                        temp.UnlockBits(data);
                    }
                }
                finally
                {
                    temp.Dispose();
                    temp = null;
                }

            }
        }

        /// <summary>
        /// Stores the output buffer to the destination image.
        /// </summary>
        /// <param name="outData">The output buffer.</param>
        /// <param name="outRowBytes">The stride of the output buffer.</param>
        /// <param name="rect">The target rectangle within the image.</param>
        /// <param name="loplane">The output loPlane.</param>
        /// <param name="hiplane">The output hiPlane.</param>
        static void store_buf(IntPtr outData, int outRowBytes, Rect16 rect, int loplane, int hiplane)
        {
#if DEBUG
            Ping(DebugFlags.AdvanceState, string.Format("inRowBytes = {0}, Rect = {1}, loplane = {2}, hiplane = {3}", new object[] { outRowBytes.ToString(), Utility.RectToString(rect), loplane.ToString(), hiplane.ToString() }));
#endif
            if (outData == IntPtr.Zero)
            {
                return;
            }

            int nplanes = hiplane - loplane + 1;
            int w = (rect.right - rect.left);
            int h = (rect.bottom - rect.top);

            if (RectNonEmpty(rect))
            {
                if (rect.left < source.Width && rect.top < source.Height)
                {
                    int bmpw = w;
                    int bmph = h;
                    if ((rect.left + w) > source.Width)
                        bmpw = (source.Width - rect.left);

                    if ((rect.top + h) > source.Height)
                        bmph = (source.Height - rect.top);

                    BitmapData data = dest.LockBits(new Rectangle(rect.left, rect.top, bmpw, bmph), ImageLockMode.WriteOnly, dest.PixelFormat);
                    try
                    {
                        if (nplanes == bpp && bmpw == w)
                        {
                            unsafe
                            {
                                for (int y = 0; y < data.Height; y++)
                                {
                                    byte* srcRow = (byte*)outData.ToPointer() + (y * outRowBytes);
                                    byte* dstRow = (byte*)data.Scan0.ToPointer() + (y * data.Stride);
                                    for (int x = 0; x < data.Width; x++)
                                    {
                                        dstRow[0] = srcRow[2];
                                        dstRow[1] = srcRow[1];
                                        dstRow[2] = srcRow[0];
                                        dstRow[3] = srcRow[3];

                                        srcRow += 4;
                                        dstRow += 4;
                                    }
                                }
                            }
                        }
                        else
                        {
                            unsafe
                            {
                                int destBpp = ignoreAlpha ? 3 : 4;
                                for (int y = 0; y < data.Height; y++)
                                {
                                    byte* dstPtr = (byte*)data.Scan0.ToPointer() + (y * data.Stride);

                                    for (int i = loplane; i <= hiplane; i++)
                                    {
                                        int ofs = i;
                                        switch (i)
                                        {
                                            case 0:
                                                ofs = 2;
                                                break;
                                            case 2:
                                                ofs = 0;
                                                break;
                                        }
                                        byte* q = (byte*)outData.ToPointer() + (y * outRowBytes) + (i - loplane);
                                        byte* p = dstPtr + ofs;

                                        for (int x = 0; x < data.Width; x++)
                                        {

                                            if (!ignoreAlpha && hiplane < 3)
                                            {
                                                byte* alpha = dstPtr + ((x * bpp) + 3);
                                                *alpha = 255;
                                            }


                                            *p = *q;

                                            p += destBpp;
                                            q += nplanes;
                                        }
                                    }
                                }


                            }
                        }
                    }
                    finally
                    {
                        dest.UnlockBits(data);
                    }


                }
            }
        }

		static short allocate_buffer_proc(int size, ref System.IntPtr bufferID)
		{
#if DEBUG
			Ping(DebugFlags.BufferSuite, string.Format("Size = {0}", size));
#endif     
			short err = PSError.noErr;
			try
			{
				bufferID = Marshal.AllocHGlobal(size);
				if (size > 0)
				{
					GC.AddMemoryPressure(size);
				}
			}
			catch (OutOfMemoryException)
			{
				err = PSError.memFullErr;
			}

			return err;
		}
		static void buffer_free_proc(System.IntPtr bufferID)
		{
			long size = (long)NativeMethods.LocalSize(bufferID).ToUInt64();
#if DEBUG
			Ping(DebugFlags.BufferSuite, string.Format("Buffer address = {0:X8}, Size = {1}", bufferID.ToInt64(), size));
#endif     
			if (size > 0L)
			{
				GC.RemoveMemoryPressure(size);
			}
			Marshal.FreeHGlobal(bufferID);
		   
		}
		static IntPtr buffer_lock_proc(System.IntPtr bufferID, byte moveHigh)
		{
#if DEBUG
			Ping(DebugFlags.BufferSuite, string.Format("Buffer address = {0:X8}", bufferID.ToInt64())); 
#endif
			
			return bufferID;
		}
		static void buffer_unlock_proc(System.IntPtr bufferID)
		{
#if DEBUG
		   Ping(DebugFlags.BufferSuite, string.Format("Buffer address = {0:X8}", bufferID.ToInt64()));
#endif    
		}
		static int buffer_space_proc()
		{
			NativeStructs.MEMORYSTATUSEX lpbuffer = new NativeStructs.MEMORYSTATUSEX();
			if (NativeMethods.GlobalMemoryStatusEx(lpbuffer))
			{
				return (int)lpbuffer.ullAvailVirtual;
			}
			return 1000000000;
		}

		static short color_services_proc(ref ColorServicesInfo info)
		{
			short err = PSError.noErr;
			switch (info.selector)
			{
				case ColorServicesSelector.plugIncolorServicesChooseColor:
#if PSSDK_3_0_4
					string name = StringFromPString(info.selectorParameter.pickerPrompt);
#else
					string name = StringFromPString(info.pickerPrompt);
#endif
					if (!string.IsNullOrEmpty(name)) // only show the picker dialog if the title is not empty
					{
						 using (ColorPicker picker = new ColorPicker())
						 {
							 picker.Title = name;
							 picker.AllowFullOpen = true;
							 picker.AnyColor = true;
							 picker.SolidColorOnly = true;

							 picker.Color = Color.FromArgb(info.colorComponents[0], info.colorComponents[1], info.colorComponents[2]);

							 if (picker.ShowDialog() == DialogResult.OK)
							 {
								 info.colorComponents[0] = picker.Color.R;
								 info.colorComponents[1] = picker.Color.G;
								 info.colorComponents[2] = picker.Color.B;

								 err = ColorServicesConvert.Convert(info.sourceSpace, info.resultSpace, ref info.colorComponents);

							 }
							 else
							 {
								err = PSError.userCanceledErr;
							 }
								 
						 }   
						  
					}

					break;
				case ColorServicesSelector.plugIncolorServicesConvertColor:

					err = ColorServicesConvert.Convert(info.sourceSpace, info.resultSpace, ref info.colorComponents);
					
					break;
#if PSSDK_3_0_4
				case ColorServicesSelector.plugIncolorServicesGetSpecialColor:
					
					unsafe
					{
						switch (info.selectorParameter.specialColorID)
						{
							case 0:

								fixed (byte* back = filterRecord.backColor)
								{
									for (int i = 0; i < 4; i++)
									{
										info.colorComponents[i] = (short)back[i];
									}
								}

								break;
							case 1:

								fixed (byte* fore = filterRecord.foreColor)
								{
									for (int i = 0; i < 4; i++)
									{
										info.colorComponents[i] = (short)fore[i];
									}
								}
								break;
							default:
								err = PSError.paramErr;
								break;
						}
					}
					break;
				case ColorServicesSelector.plugIncolorServicesSamplePoint:
					Point16 point = (Point16)Marshal.PtrToStructure(info.selectorParameter.globalSamplePoint, typeof(Point16));
						
					if (IsInSourceBounds(point))
					{
						Color pixel = source.GetPixel(point.h, point.v);
						info.colorComponents = new short[4] { (short)pixel.R, (short)pixel.G, (short)pixel.B, 0 };
						err = ColorServicesConvert.Convert(info.sourceSpace, info.resultSpace, ref info.colorComponents);
					}
					else
					{
						err = PSError.errInvalidSamplePoint;
					}

					break;
#endif

			}
			return err;
		}

#if PSSDK_3_0_4
		static bool IsInSourceBounds(Point16 point)
		{
			if (source == null) // Bitmap Disposed?
				return false;

			bool inh = (point.h >= 0 && point.h < (source.Width - 1));
			bool inv = (point.v >= 0 && point.v < (source.Height - 1));

			return (inh && inv);
		} 
#endif

		static unsafe short display_pixels_proc(ref PSPixelMap source, ref VRect srcRect, int dstRow, int dstCol, System.IntPtr platformContext)
		{
#if DEBUG
			Ping(DebugFlags.DisplayPixels, string.Format("source: bounds = {0}, ImageMode = {1}, colBytes = {2}, rowBytes = {3},planeBytes = {4}, BaseAddress = {5}", new object[]{Utility.RectToString(source.bounds), ((ImageModes)source.imageMode).ToString("G"),
			source.colBytes.ToString(), source.rowBytes.ToString(), source.planeBytes.ToString(), source.baseAddr.ToString("X8")}));
			Ping(DebugFlags.DisplayPixels, string.Format("dstCol (x, width) = {0}, dstRow (y, height) = {1}", dstCol, dstRow));
#endif

			if (platformContext == IntPtr.Zero || source.rowBytes == 0 || source.baseAddr == IntPtr.Zero)
				return PSError.filterBadParameters;

			int w = srcRect.right - srcRect.left;
			int h = srcRect.bottom - srcRect.top;
			int planes = filterRecord.planes;

			PixelFormat format = (planes == 4 && (source.colBytes == 4 || source.colBytes == 1)) ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
			int bpp = (Bitmap.GetPixelFormatSize(format) / 8);

			using (Bitmap bmp = new Bitmap(w, h, format))
			{
				BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);

				try
				{
					for (int y = 0; y < data.Height; y++)
					{

						if (source.colBytes == 1)
						{

							if (planes == 4 && source.colBytes == 1)
							{
								for (int x = 0; x < data.Width; x++)
								{
									byte* p = (byte*)data.Scan0.ToPointer() + (y * data.Stride) + (x * 4);
									p[3] = 255;
								}
							}

							for (int i = 0; i < planes; i++)
							{
								int ofs = i;
								switch (i) // Photoshop uses RGBA pixel order so map the Red and Blue channels to BGRA order
								{
									case 0:
										ofs = 2;
										break;
									case 2:
										ofs = 0;
										break;
								}
								byte* p = (byte*)data.Scan0.ToPointer() + (y * data.Stride) + ofs;
								byte* q = (byte*)source.baseAddr.ToPointer() + (source.rowBytes * y) + (i * source.planeBytes);

								for (int x = 0; x < data.Width; x++)
								{
									*p = *q;

									p += bpp;
									q += source.colBytes;
								}
							}

						}
						else
						{

							byte* p = (byte*)data.Scan0.ToPointer() + (y * data.Stride);
							byte* q = (byte*)source.baseAddr.ToPointer() + (source.rowBytes * y);
							for (int x = 0; x < data.Width; x++)
							{
								p[0] = q[2];
								p[1] = q[1];
								p[2] = q[0];
								if (source.colBytes == 4)
								{
									p[3] = q[3];
								}

								p += bpp;
								q += source.colBytes;
							}
						}
					}


				}
				finally
				{
					bmp.UnlockBits(data);
				}

				using (Graphics gr = Graphics.FromHdc(platformContext))
				{
					if (source.colBytes == 4)
					{
						using (Bitmap temp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
						{
							Rectangle rect = new Rectangle(0, 0, w, h);

							using (Graphics tempGr = Graphics.FromImage(temp))
							{
                                tempGr.DrawImageUnscaledAndClipped(checkerBoardBitmap, rect);
								tempGr.DrawImageUnscaled(bmp, rect);
							}
							// temp.Save(Path.Combine(Application.StartupPath, "masktemp.png"), ImageFormat.Png);

							gr.DrawImageUnscaled(temp, dstCol, dstRow);
						}

					}
					else
					{
						gr.DrawImage(bmp, dstCol, dstRow);
					}
				}
			}



			return PSError.noErr;
		}

        static Bitmap checkerBoardBitmap;
        static unsafe void DrawCheckerBoardBitmap()
        {
            checkerBoardBitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

            BitmapData bd = checkerBoardBitmap.LockBits(new Rectangle(0, 0, checkerBoardBitmap.Width, checkerBoardBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < checkerBoardBitmap.Height; y++)
                {
                    byte* p = (byte*)bd.Scan0.ToPointer() + (y * bd.Stride);
                    for (int x = 0; x < checkerBoardBitmap.Width; x++)
                    {
                        byte v = (byte)((((x ^ y) & 8) * 8) + 191);

                        p[0] = p[1] = p[2] = v;
                        p[3] = 255;
                        p += 4;
                    }
                }
            }
            finally
            {
                checkerBoardBitmap.UnlockBits(bd);
            }

        }

        static Bitmap maskBitmap;
        static unsafe void DrawMaskBitmap()
        {
            maskBitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);

            BitmapData bd = maskBitmap.LockBits(new Rectangle(0, 0, maskBitmap.Width, maskBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                for (int y = 0; y < maskBitmap.Height; y++)
                {
                    byte* p = (byte*)bd.Scan0.ToPointer() + (y * bd.Stride);
                    for (int x = 0; x < maskBitmap.Width; x++)
                    {

                        if (selectedRegion.IsVisible(x, y))
                        {
                            p[0] = p[1] = p[2] = 255;
                        }
                        else
                        {
                            p[0] = p[1] = p[2] = 0;
                        }

                        p += 3;
                    }
                }
            }
            finally
            {
                maskBitmap.UnlockBits(bd);
            }
        }

		static bool handle_valid(IntPtr h)
		{
			return ((handles != null) && handles.ContainsKey(h.ToInt64()));
		}

		static unsafe IntPtr handle_new_proc(int size)
		{
			try
			{
				IntPtr ptr = Marshal.AllocHGlobal(size);

				PSHandle hand = new PSHandle() { pointer = ptr, size = size };

				IntPtr handle = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PSHandle)));

				Marshal.StructureToPtr(hand, handle, false);

				if (handles == null)
					handles = new Dictionary<long, PSHandle>();

				handles.Add(handle.ToInt64(), hand);

				if (size > 0)
				{
					GC.AddMemoryPressure(size);
				}

#if DEBUG
				Ping(DebugFlags.HandleSuite, string.Format("Handle address = {0:X8}, size = {1}", ptr.ToInt64(), size));
#endif
				return handle;
			}
			catch (OutOfMemoryException)
			{
				return IntPtr.Zero;
			}
		}

		static void handle_dispose_proc(IntPtr h)
		{
			if (!handle_valid(h))
			{
				if (NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					NativeMethods.GlobalFree(h);
					return;
				}
				else if (!NativeMethods.IsBadReadPtr(h, new UIntPtr((uint)IntPtr.Size))
					&& NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					NativeMethods.GlobalFree(h);
					return;
				}
				else
				{
					return;
				}
			}
#if DEBUG
			Ping(DebugFlags.HandleSuite, string.Format("Handle address = {0:X8}", h));
			Ping(DebugFlags.HandleSuite, string.Format("Handle pointer address = {0:X8}", handles[h.ToInt64()].pointer));
#endif
			Marshal.FreeHGlobal(handles[h.ToInt64()].pointer);

			if (handles[h.ToInt64()].size > 0)
			{
				GC.RemoveMemoryPressure((long)handles[h.ToInt64()].size);
			}
			handles.Remove(h.ToInt64());
			Marshal.FreeHGlobal(h);
		}

		static IntPtr handle_lock_proc(IntPtr h, byte moveHigh)
		{
#if DEBUG
			Ping(DebugFlags.HandleSuite, string.Format("Handle address = {0:X8}, moveHigh = {1:X1}", h.ToInt64(), moveHigh));
#endif
			if (!handle_valid(h))
			{
				if (NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					return NativeMethods.GlobalLock(h);
				}
				else if (!NativeMethods.IsBadReadPtr(h, new UIntPtr((uint)UIntPtr.Size))
					&& NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					return NativeMethods.GlobalLock(h);
				}
				else
				{
					if (!NativeMethods.IsBadReadPtr(h, new UIntPtr((uint)IntPtr.Size)) && NativeMethods.IsBadWritePtr(h, new UIntPtr((uint)IntPtr.Size)))
					{
						return h;
					}
					else
						return IntPtr.Zero;
				}
			}

#if DEBUG
			Ping(DebugFlags.HandleSuite, String.Format("Handle Pointer Address = 0x{0:X}", handles[h.ToInt64()].pointer));
#endif       
			return NativeMethods.GlobalLock(handles[h.ToInt64()].pointer);
		}

		static int handle_get_size_proc(IntPtr h)
		{
#if DEBUG
			Ping(DebugFlags.HandleSuite, string.Format("Handle address = {0:X8}", h.ToInt64()));
#endif
			if (!handle_valid(h))
			{
				if (NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					return NativeMethods.GlobalSize(h).ToInt32();
				}
				else if (!NativeMethods.IsBadReadPtr(h, new UIntPtr((uint)IntPtr.Size))
					&& NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					return NativeMethods.GlobalSize(h).ToInt32();
				}
				else
				{
					return 0;
				}
			}

			return handles[h.ToInt64()].size;
		}

		static void handle_recover_space_proc(int size)
		{
#if DEBUG
			Ping(DebugFlags.HandleSuite, string.Format("size = {0}", size));
#endif
		}

		static short handle_set_size(IntPtr h, int newSize)
		{
#if DEBUG
			Ping(DebugFlags.HandleSuite, string.Format("Handle address = {0:X8}", h.ToInt64())); 
#endif
			if (!handle_valid(h))
			{
				if (NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					if ((h = NativeMethods.GlobalReAlloc(h, new UIntPtr((uint)newSize), 0U)) == IntPtr.Zero)
						return PSError.nilHandleErr;
					return PSError.noErr;
				}
				else if (!NativeMethods.IsBadReadPtr(h, new UIntPtr((uint)IntPtr.Size))
					&& NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					h = NativeMethods.GlobalReAlloc(h, new UIntPtr((uint)newSize), 0U);
					return PSError.noErr;
				}
				else
				{
					return PSError.nilHandleErr;
				}
			}

			try
			{
				PSHandle handle = new PSHandle() { pointer = Marshal.ReAllocHGlobal(h, new IntPtr(newSize)), size = newSize};

                if (handles[h.ToInt64()].size > 0)
                {
                    GC.RemoveMemoryPressure((long)handles[h.ToInt64()].size);
                }
                handles.Remove(h.ToInt64());

			    if (newSize > 0)
			    {
				    GC.AddMemoryPressure(newSize);
			    }
                h = handle.pointer;
                handles.Add(h.ToInt64(), handle);
			}
			catch (OutOfMemoryException)
			{
				return PSError.memFullErr;
			} 
		
			return PSError.noErr;
		}
		static void handle_unlock_proc(IntPtr h)
		{
#if DEBUG
			Ping(DebugFlags.HandleSuite, string.Format("Handle address = {0:X8}", h.ToInt64())); 
#endif
			if (!handle_valid(h))
			{
				if (NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					NativeMethods.GlobalUnlock(h);
					return;
				}
				else if (!NativeMethods.IsBadReadPtr(h, new UIntPtr((uint)UIntPtr.Size))
					&& NativeMethods.GlobalSize(h).ToInt64() > 0L)
				{
					NativeMethods.GlobalUnlock(h);
					return;
				}
				else
				{
					if (!NativeMethods.IsBadReadPtr(h, new UIntPtr((uint)UIntPtr.Size)) && NativeMethods.IsBadWritePtr(h, new UIntPtr((uint)UIntPtr.Size)))
					{
						return;
					}
				}
			}

			NativeMethods.GlobalUnlock(handles[h.ToInt64()].pointer);
		}

		static void host_proc(short selector, IntPtr data)
		{
#if DEBUG
			Ping(DebugFlags.MiscCallbacks, string.Format("{0} : {1}", selector, data)); 
#endif
		}

#if PSSDK_3_0_4
		static short image_services_interpolate_1d_proc(ref PSImagePlane source, ref PSImagePlane destination, ref Rect16 area, ref int coords, short method)
		{
			return PSError.memFullErr;
		}

		static short image_services_interpolate_2d_proc(ref PSImagePlane source, ref PSImagePlane destination, ref Rect16 area, ref int coords, short method)
		{
			return PSError.memFullErr;
		} 
#endif

		static void process_event_proc (IntPtr @event)
		{
		}
		static void progress_proc(int done, int total)
		{
			if (done < 0)
				done = 0;
#if DEBUG
			Ping(DebugFlags.MiscCallbacks, string.Format("Done = {0}, Total = {1}", done, total));
			Ping(DebugFlags.MiscCallbacks, string.Format("progress_proc = {0}", (((double)done / (double)total) * 100d).ToString())); 
#endif
			if (progressFunc != null)
			{
				progressFunc.Invoke(done, total);
			}
		}

		static short property_get_proc(uint signature, uint key, int index, ref int simpleProperty, ref System.IntPtr complexProperty)
		{
			if (signature != PSConstants.kPhotoshopSignature)
				return PSError.errPlugInHostInsufficient;

			if (key == PSConstants.propNumberOfChannels)
			{
				simpleProperty = 4;
			}
			else if (key == PSConstants.propImageMode)
			{
				simpleProperty = PSConstants.plugInModeRGBColor;
			}
			else
			{
				return PSError.errPlugInHostInsufficient;
			}

			return PSError.noErr;
		}

#if PSSDK_3_0_4
		static short property_set_proc(uint signature, uint key, int index, int simpleProperty, ref System.IntPtr complexProperty)
		{
			if (signature != PSConstants.kPhotoshopSignature)
				return PSError.errPlugInHostInsufficient;

			if (key == PSConstants.propNumberOfChannels)
			{
			}
			else if (key == PSConstants.propImageMode)
			{
			}
			else
			{
				return PSError.errPlugInHostInsufficient;
			}

			return PSError.noErr;
		} 
#endif

		static short resource_add_proc(uint ofType, ref IntPtr data)
		{
			return PSError.memFullErr;
		}

		static short resource_count_proc(uint ofType)
		{
			return 0;
		}

		static void resource_delete_proc(uint ofType, short index)
		{

		}
		static IntPtr resource_get_proc(uint ofType, short index)
		{
			return IntPtr.Zero;
		}
		/// <summary>
		/// Converts a long value to Photoshop's 'Fixed' type.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <returns>The converted value</returns>
		static int long2fixed(long value)
		{
			return (int)(value << 16);
		}

        static bool sizesSetup;
        static void setup_sizes()
        {
            if (sizesSetup)
                return;

            sizesSetup = true;

			filterRecord.imageSize.h = (short)source.Width;
			filterRecord.imageSize.v = (short)source.Height;

			if (ignoreAlpha)
			{
				filterRecord.planes = (short)3;
			}
			else
			{
				filterRecord.planes = (short)4;
			}

			filterRecord.floatCoord.h = (short)0;
			filterRecord.floatCoord.v = (short)0;
			filterRecord.filterRect.left = (short)0;
			filterRecord.filterRect.top = (short)0;
			filterRecord.filterRect.right = (short)source.Width;
			filterRecord.filterRect.bottom = (short)source.Height;

			filterRecord.imageHRes = long2fixed((long)(dpiX + 0.5));
			filterRecord.imageVRes = long2fixed((long)(dpiY + 0.5));

			filterRecord.wholeSize.h = (short)source.Width;
			filterRecord.wholeSize.v = (short)source.Height;
		}

		static void setup_delegates()
		{ 
			advanceProc = new AdvanceStateProc(advance_state_proc);
			// BufferProc
			allocProc = new AllocateBufferProc(allocate_buffer_proc);
			freeProc = new FreeBufferProc(buffer_free_proc);
			lockProc = new LockBufferProc(buffer_lock_proc);
			unlockProc = new UnlockBufferProc(buffer_unlock_proc);
			spaceProc = new BufferSpaceProc(buffer_space_proc);
			// Misc Callbacks
			colorProc = new ColorServicesProc(color_services_proc);
			displayPixelsProc = new DisplayPixelsProc(display_pixels_proc);
			hostProc = new HostProcs(host_proc);
			processEventProc = new ProcessEventProc(process_event_proc);
			progressProc = new ProgressProc(progress_proc);
			abortProc = new TestAbortProc(abort_proc);
			// HandleProc
			handleNewProc = new NewPIHandleProc(handle_new_proc);
			handleDisposeProc = new DisposePIHandleProc(handle_dispose_proc);
			handleGetSizeProc = new GetPIHandleSizeProc(handle_get_size_proc);
			handleSetSizeProc = new SetPIHandleSizeProc(handle_set_size);
			handleLockProc = new LockPIHandleProc(handle_lock_proc);
			handleRecoverSpaceProc = new RecoverSpaceProc(handle_recover_space_proc);
			handleUnlockProc = new UnlockPIHandleProc(handle_unlock_proc);
			
			// ImageServicesProc
#if PSSDK_3_0_4
			resample1DProc = new PIResampleProc(image_services_interpolate_1d_proc);
			resample2DProc = new PIResampleProc(image_services_interpolate_2d_proc); 
#endif

			// PropertyProc
			getPropertyProc = new GetPropertyProc(property_get_proc);

#if PSSDK_3_0_4
			setPropertyProc = new SetPropertyProc(property_set_proc);
#endif		
			// ResourceProcs
			countResourceProc = new CountPIResourcesProc(resource_count_proc);
			getResourceProc = new GetPIResourceProc(resource_get_proc);
			deleteResourceProc = new DeletePIResourceProc(resource_delete_proc);
			addResourceProc = new AddPIResourceProc(resource_add_proc);
		}

		static bool suitesSetup;
		static void setup_suites()
		{
			if (suitesSetup)
				return;

			suitesSetup = true;

			// BufferProcs
			buffer_proc = new BufferProcs();
			buffer_proc.bufferProcsVersion = PSConstants.kCurrentBufferProcsVersion;
			buffer_proc.numBufferProcs = PSConstants.kCurrentBufferProcsCount;
			buffer_proc.allocateProc = Marshal.GetFunctionPointerForDelegate(allocProc);
			buffer_proc.freeProc = Marshal.GetFunctionPointerForDelegate(freeProc);
			buffer_proc.lockProc = Marshal.GetFunctionPointerForDelegate(lockProc);
			buffer_proc.unlockProc = Marshal.GetFunctionPointerForDelegate(unlockProc);
			buffer_proc.spaceProc = Marshal.GetFunctionPointerForDelegate(spaceProc);
			buffer_procPtr = GCHandle.Alloc(buffer_proc, GCHandleType.Pinned);
			// HandleProc
			handle_procs = new HandleProcs();
			handle_procs.handleProcsVersion = PSConstants.kCurrentHandleProcsVersion;
			handle_procs.numHandleProcs = PSConstants.kCurrentHandleProcsCount;
			handle_procs.newProc = Marshal.GetFunctionPointerForDelegate(handleNewProc);
			handle_procs.disposeProc = Marshal.GetFunctionPointerForDelegate(handleDisposeProc);
			handle_procs.getSizeProc = Marshal.GetFunctionPointerForDelegate(handleGetSizeProc);
			handle_procs.lockProc = Marshal.GetFunctionPointerForDelegate(handleLockProc);
			handle_procs.setSizeProc = Marshal.GetFunctionPointerForDelegate(handleSetSizeProc);
			handle_procs.recoverSpaceProc = Marshal.GetFunctionPointerForDelegate(handleRecoverSpaceProc);
			handle_procs.unlockProc = Marshal.GetFunctionPointerForDelegate(handleUnlockProc);
			handle_procPtr = GCHandle.Alloc(handle_procs, GCHandleType.Pinned);
			// ImageServicesProc

#if PSSDK_3_0_4

			image_services_procs = new ImageServicesProcs();
			image_services_procs.imageServicesProcsVersion = PSConstants.kCurrentImageServicesProcsVersion;
			image_services_procs.numImageServicesProcs = PSConstants.kCurrentImageServicesProcsCount;
			image_services_procs.interpolate1DProc = Marshal.GetFunctionPointerForDelegate(resample1DProc);
			image_services_procs.interpolate2DProc = Marshal.GetFunctionPointerForDelegate(resample2DProc);

			image_services_procsPtr = GCHandle.Alloc(image_services_procs, GCHandleType.Pinned); 
#endif

			// PropertyProcs
#if PSSDK_3_0_4
			property_procs = new PropertyProcs();
			property_procs.propertyProcsVersion = PSConstants.kCurrentPropertyProcsVersion;
			property_procs.numPropertyProcs = PSConstants.kCurrentPropertyProcsCount;
			property_procs.getPropertyProc = Marshal.GetFunctionPointerForDelegate(getPropertyProc);

			property_procs.setPropertyProc = Marshal.GetFunctionPointerForDelegate(setPropertyProc);
			property_procsPtr = GCHandle.Alloc(property_procs, GCHandleType.Pinned);
#endif
			// ResourceProcs
			resource_procs = new ResourceProcs();
			resource_procs.resourceProcsVersion = PSConstants.kCurrentResourceProcsVersion;
			resource_procs.numResourceProcs = PSConstants.kCurrentResourceProcsCount;
			resource_procs.addProc = Marshal.GetFunctionPointerForDelegate(addResourceProc);
			resource_procs.countProc = Marshal.GetFunctionPointerForDelegate(countResourceProc);
			resource_procs.deleteProc = Marshal.GetFunctionPointerForDelegate(deleteResourceProc);
			resource_procs.getProc = Marshal.GetFunctionPointerForDelegate(getResourceProc);
			resource_procsPtr = GCHandle.Alloc(resource_procs, GCHandleType.Pinned);
		}
		static bool frsetup;
		static unsafe void setup_filter_record()
		{
			if (frsetup)
				return;

			frsetup = true;

			filterRecord = new FilterRecord();
			filterRecord.serial = 0;
			filterRecord.abortProc = Marshal.GetFunctionPointerForDelegate(abortProc);
			filterRecord.progressProc = Marshal.GetFunctionPointerForDelegate(progressProc);
			filterRecord.parameters = IntPtr.Zero;

			filterRecord.background.red = (ushort)((secondaryColor[0] * 65535) / 255); 
			filterRecord.background.green = (ushort)((secondaryColor[1] * 65535) / 255); 
			filterRecord.background.blue = (ushort)((secondaryColor[2] * 65535) / 255); 

			fixed (byte* backColor = filterRecord.backColor)
			{
				for (int i = 0; i < 4; i++)
				{
					backColor[i] = secondaryColor[i];
				}
			}

			filterRecord.foreground.red = (ushort)((primaryColor[0] * 65535) / 255); 
			filterRecord.foreground.green = (ushort)((primaryColor[1] * 65535) / 255);
			filterRecord.foreground.blue = (ushort)((primaryColor[2] * 65535) / 255);

			fixed (byte* foreColor = filterRecord.foreColor)
			{
				for (int i = 0; i < 4; i++)
				{
					foreColor[i] = primaryColor[i];
				}
			}

            filterRecord.bufferSpace = buffer_space_proc();

			filterRecord.maxSpace = 1000000000;
			filterRecord.hostSig = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(".PDN"), 0);
			filterRecord.hostProcs = Marshal.GetFunctionPointerForDelegate(hostProc);
			filterRecord.platformData = platFormDataPtr.AddrOfPinnedObject();
			filterRecord.bufferProcs = buffer_procPtr.AddrOfPinnedObject();
			filterRecord.resourceProcs = resource_procsPtr.AddrOfPinnedObject();
			filterRecord.processEvent = Marshal.GetFunctionPointerForDelegate(processEventProc);
			filterRecord.displayPixels = Marshal.GetFunctionPointerForDelegate(displayPixelsProc);

			filterRecord.handleProcs = handle_procPtr.AddrOfPinnedObject();

			filterRecord.supportsDummyChannels = 0;
			filterRecord.supportsAlternateLayouts = 0;
			filterRecord.wantLayout = 0;
			filterRecord.filterCase = filterCase;
			filterRecord.dummyPlaneValue = -1;
			/* premiereHook */
			filterRecord.advanceState = Marshal.GetFunctionPointerForDelegate(advanceProc);

			filterRecord.supportsAbsolute = 1;
			filterRecord.wantsAbsolute = 0;
			filterRecord.getPropertyObsolete = Marshal.GetFunctionPointerForDelegate(getPropertyProc);
			/* cannotUndo */
			filterRecord.supportsPadding = 0;
			/* inputPadding */
			/* outputPadding */
			/* maskPadding */
			filterRecord.samplingSupport = 1;
			/* reservedByte */
			/* inputRate */
			/* maskRate */			
			filterRecord.colorServices = Marshal.GetFunctionPointerForDelegate(colorProc);

#if PSSDK_3_0_4
			filterRecord.imageServicesProcs = image_services_procsPtr.AddrOfPinnedObject();

			filterRecord.propertyProcs = property_procsPtr.AddrOfPinnedObject();
			filterRecord.inTileHeight = 0;
			filterRecord.inTileWidth = 0;
			filterRecord.inTileOrigin.h = 0;
			filterRecord.inTileOrigin.v = 0;
			filterRecord.absTileHeight = 0;
			filterRecord.absTileWidth = 0;
			filterRecord.absTileOrigin.h = 0;
			filterRecord.absTileOrigin.v = 0;
			filterRecord.outTileHeight = 0;
			filterRecord.outTileWidth = 0;
			filterRecord.outTileOrigin.h = 0;
			filterRecord.outTileOrigin.v = 0;
			filterRecord.maskTileHeight = 0;
			filterRecord.maskTileWidth = 0;
			filterRecord.maskTileOrigin.h = 0;
			filterRecord.maskTileOrigin.v = 0; 
#endif
#if PSDDK4 
			filterRecord.descriptorParameters = IntPtr.Zero;
			filterRecord.channelPortProcs = IntPtr.Zero;
			filterRecord.documentInfo = IntPtr.Zero;
#endif
			filterRecordPtr = GCHandle.Alloc(filterRecord, GCHandleType.Pinned);
		}

		#region IDisposable Members

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		private bool disposed;
		private void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
                    if (platFormDataPtr.IsAllocated)
                    {
                        platFormDataPtr.Free();
                    }
					if (buffer_procPtr.IsAllocated)
					{
						buffer_procPtr.Free();
					}
					if (handle_procPtr.IsAllocated)
					{
						handle_procPtr.Free();
					}

#if PSSDK_3_0_4
					if (image_services_procsPtr.IsAllocated)
					{
						image_services_procsPtr.Free();
					}
					if (property_procsPtr.IsAllocated)
					{
						property_procsPtr.Free();
					} 
#endif
	
					if (resource_procsPtr.IsAllocated)
					{
						resource_procsPtr.Free();
					}

                    if (filterRecord.parameters != IntPtr.Zero)
                    {
                        if (handle_valid(filterRecord.parameters))
                        {
                            handle_unlock_proc(filterRecord.parameters);
                            handle_dispose_proc(filterRecord.parameters);
                        }
                        else
                        {
                            NativeMethods.GlobalUnlock(filterRecord.parameters);
                            NativeMethods.GlobalFree(filterRecord.parameters);
                        }

                        filterRecord.parameters = IntPtr.Zero;
                    }

                    if (src_valid)
                    {
                        Marshal.FreeHGlobal(filterRecord.inData);
                        filterRecord.inData = IntPtr.Zero;
                        src_valid = false;
                    }

                    if (dst_valid)
                    {
                        Marshal.FreeHGlobal(filterRecord.outData);
                        filterRecord.outData = IntPtr.Zero;
                        dst_valid = false;
                    }


					if (filterRecordPtr.IsAllocated)
					{
						filterRecordPtr.Free();
					}
					progressFunc = null;

                    if (data != IntPtr.Zero)
                    {
                        if (handle_valid(data))
                        {
                            handle_unlock_proc(data);
                            handle_dispose_proc(data);
                        }
                        else if (NativeMethods.GlobalSize(data).ToInt64() > 0L)
                        {
                            NativeMethods.GlobalUnlock(data);
                            NativeMethods.GlobalFree(data);
                        } 		
                        data = IntPtr.Zero;
                    }

					suitesSetup = false;
					frsetup = false;


					if (source != null)
					{
						source.Dispose();
						source = null;
					}
					if (dest != null)
					{
						dest.Dispose();
						dest = null;
					}
                    if (checkerBoardBitmap != null)
                    {
                        checkerBoardBitmap.Dispose();
                        checkerBoardBitmap = null;
                    }

					disposed = true;
				}
			}
		}

		#endregion
	}
}
