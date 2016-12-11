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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PSFilterLoad.PSApi
{
    internal sealed class ActionDescriptorSuite
    {
        private sealed class ScriptingParameters
        {
            private Dictionary<uint, AETEValue> parameters;
            private List<uint> keys;

            public ScriptingParameters()
            {
                this.parameters = new Dictionary<uint, AETEValue>();
                this.keys = new List<uint>();
            }
            public ScriptingParameters(IDictionary<uint, AETEValue> dict)
            {
                this.parameters = new Dictionary<uint, AETEValue>(dict);
                this.keys = new List<uint>(dict.Keys);
            }

            public int Count
            {
                get
                {
                    return this.parameters.Count;
                }
            }

            public void Add(uint key, AETEValue value)
            {
                if (this.parameters.ContainsKey(key))
                {
                    this.parameters[key] = value;
                }
                else
                {
                    this.parameters.Add(key, value);
                    this.keys.Add(key);
                }
            }

            public void Clear()
            {
                this.parameters.Clear();
                this.keys.Clear();
            }

            public bool ContainsKey(uint key)
            {
                return this.parameters.ContainsKey(key);
            }

            public uint GetKeyAtIndex(int index)
            {
                return this.keys[index];
            }

            public void Remove(uint key)
            {
                this.parameters.Remove(key);
                this.keys.Remove(key);
            }

            public bool TryGetValue(uint key, out AETEValue value)
            {
                return this.parameters.TryGetValue(key, out value);
            }

            public Dictionary<uint, AETEValue> ToDictionary()
            {
                return new Dictionary<uint, AETEValue>(this.parameters);
            }
        }

        private AETEData aete;

        private Dictionary<IntPtr, ScriptingParameters> actionDescriptors;
        private Dictionary<IntPtr, ScriptingParameters> descriptorHandles;

        #region Callbacks
        private readonly ActionDescriptorMake make;
        private readonly ActionDescriptorFree free;
        private readonly ActionDescriptorHandleToDescriptor handleToDescriptor;
        private readonly ActionDescriptorAsHandle asHandle;
        private readonly ActionDescriptorGetType getType;
        private readonly ActionDescriptorGetKey getKey;
        private readonly ActionDescriptorHasKey hasKey;
        private readonly ActionDescriptorGetCount getCount;
        private readonly ActionDescriptorIsEqual isEqual;
        private readonly ActionDescriptorErase erase;
        private readonly ActionDescriptorClear clear;
        private readonly ActionDescriptorHasKeys hasKeys;
        private readonly ActionDescriptorPutInteger putInteger;
        private readonly ActionDescriptorPutFloat putFloat;
        private readonly ActionDescriptorPutUnitFloat putUnitFloat;
        private readonly ActionDescriptorPutString putString;
        private readonly ActionDescriptorPutBoolean putBoolean;
        private readonly ActionDescriptorPutList putList;
        private readonly ActionDescriptorPutObject putObject;
        private readonly ActionDescriptorPutGlobalObject putGlobalObject;
        private readonly ActionDescriptorPutEnumerated putEnumerated;
        private readonly ActionDescriptorPutReference putReference;
        private readonly ActionDescriptorPutClass putClass;
        private readonly ActionDescriptorPutGlobalClass putGlobalClass;
        private readonly ActionDescriptorPutAlias putAlias;
        private readonly ActionDescriptorPutIntegers putIntegers;
        private readonly ActionDescriptorPutZString putZString;
        private readonly ActionDescriptorPutData putData;
        private readonly ActionDescriptorGetInteger getInteger;
        private readonly ActionDescriptorGetFloat getFloat;
        private readonly ActionDescriptorGetUnitFloat getUnitFloat;
        private readonly ActionDescriptorGetStringLength getStringLength;
        private readonly ActionDescriptorGetString getString;
        private readonly ActionDescriptorGetBoolean getBoolean;
        private readonly ActionDescriptorGetList getList;
        private readonly ActionDescriptorGetObject getObject;
        private readonly ActionDescriptorGetGlobalObject getGlobalObject;
        private readonly ActionDescriptorGetEnumerated getEnumerated;
        private readonly ActionDescriptorGetReference getReference;
        private readonly ActionDescriptorGetClass getClass;
        private readonly ActionDescriptorGetGlobalClass getGlobalClass;
        private readonly ActionDescriptorGetAlias getAlias;
        private readonly ActionDescriptorGetIntegers getIntegers;
        private readonly ActionDescriptorGetZString getZString;
        private readonly ActionDescriptorGetDataLength getDataLength;
        private readonly ActionDescriptorGetData getData;
        #endregion

        public AETEData Aete
        {
            set
            {
                this.aete = value;
            }
        }

        public ActionDescriptorSuite()
        {
            this.make = new ActionDescriptorMake(Make);
            this.free = new ActionDescriptorFree(Free);
            this.handleToDescriptor = new ActionDescriptorHandleToDescriptor(HandleToDescriptor);
            this.asHandle = new ActionDescriptorAsHandle(AsHandle);
            this.getType = new ActionDescriptorGetType(GetType);
            this.getKey = new ActionDescriptorGetKey(GetKey);
            this.hasKey = new ActionDescriptorHasKey(HasKey);
            this.getCount = new ActionDescriptorGetCount(GetCount);
            this.isEqual = new ActionDescriptorIsEqual(IsEqual);
            this.erase = new ActionDescriptorErase(Erase);
            this.clear = new ActionDescriptorClear(Clear);
            this.hasKeys = new ActionDescriptorHasKeys(HasKeys);
            this.putInteger = new ActionDescriptorPutInteger(PutInteger);
            this.putFloat = new ActionDescriptorPutFloat(PutFloat);
            this.putUnitFloat = new ActionDescriptorPutUnitFloat(PutUnitFloat);
            this.putString = new ActionDescriptorPutString(PutString);
            this.putBoolean = new ActionDescriptorPutBoolean(PutBoolean);
            this.putList = new ActionDescriptorPutList(PutList);
            this.putObject = new ActionDescriptorPutObject(PutObject);
            this.putGlobalObject = new ActionDescriptorPutGlobalObject(PutGlobalObject);
            this.putEnumerated = new ActionDescriptorPutEnumerated(PutEnumerated);
            this.putReference = new ActionDescriptorPutReference(PutReference);
            this.putClass = new ActionDescriptorPutClass(PutClass);
            this.putGlobalClass = new ActionDescriptorPutGlobalClass(PutGlobalClass);
            this.putAlias = new ActionDescriptorPutAlias(PutAlias);
            this.putIntegers = new ActionDescriptorPutIntegers(PutIntegers);
            this.putZString = new ActionDescriptorPutZString(PutZString);
            this.putData = new ActionDescriptorPutData(PutData);
            this.getInteger = new ActionDescriptorGetInteger(GetInteger);
            this.getFloat = new ActionDescriptorGetFloat(GetFloat);
            this.getUnitFloat = new ActionDescriptorGetUnitFloat(GetUnitFloat);
            this.getStringLength = new ActionDescriptorGetStringLength(GetStringLength);
            this.getString = new ActionDescriptorGetString(GetString);
            this.getBoolean = new ActionDescriptorGetBoolean(GetBoolean);
            this.getList = new ActionDescriptorGetList(GetList);
            this.getObject = new ActionDescriptorGetObject(GetObject);
            this.getGlobalObject = new ActionDescriptorGetGlobalObject(GetGlobalObject);
            this.getEnumerated = new ActionDescriptorGetEnumerated(GetEnumerated);
            this.getReference = new ActionDescriptorGetReference(GetReference);
            this.getClass = new ActionDescriptorGetClass(GetClass);
            this.getGlobalClass = new ActionDescriptorGetGlobalClass(GetGlobalClass);
            this.getAlias = new ActionDescriptorGetAlias(GetAlias);
            this.getIntegers = new ActionDescriptorGetIntegers(GetIntegers);
            this.getZString = new ActionDescriptorGetZString(GetZString);
            this.getDataLength = new ActionDescriptorGetDataLength(GetDataLength);
            this.getData = new ActionDescriptorGetData(GetData);

            this.aete = null;
            this.actionDescriptors = new Dictionary<IntPtr, ScriptingParameters>(IntPtrEqualityComparer.Instance);
            this.descriptorHandles = new Dictionary<IntPtr, ScriptingParameters>(IntPtrEqualityComparer.Instance);
        }

        public PSActionDescriptorProc CreateActionDescriptorSuite2()
        {
            PSActionDescriptorProc suite = new PSActionDescriptorProc();
            suite.Make = Marshal.GetFunctionPointerForDelegate(this.make);
            suite.Free = Marshal.GetFunctionPointerForDelegate(this.free);
            suite.GetType = Marshal.GetFunctionPointerForDelegate(this.getType);
            suite.GetKey = Marshal.GetFunctionPointerForDelegate(this.getKey);
            suite.HasKey = Marshal.GetFunctionPointerForDelegate(this.hasKey);
            suite.GetCount = Marshal.GetFunctionPointerForDelegate(this.getCount);
            suite.IsEqual = Marshal.GetFunctionPointerForDelegate(this.isEqual);
            suite.Erase = Marshal.GetFunctionPointerForDelegate(this.erase);
            suite.Clear = Marshal.GetFunctionPointerForDelegate(this.clear);
            suite.PutInteger = Marshal.GetFunctionPointerForDelegate(this.putInteger);
            suite.PutFloat = Marshal.GetFunctionPointerForDelegate(this.putFloat);
            suite.PutUnitFloat = Marshal.GetFunctionPointerForDelegate(this.putUnitFloat);
            suite.PutString = Marshal.GetFunctionPointerForDelegate(this.putString);
            suite.PutBoolean = Marshal.GetFunctionPointerForDelegate(this.putBoolean);
            suite.PutList = Marshal.GetFunctionPointerForDelegate(this.putList);
            suite.PutObject = Marshal.GetFunctionPointerForDelegate(this.putObject);
            suite.PutGlobalObject = Marshal.GetFunctionPointerForDelegate(this.putGlobalObject);
            suite.PutEnumerated = Marshal.GetFunctionPointerForDelegate(this.putEnumerated);
            suite.PutReference = Marshal.GetFunctionPointerForDelegate(this.putReference);
            suite.PutClass = Marshal.GetFunctionPointerForDelegate(this.putClass);
            suite.PutGlobalClass = Marshal.GetFunctionPointerForDelegate(this.putGlobalClass);
            suite.PutAlias = Marshal.GetFunctionPointerForDelegate(this.putAlias);
            suite.GetInteger = Marshal.GetFunctionPointerForDelegate(this.getInteger);
            suite.GetFloat = Marshal.GetFunctionPointerForDelegate(this.getFloat);
            suite.GetUnitFloat = Marshal.GetFunctionPointerForDelegate(this.getUnitFloat);
            suite.GetStringLength = Marshal.GetFunctionPointerForDelegate(this.getStringLength);
            suite.GetString = Marshal.GetFunctionPointerForDelegate(this.getString);
            suite.GetBoolean = Marshal.GetFunctionPointerForDelegate(this.getBoolean);
            suite.GetList = Marshal.GetFunctionPointerForDelegate(this.getList);
            suite.GetObject = Marshal.GetFunctionPointerForDelegate(this.getObject);
            suite.GetGlobalObject = Marshal.GetFunctionPointerForDelegate(this.getGlobalObject);
            suite.GetEnumerated = Marshal.GetFunctionPointerForDelegate(this.getEnumerated);
            suite.GetReference = Marshal.GetFunctionPointerForDelegate(this.getReference);
            suite.GetClass = Marshal.GetFunctionPointerForDelegate(this.getClass);
            suite.GetGlobalClass = Marshal.GetFunctionPointerForDelegate(this.getGlobalClass);
            suite.GetAlias = Marshal.GetFunctionPointerForDelegate(this.getAlias);
            suite.HasKeys = Marshal.GetFunctionPointerForDelegate(this.hasKeys);
            suite.PutIntegers = Marshal.GetFunctionPointerForDelegate(this.putIntegers);
            suite.GetIntegers = Marshal.GetFunctionPointerForDelegate(this.getIntegers);
            suite.AsHandle = Marshal.GetFunctionPointerForDelegate(this.asHandle);
            suite.HandleToDescriptor = Marshal.GetFunctionPointerForDelegate(this.handleToDescriptor);
            suite.PutZString = Marshal.GetFunctionPointerForDelegate(this.putZString);
            suite.GetZString = Marshal.GetFunctionPointerForDelegate(this.getZString);
            suite.PutData = Marshal.GetFunctionPointerForDelegate(this.putData);
            suite.GetDataLength = Marshal.GetFunctionPointerForDelegate(this.getDataLength);
            suite.GetData = Marshal.GetFunctionPointerForDelegate(this.getData);

            return suite;
        }

        public bool TryGetScriptingData(IntPtr descriptorHandle, out Dictionary<uint, AETEValue> scriptingData)
        {
            scriptingData = null;

            ScriptingParameters parameters;
            if (this.descriptorHandles.TryGetValue(descriptorHandle, out parameters))
            {
                scriptingData = parameters.ToDictionary();

                return true;
            }

            return false;
        }

        public unsafe void SetScriptingData(PIDescriptorParameters* descriptorParameters, Dictionary<uint, AETEValue> scriptingData)
        {
            if (descriptorParameters->descriptor != IntPtr.Zero)
            {
                this.descriptorHandles.Add(descriptorParameters->descriptor, new ScriptingParameters(scriptingData));
            }
        }

        private IntPtr GenerateDictionaryKey()
        {
            return new IntPtr(this.actionDescriptors.Count + 1);
        }

        private int Make(ref IntPtr descriptor)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("descriptor: 0x{0}", descriptor.ToHexString()));
#endif
            try
            {
                descriptor = GenerateDictionaryKey();
                this.actionDescriptors.Add(descriptor, new ScriptingParameters());
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }

            return PSError.kSPNoError;
        }

        private int Free(IntPtr descriptor)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("descriptor: 0x{0}", descriptor.ToHexString()));
