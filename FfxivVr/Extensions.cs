using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVR
{
    static class Extensions
    {
        internal static void CheckResult(this Result result, string action)
        {
            if (result != Result.Success)
            {
                throw new Exception($"Failed to {action} got result {result}");
            }
        }

        internal static void LogResult(this Result result, string action, Logger logger)
        {
            if (result != Result.Success)
            {
                logger.Error($"Failed to {action} got result {result}");
            }
        }

        internal static unsafe void SetApplicationName(this ref ApplicationInfo applicationInfo, string applicationName)
        {
            fixed (byte* p = applicationInfo.ApplicationName)
            {
                Native.WriteCString(p, applicationName, 128);
            }
        }

        internal static unsafe string GetRuntimeName(this InstanceProperties instanceProperties)
        {
            return Native.ReadCString(instanceProperties.RuntimeName);
        }
    }
}
