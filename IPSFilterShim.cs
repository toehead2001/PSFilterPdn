﻿using System.ServiceModel;
using System.Drawing;
using PSFilterLoad.PSApi;

[ServiceContract(Namespace = "http://PSFilterPdn.shimData")]
internal interface IPSFilterShim
{
    [OperationContract]
    bool AbortFilter();
    
    [OperationContract]
    bool IsRepeatEffect();
    
    [OperationContract]
    bool ShowAboutDialog();

    [OperationContract]
    Rectangle GetFilterRect();
    
    [OperationContract]
    PluginData GetPluginData();
       
    [OperationContract]
    System.IntPtr GetWindowHandle();

    [OperationContract]
    Color GetPrimaryColor();

    [OperationContract]
    Color GetSecondaryColor();

    [OperationContract]
    PSFilterPdn.RegionDataWrapper GetSelectedRegion();

    [OperationContract]
    ParameterData GetFilterParameters();

    [OperationContract(IsOneWay = true)]
    void SetProxyErrorMessage(string errorMessage);

    [OperationContract(IsOneWay = true)]
    void SetProxyFilterParamters(ParameterData filterParameters);


}