using System;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace OneTabPerSplit
{
    public class DocumentManager
    {

        public static DocumentManager Instance
        {
            get;
            private set;
        }
        private readonly DTE2 _dte;

        public static async Task InitializeAsync(AsyncPackage package, DTE2 dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            Instance = new DocumentManager(dte);
        }
        private DocumentManager(DTE2 dte)
        {
            _dte = dte;
            _dte.Events.DocumentEvents.DocumentOpened += OnDocumentOpened;
        }

        private void OnDocumentOpened(Document doc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var currentWindow = _dte.ActiveWindow;
            if (currentWindow == null || currentWindow.Type != vsWindowType.vsWindowTypeDocument)
                return;

            var currentDocName = doc.FullName;

            var windowsInSameGroup = _dte.Windows
                .OfType<Window>()
                .Where(w => w.Type == vsWindowType.vsWindowTypeDocument &&
                            w.LinkedWindows != null &&
                            w.LinkedWindows.OfType<Window>().Contains(currentWindow) &&
                            w.Document?.FullName != currentDocName)
                .ToList();

            foreach (var win in windowsInSameGroup)
            {
                try
                {
                    win.Close(vsSaveChanges.vsSaveChangesNo);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error closing window: " + ex.Message);
                }
            }
        }
    }
}
