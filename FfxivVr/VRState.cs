using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVR
{
    internal class VRState
    {
        public SessionState State = SessionState.Unknown;
        public bool SessionRunning = false;
        public bool Exiting = false;

        internal bool IsActive()
        {
            return State == SessionState.Synchronized || State == SessionState.Visible || State == SessionState.Focused ;
        }
    }
}
