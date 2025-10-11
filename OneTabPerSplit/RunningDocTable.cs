using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OneTabPerSplit
{
    public class RunningDocTableEventHandler : IVsRunningDocTableEvents
    {

        public static RunningDocTableEventHandler Instance
        {
            get;
            private set;
        }
        private readonly IVsRunningDocumentTable _rdt;
        private readonly uint _cookie;

        public static async Task InitializeAsync(AsyncPackage package, IVsRunningDocumentTable rdt)
        {
            // Switch to the main thread - the call to AddCommand in ShrinkSelection's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            Instance = new RunningDocTableEventHandler(rdt);
        }
        public RunningDocTableEventHandler(IVsRunningDocumentTable rdt)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _rdt = rdt;
            _rdt.AdviseRunningDocTableEvents(this, out _cookie);
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;
        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocks, uint dwEditLocks) => VSConstants.S_OK;
        public int OnAfterSave(uint docCookie) => VSConstants.S_OK;
        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    EnforceOneTabPerSplit(pFrame);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OneTabPerSplit] Error: {ex.Message}");
                }
            });

            return VSConstants.S_OK;
        }

        public int OnBeforeSave(uint docCookie) => VSConstants.S_OK;

        private void EnforceOneTabPerSplit(IVsWindowFrame activeFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (activeFrame == null) return;

            var vsUIShell = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
            if (vsUIShell == null) return;

            vsUIShell.GetDocumentWindowEnum(out IEnumWindowFrames enumFrames);
            var frames = new IVsWindowFrame[1];

            while (enumFrames.Next(1, frames, out var fetched) == VSConstants.S_OK && fetched == 1)
            {
                if (!ReferenceEquals(frames[0], activeFrame))
                {
                    // Get tab group of this frame and compare to activeFrame
                    frames[0].GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object docView1);
                    activeFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out object docView2);

                    if (docView1 != null && docView2 != null &&
                        Equals(GetParentGroup(frames[0]), GetParentGroup(activeFrame)))
                    {
                        // Close the previous document in same split
                        frames[0].CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                    }
                }
            }
        }
        private const int VSFPROPID_ViewGroup = 11025;
        private object GetParentGroup(IVsWindowFrame frame)
        {
            frame.GetProperty(VSFPROPID_ViewGroup, out object group);
            return group;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }
    }
}