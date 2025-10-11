using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
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
        List<object> events = new List<object>();
        static AsyncPackage package_;
        public static async Task InitializeAsync(AsyncPackage package, DTE2 dte)
        {
            package_ = package;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            Instance = new DocumentManager(dte);
        }
        private DocumentManager(DTE2 dte)
        {
            _dte = dte;
            var docEvent = dte.Events.DocumentEvents;
            // add event to list so that GC does not remove it
            events.Add(docEvent);

            //docEvent.DocumentOpened += (document) =>
            //{

            //    Console.Write("document was opened!");
            //};
            docEvent.DocumentOpened += OnDocumentOpened;
            docEvent.DocumentOpening += OnDocumentOpening;
        }

        string path;
        private void OnDocumentOpening(string DocumentPath, bool ReadOnly)
        {
            path = DocumentPath;
        }


        private void OnDocumentOpened(Document doc)
        {
            ThreadHelper.ThrowIfNotOnUIThread();


            //Task.Run(async () =>
            //{
            //    await Task.Delay(80);
            //    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package_.DisposalToken);

                var windows = _dte.Windows.Cast<Window>().Reverse().ToList();
                var trash = windows.Select(x => x.Left).ToArray();
                foreach (var currentWindow in windows)
                {
                    if (currentWindow == null || currentWindow.Type != vsWindowType.vsWindowTypeDocument)
                        continue;

                    if (currentWindow.Document.FullName == doc.FullName)
                        continue;

                    if (currentWindow.Left == 0 && currentWindow.Top == 0)
                        currentWindow.Close(vsSaveChanges.vsSaveChangesPrompt);
                    continue;

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


            //});


        }
    }
}