#endif
            this.actionDescriptors.Remove(descriptor);

            return PSError.kSPNoError;
        }

        private int HandleToDescriptor(IntPtr handle, ref IntPtr descriptor)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("handle: 0x{0}", handle.ToHexString()));
#endif
            ScriptingParameters parameters;
            if (this.descriptorHandles.TryGetValue(handle, out parameters))
            {
                try
                {
                    descriptor = GenerateDictionaryKey();
                    this.actionDescriptors.Add(descriptor, parameters);
                }
                catch (OutOfMemoryException)
                {
                    return PSError.memFullErr;
                }

                return PSError.kSPNoError;
            }

            return PSError.kSPBadParameterError;
        }

        private int AsHandle(IntPtr descriptor, ref IntPtr handle)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("descriptor: 0x{0}", descriptor.ToHexString()));
#endif
            handle = HandleSuite.Instance.NewHandle(1);
            if (handle == IntPtr.Zero)
            {
                return PSError.memFullErr;
            }
            this.descriptorHandles.Add(handle, this.actionDescriptors[descriptor]);

            return PSError.kSPNoError;
        }

        private int GetType(IntPtr descriptor, uint key, ref uint type)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item = null;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                // If the value is a sub-descriptor it must be retrieved with GetObject.
                if (item.Value is ScriptingParameters)
                {
                    type = DescriptorTypes.typeObject;
                }
                else
                {
                    type = item.Type;
                }

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetKey(IntPtr descriptor, uint index, ref uint key)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("index: {0}", index));
#endif
            ScriptingParameters parameters = this.actionDescriptors[descriptor];

            if (index >= 0 && index < parameters.Count)
            {
                key = parameters.GetKeyAtIndex((int)index);
                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int HasKey(IntPtr descriptor, uint key, ref byte hasKey)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            hasKey = this.actionDescriptors[descriptor].ContainsKey(key) ? (byte)1 : (byte)0;

            return PSError.kSPNoError;
        }

        private int GetCount(IntPtr descriptor, ref uint count)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Empty);
