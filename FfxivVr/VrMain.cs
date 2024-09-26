using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVr
{
    public class VrMain
    {
        public unsafe void Initialize()
        {
            var appInfo = createAppInfo();

            InstanceCreateInfo info = new InstanceCreateInfo();
            info.CreateFlags = 0;
            info.ApplicationInfo = appInfo;
            Instance xrInstance;
            var api = XR.GetApi();
            api.CreateInstance(&info, &xrInstance);
        }

        private static unsafe ApplicationInfo createAppInfo()
        {
            ApplicationInfo appInfo = new ApplicationInfo();
            var bytes = Encoding.UTF8.GetBytes("FFXIV VR");
            for (int i = 0; i < bytes.Length; i++)
            {
                appInfo.ApplicationName[i] = bytes[i];
            }
            appInfo.ApplicationName[bytes.Length] = 0;
            appInfo.EngineVersion = 1;
            appInfo.ApiVersion = XR.CurrentLoaderRuntimeVersion;

            return appInfo;
        }
    }
}
