using EnvDTE;
using Microsoft.CodeAnalysis.Host;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace VSSyntaxExtensions
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.MainToolBarVisible_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FullSolutionLoading_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string, PackageAutoLoadFlags.BackgroundLoad)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(VSSyntaxExtensionsPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class VSSyntaxExtensionsPackage : AsyncPackage
    {
        /// <summary>
        /// VSSyntaxExtensionsPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "eb25b805-f265-4e2f-b255-cffcd0538ba0";

        #region Package Members


        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await ExpandSelection.InitializeAsync(this);
            await ExpandSelectionInner.InitializeAsync(this);
            await ExpandSelectionOuter.InitializeAsync(this);
            await MoveParameterRight.InitializeAsync(this);
            await MoveParameterLeft.InitializeAsync(this);
            //await SelectFunctionInner.InitializeAsync(this);
            ////await SelectFunctionOuter.InitializeAsync(this);
            //await SelectNodeListInner.InitializeAsync(this);
            await DialogTest.InitializeAsync(this);


            var componentModel = await this.GetServiceAsync<SComponentModel, IComponentModel>();
            var ws = componentModel.GetService<VisualStudioWorkspace>();
            ws.WorkspaceChanged += (o, e) =>
            {
                if (e.NewSolution.FilePath != currSol)
                {
                    System.IO.File.AppendAllLines(@"C:\temp\test.txt", new string[] { e.NewSolution.FilePath });
                    currSol = e.NewSolution.FilePath;
                    if (string.IsNullOrWhiteSpace(currSol))
                    {
                        currSol = "";
                        currRecentFile = "";
                        return;
                    }
                    var folder = getfolder();
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    var cleaned = getcleanPath(currSol) + ".txt";
                    currRecentFile = Path.Combine(folder, cleaned);
                    if (File.Exists(currRecentFile))
                    {
                        var list = File.ReadAllLines(currRecentFile).ToList();
                        recentFiles[currSol] = list;
                    }
                    else
                    {
                        recentFiles[currSol] = new List<string>();
                    }

                }
                else if (e.Kind == Microsoft.CodeAnalysis.WorkspaceChangeKind.DocumentChanged)
                {

                }

            };

            ws.DocumentActiveContextChanged += (o, e) =>
            {

            };

            ws.DocumentOpened += (o, e) =>
            {
                if (recentFiles.TryGetValue(currSol, out var curr))
                {
                    var file = e.Document.FilePath;
                    var idx = curr.IndexOf(file);
                    if (idx >= 0)
                        curr.RemoveAt(idx);
                    curr.Insert(0, file);
                    while (curr.Count > 30)
                        curr.RemoveAt(curr.Count - 1);

                    System.IO.File.WriteAllLines(currRecentFile, curr);
                }
                //var lines = new string[]
                //{
                //    currSol,
                //    e.Document.FilePath,
                //};
                //System.IO.File.AppendAllLines(@"c:\temp\openedFiles.txt", lines);
            };
            await ShowRecentFilesDialog.InitializeAsync(this);
            await SelectFunctionOuter.InitializeAsync(this);
            await SelectFunctionInner.InitializeAsync(this);
            await SelectNodeListInner.InitializeAsync(this);
            await ShrinkSelection.InitializeAsync(this);
            await DoGrep.InitializeAsync(this);

        }

        public static string getcleanPath(string path) => path.Replace("\\", "_").Replace(":", "_").Replace("\n", "").Replace("\r", "");

        public static string getfolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VSSyntaxExtensions");

        public static string currSol = "";
        public static string currRecentFile = "";

        public static Dictionary<string, List<string>> recentFiles = new Dictionary<string, List<string>>();

        #endregion
    }
}