#endif
            ScriptingParameters parameters = this.actionDescriptors[descriptor];

            count = (uint)parameters.Count;

            return PSError.kSPNoError;
        }

        private int IsEqual(IntPtr firstDescriptor, IntPtr secondDescriptor, ref byte isEqual)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Empty);
#endif
            isEqual = 0;

            return PSError.kSPNotImplmented;
        }

        private int Erase(IntPtr descriptor, uint key)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            this.actionDescriptors[descriptor].Remove(key);

            return PSError.kSPNoError;
        }

        private int Clear(IntPtr descriptor)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Empty);
#endif
            this.actionDescriptors[descriptor].Clear();

            return PSError.kSPNoError;
        }

        private int HasKeys(IntPtr descriptor, IntPtr keyArray, ref byte hasKeys)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Empty);
#endif
            if (keyArray != IntPtr.Zero)
            {
                ScriptingParameters parameters = this.actionDescriptors[descriptor];
                bool result = true;

                unsafe
                {
                    uint* key = (uint*)keyArray.ToPointer();

                    while (*key != 0U)
                    {
                        if (!parameters.ContainsKey(*key))
                        {
                            result = false;
                            break;
                        }

                        key++;
                    }
                }
                hasKeys = result ? (byte)1 : (byte)0;
            }
            else
            {
                return PSError.kSPBadParameterError;
            }
           
            return PSError.kSPNoError;
        }

        #region  Descriptor write methods
        private int GetAETEParamFlags(uint key)
        {
            if (aete != null)
            {
                foreach (var item in aete.FlagList)
                {
                    if (item.Key == key)
                    {
                        return item.Value;
                    }
                }

            }

            return 0;
        }

        private int PutInteger(IntPtr descriptor, uint key, int data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            try
            {
                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeInteger, GetAETEParamFlags(key), 0, data));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }
            return PSError.kSPNoError;
        }

        private int PutFloat(IntPtr descriptor, uint key, double data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            try
            {
                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeFloat, GetAETEParamFlags(key), 0, data));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }
            return PSError.kSPNoError;
        }

        private int PutUnitFloat(IntPtr descriptor, uint key, uint unit, double data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            try
            {
                UnitFloat item = new UnitFloat(unit, data);

                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeUintFloat, GetAETEParamFlags(key), 0, item));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }
            return PSError.kSPNoError;
        }


        private int PutString(IntPtr descriptor, uint key, IntPtr cstrValue)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            if (cstrValue == IntPtr.Zero)
            {
                return PSError.kSPBadParameterError;
            }

            try
            {
                int length = SafeNativeMethods.lstrlenA(cstrValue);
                byte[] data = new byte[length];
                Marshal.Copy(cstrValue, data, 0, length);

                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeChar, GetAETEParamFlags(key), length, data));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }

            return PSError.kSPNoError;
        }

        private int PutBoolean(IntPtr descriptor, uint key, byte data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            try
            {
                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeBoolean, GetAETEParamFlags(key), 0, data));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }
            return PSError.kSPNoError;
        }

        private int PutList(IntPtr descriptor, uint key, IntPtr data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            return PSError.kSPNotImplmented;
        }

        private int PutObject(IntPtr descriptor, uint key, uint type, IntPtr descriptorHandle)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            // Attach the sub key to the parent descriptor.
            ScriptingParameters subKeys;
            if (this.actionDescriptors.TryGetValue(descriptorHandle, out subKeys))
            {
                try
                {
                    this.actionDescriptors[descriptor].Add(key, new AETEValue(type, GetAETEParamFlags(key), 0, subKeys));
                }
                catch (OutOfMemoryException)
                {
                    return PSError.memFullErr;
                }
            }
            else
            {
                return PSError.errMissingParameter;
            }

            return PSError.kSPNoError;
        }

        private int PutGlobalObject(IntPtr descriptor, uint key, uint type, IntPtr descriptorHandle)
        {
            return PutObject(descriptor, key, type, descriptorHandle);
        }

        private int PutEnumerated(IntPtr descriptor, uint key, uint type, uint data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            try
            {
                EnumeratedValue item = new EnumeratedValue(type, data);
                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeEnumerated, GetAETEParamFlags(key), 0, item));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }
            return PSError.kSPNoError;
        }

        private int PutReference(IntPtr descriptor, uint key, IntPtr value)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif

            return PSError.kSPNotImplmented;
        }

        private int PutClass(IntPtr descriptor, uint key, uint data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            try
            {
                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeClass, GetAETEParamFlags(key), 0, data));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }

            return PSError.kSPNoError;
        }

        private int PutGlobalClass(IntPtr descriptor, uint key, uint data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            try
            {
                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeGlobalClass, GetAETEParamFlags(key), 0, data));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }

            return PSError.kSPNoError;
        }

        private int PutAlias(IntPtr descriptor, uint key, IntPtr aliasHandle)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            IntPtr hPtr = HandleSuite.Instance.LockHandle(aliasHandle, 0);

            try
            {
                try
                {
                    int size = HandleSuite.Instance.GetHandleSize(aliasHandle);
                    byte[] data = new byte[size];
                    Marshal.Copy(hPtr, data, 0, size);

                    this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeAlias, GetAETEParamFlags(key), size, data));
                }
                finally
                {
                    HandleSuite.Instance.UnlockHandle(aliasHandle);
                }
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }
            return PSError.kSPNoError;
        }

        private int PutIntegers(IntPtr descriptor, uint key, uint count, IntPtr arrayPointer)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            if (arrayPointer == IntPtr.Zero)
            {
                return PSError.kSPBadParameterError;
            }

            try
            {
                int[] data = new int[count];

                unsafe
                {
                    int* ptr = (int*)arrayPointer;

                    for (int i = 0; i < count; i++)
                    {
                        data[i] = *ptr;
                        ptr++;
                    }
                }

                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeInteger, GetAETEParamFlags(key), 0, data));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }

            return PSError.kSPNoError;
        }

        private int PutZString(IntPtr descriptor, uint key, IntPtr zstring)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            try
            {
                ActionDescriptorZString value;
                if (PICA.ASZStringSuite.Instance.ConvertToActionDescriptor(zstring, out value))
                {
                    this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeChar, GetAETEParamFlags(key), 0, value));

                    return PSError.kSPNoError;
                }
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }

            return PSError.kSPBadParameterError;
        }

        private int PutData(IntPtr descriptor, uint key, int length, IntPtr blob)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            if (blob == IntPtr.Zero || length < 0)
            {
                return PSError.kSPBadParameterError;
            }

            try
            {
                byte[] data = new byte[length];

                Marshal.Copy(blob, data, 0, length);

                this.actionDescriptors[descriptor].Add(key, new AETEValue(DescriptorTypes.typeRawData, GetAETEParamFlags(key), length, data));
            }
            catch (OutOfMemoryException)
            {
                return PSError.memFullErr;
            }

            return PSError.kSPNoError;
        }
        #endregion

        #region Descriptor read methods
        private int GetInteger(IntPtr descriptor, uint key, ref int data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                data = (int)item.Value;

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetFloat(IntPtr descriptor, uint key, ref double data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                data = (double)item.Value;

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetUnitFloat(IntPtr descriptor, uint key, ref uint unit, ref double data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                UnitFloat unitFloat = (UnitFloat)item.Value;

                try
                {
                    unit = unitFloat.Unit;
                }
                catch (NullReferenceException)
                {
                }

                data = unitFloat.Value;

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetStringLength(IntPtr descriptor, uint key, ref uint length)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                byte[] bytes = (byte[])item.Value;

                length = (uint)bytes.Length;

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetString(IntPtr descriptor, uint key, IntPtr cstrValue, uint maxLength)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            if (cstrValue == IntPtr.Zero)
            {
                return PSError.kSPBadParameterError;
            }

            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                if (maxLength > 0)
                {
                    byte[] bytes = (byte[])item.Value;

                    // Ensure that the buffer has room for the null terminator.
                    int length = (int)Math.Min(bytes.Length, maxLength - 1);

                    Marshal.Copy(bytes, 0, cstrValue, length);
                    Marshal.WriteByte(cstrValue, length, 0);
                }
                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetBoolean(IntPtr descriptor, uint key, ref byte data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                data = (byte)item.Value;

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetList(IntPtr descriptor, uint key, ref IntPtr data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            return PSError.kSPNotImplmented;
        }

        private int GetObject(IntPtr descriptor, uint key, ref uint retType, ref IntPtr descriptorHandle)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                uint type = item.Type;

                try
                {
                    retType = type;
                }
                catch (NullReferenceException)
                {
                    // ignore it
                }

                ScriptingParameters parameters = item.Value as ScriptingParameters;
                if (parameters != null)
                {
                    descriptorHandle = GenerateDictionaryKey();
                    this.actionDescriptors.Add(descriptorHandle, parameters);

                    return PSError.kSPNoError;
                }
            }

            return PSError.errMissingParameter;
        }

        private int GetGlobalObject(IntPtr descriptor, uint key, ref uint retType, ref IntPtr descriptorHandle)
        {
            return GetObject(descriptor, key, ref retType, ref descriptorHandle);
        }

        private int GetEnumerated(IntPtr descriptor, uint key, ref uint type, ref uint data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                EnumeratedValue enumerated = (EnumeratedValue)item.Value;
                try
                {
                    type = enumerated.Type;
                }
                catch (NullReferenceException)
                {
                }

                data = enumerated.Value;

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetReference(IntPtr descriptor, uint key, ref IntPtr data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            return PSError.kSPNotImplmented;
        }

        private int GetClass(IntPtr descriptor, uint key, ref uint data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                data = (uint)item.Value;

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetGlobalClass(IntPtr descriptor, uint key, ref uint data)
        {
            return GetClass(descriptor, key, ref data);
        }

        private int GetAlias(IntPtr descriptor, uint key, ref IntPtr data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                int size = item.Size;
                data = HandleSuite.Instance.NewHandle(size);

                if (data == IntPtr.Zero)
                {
                    return PSError.memFullErr;
                }

                Marshal.Copy((byte[])item.Value, 0, HandleSuite.Instance.LockHandle(data, 0), size);
                HandleSuite.Instance.UnlockHandle(data);

                return PSError.kSPNoError; 
            }

            return PSError.errMissingParameter;
        }


        private int GetIntegers(IntPtr descriptor, uint key, uint count, IntPtr data)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            if (data == IntPtr.Zero)
            {
                return PSError.kSPBadParameterError;
            }

            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                int[] values = (int[])item.Value;
                if (count > values.Length)
                {
                    return PSError.kSPBadParameterError;
                }

                Marshal.Copy(values, 0, data, (int)count);

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetZString(IntPtr descriptor, uint key, ref IntPtr zstring)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif

            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                ActionDescriptorZString value = (ActionDescriptorZString)item.Value;

                try
                {
                    zstring = PICA.ASZStringSuite.Instance.CreateFromActionDescriptor(value);
                }
                catch (OutOfMemoryException)
                {
                    return PSError.memFullErr;
                }
                
                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetDataLength(IntPtr descriptor, uint key, ref int length)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif

            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                byte[] bytes = (byte[])item.Value;

                length = bytes.Length;

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }

        private int GetData(IntPtr descriptor, uint key, IntPtr blob)
        {
#if DEBUG
            DebugUtils.Ping(DebugFlags.DescriptorParameters, string.Format("key: 0x{0:X4}({1})", key, DebugUtils.PropToString(key)));
#endif
            if (blob == IntPtr.Zero)
            {
                return PSError.kSPBadParameterError;
            }

            AETEValue item;
            if (this.actionDescriptors[descriptor].TryGetValue(key, out item))
            {
                byte[] data = (byte[])item.Value;

                Marshal.Copy(data, 0, blob, data.Length);

                return PSError.kSPNoError;
            }

            return PSError.errMissingParameter;
        }
        #endregion
    }
}
