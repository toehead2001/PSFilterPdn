﻿using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;
using PSFilterLoad.PSApi;
using System.Drawing;

namespace PSFilterPdn
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    internal sealed class PSFilterShimService : IPSFilterShim
    {
        private Func<byte> abortFunc;
        internal bool isRepeatEffect;
        internal bool showAboutDialog;
        internal string sourceFileName;
        internal string destFileName;
        internal PluginData pluginData;
        internal IntPtr parentHandle;
        internal Rectangle filterRect;
        internal Color primary;
        internal Color secondary;
        internal string regionFileName;
        internal string parameterDataFileName;
        internal string resourceFileName;
        internal Action<string> errorCallback;
        internal Action<int,int> progressCallback;

        public PSFilterShimService() : this(null)
        {
        }

        public PSFilterShimService(Func<byte> abort)
        {
            this.abortFunc = abort;
            this.isRepeatEffect = false;
            this.showAboutDialog = false;
            this.pluginData = null;
            this.parentHandle = IntPtr.Zero;
            this.filterRect = Rectangle.Empty;
            this.primary = Color.Black;
            this.secondary = Color.White;
            this.regionFileName = string.Empty;
            this.errorCallback = null;
            this.progressCallback = null;
        }

        public byte AbortFilter()
        {
            if (abortFunc != null)
            {
                return abortFunc();
            }

            return 0;
        }

        public bool IsRepeatEffect()
        {
            return isRepeatEffect;
        }

        public bool ShowAboutDialog()
        {
            return showAboutDialog;
        }

        public string GetSoureImagePath()
        {
            return sourceFileName;
        }

        public string GetDestImagePath()
        {
            return destFileName;
        }
        
        public Rectangle GetFilterRect()
        {
            return filterRect;
        }

        public IntPtr GetWindowHandle()
        {
            return parentHandle;
        }
        
        public PluginData GetPluginData()
        {
            return pluginData;
        }

        public Color GetPrimaryColor()
        {
            return primary;
        }

        public Color GetSecondaryColor()
        {
            return secondary;
        }

        public string GetRegionDataPath()
        {
            return regionFileName;
        }  
        
        public string GetParameterDataPath()
        {
           return parameterDataFileName;
        }

        public string GetPseudoResourcePath()
        {
            return resourceFileName;
        }

        public void SetProxyErrorMessage(string errorMessage)
        {
            errorCallback.Invoke(errorMessage);
        }

        public void UpdateFilterProgress(int done, int total)
        {
            if (progressCallback != null)
            {
                progressCallback.Invoke(done, total);
            }
        }


     
    }

    // Adapted from: http://www.jmedved.com/2010/03/named-pipes-in-wcf/ 
    static class PSFilterShimServer
    {

        private static readonly Uri ServiceUri = new Uri("net.pipe://localhost/PSFilterShim");
        private static readonly string PipeName = "ShimData";

        private static PSFilterShimService _service = null;
        private static ServiceHost _host = null;

        /// <summary>
        /// Starts the WCF server service.
        /// </summary>
        /// <param name="service">The service instance to use.</param>
        public static void Start(PSFilterShimService service)
        {
            _service = service;

            _host = new ServiceHost(_service, ServiceUri);
            _host.AddServiceEndpoint(typeof(IPSFilterShim), new NetNamedPipeBinding(), PipeName);
            _host.Open();
        }

        /// <summary>
        /// Stops the WCF server instance.
        /// </summary>
        public static void Stop()
        {
            if ((_host != null) && (_host.State != CommunicationState.Closed)) 
            {
                _host.Close();
                _host = null;
            }
        }
    }

}
