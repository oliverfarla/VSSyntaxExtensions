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

        public int OnAfterDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (fFirstShow != 0)
            {
                // New document shown: enforce one-tab-per-split rule
                CloseOtherTabsInSameGroup(pFrame);
            }

            return VSConstants.S_OK;
        }

        private void CloseOtherTabsInSameGroup(IVsWindowFrame newFrame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var shell = Package.GetGlobalService(typeof(SVsUIShell)) as IVsUIShell;
            if (shell == null) return;

            shell.GetDocumentWindowEnum(out IEnumWindowFrames frameEnum);
            if (frameEnum == null) return;

            var targetGroup = GetTabGroupKey(newFrame);
            if (targetGroup == null) return;

            IVsWindowFrame[] frameArray = new IVsWindowFrame[1];
            uint fetched;
            while (frameEnum.Next(1, frameArray, out fetched) == VSConstants.S_OK && fetched == 1)
            {
                var frame = frameArray[0];

                if (!ReferenceEquals(frame, newFrame))
                {
                    var group = GetTabGroupKey(frame);
                    if (group != null && group.Equals(targetGroup))
                    {
                        frame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                    }
                }
            }
        }

        // Simplified grouping key based on tab index and parent
        private string GetTabGroupKey(IVsWindowFrame frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            frame.GetProperty(11025, out object tabIndex);
            frame.GetProperty(11027, out object parent);
            var key = $"{parent?.GetHashCode()}-{tabIndex?.GetHashCode()}";
            return key;
        }

        // Unused RDT events (you must still implement them)
        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;
        public int OnAfterSave(uint docCookie) => VSConstants.S_OK;
        public int OnBeforeDocumentWindowClose(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;
        public int OnBeforeSave(uint docCookie) => VSConstants.S_OK;

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }
    }
}