using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVr
{
    public class VrMain
    {
        private XR api = XR.GetApi();
        public unsafe void Initialize()
        {
            var appInfo = createAppInfo();

            InstanceCreateInfo info = new InstanceCreateInfo(createFlags: 0, applicationInfo: appInfo);

            uint count;
            CheckResult(api.EnumerateApiLayerProperties(0, &count, null), "EnumerateApiLayerProperties");
            ApiLayerProperties[] properties = Native.CreateArray(new ApiLayerProperties(next: null), count);

            System.Diagnostics.Debug.WriteLine($"property count {properties.Length} {count}");
            uint written = 0;
            CheckResult(api.EnumerateApiLayerProperties(count, &written, properties), "EnumerateApiLayerProperties");
            for (int i = 0; i < properties.Length; i++)
            {
                var prop = properties[i];
                System.Diagnostics.Debug.WriteLine($"property {Native.ByteToString(prop.Description)}");
            }
            Instance xrInstance = new Instance();
            CheckResult(api.CreateInstance(&info, &xrInstance), "CreateInstance");

            var instanceProperties = new InstanceProperties(next: null);
            CheckResult(api.GetInstanceProperties(xrInstance, &instanceProperties), "GetInstanceProperties");

            System.Diagnostics.Debug.WriteLine($"property {Native.ByteToString(instanceProperties.RuntimeName)} {instanceProperties.RuntimeVersion}");

            ulong systemId;
            var getInfo = new SystemGetInfo(next: null, formFactor: FormFactor.HeadMountedDisplay);
            var result = api.GetSystem(xrInstance, &getInfo, &systemId);
            if (result == Result.ErrorFormFactorUnavailable)
            {
                throw new Exception("Form factor unavailaable, check that headset is on");
            }
            CheckResult(result, "GetSystem");
            var binding = new GraphicsBindingD3D11KHR(
               device: null
          );

            var sessionInfo = new SessionCreateInfo(systemId: systemId, createFlags: 0, next: &binding);

            var session = new Session();
            CheckResult(api.CreateSession(xrInstance, sessionInfo, &session), "CreateSession");
        }

        private void CheckResult(Result result, String action)
        {
            if (result != Result.Success)
            {
                throw new Exception($"Failed: {result} when {action}");
            }
        }

        private static unsafe ApplicationInfo createAppInfo()
        {
            ApplicationInfo appInfo = new ApplicationInfo(applicationVersion: 1, engineVersion: 1, apiVersion: 1UL << 48);
            Native.Write(appInfo.ApplicationName, "FFXIV VR");

            return appInfo;
        }
    }
}
