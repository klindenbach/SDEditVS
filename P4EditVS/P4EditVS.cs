﻿using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;
using System.ComponentModel;

// The documentation for IVsPersistSolutionOpts is... not good. There's some use
// here:
// https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs

namespace P4EditVS
{
    /// <summary>
    /// Values saved to the .suo file.
    /// </summary>
    /// <remarks>
    /// Since it's convenient, this stuff is serialized using the XmlSerializer.
    /// So anything in here needs to be compatible with that.
    ///
    /// If the suo doesn't contain a SolutionOptions, the default values here
    /// will be used. 
    /// </remarks>
    public class SolutionOptions
    {
        // Index of workspace to use.
        public int WorkspaceIndex = -1;

        // For debugging purposes, when you're staring at a mostly
        // incomprehensible hex dump of the .suo file.
        public string Now = DateTime.Now.ToString();
    }

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
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    //[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.None)]
    [Guid(P4EditVS.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionPageGrid), "P4EditVS", "Settings", 0, 0, true)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class P4EditVS : AsyncPackage, IVsPersistSolutionOpts
    {
        private const string SolutionUserOptionsKey = "P4EditVS";

        /// <summary>
        /// P4EditVS GUID string.
        /// </summary>
        public const string PackageGuidString = "d6a4db63-698d-4d16-bbc0-944fe52f83db";

        private int _selectedWorkspace = -1;
        public int SelectedWorkspace { get => _selectedWorkspace; set => _selectedWorkspace = value; }

        public string ClientName
        {
            get
            {
                return GetWorkspaceName(_selectedWorkspace);
            }
        }

        public string UserName
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                // This is truly awful
                switch (_selectedWorkspace)
                {
                    case 0:
                        return page.UserName;
                    case 1:
                        return page.UserName2;
                    case 2:
                        return page.UserName3;
                    case 3:
                        return page.UserName4;
                    case 4:
                        return page.UserName5;
                    case 5:
                        return page.UserName6;
                }
                throw new IndexOutOfRangeException();
            }
        }

        public string Server
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                // This is truly awful
                switch (_selectedWorkspace)
                {
                    case 0:
                        return page.Server;
                    case 1:
                        return page.Server2;
                    case 2:
                        return page.Server3;
                    case 3:
                        return page.Server4;
                    case 4:
                        return page.Server5;
                    case 5:
                        return page.Server6;
                }
                throw new IndexOutOfRangeException();
            }
        }

		public bool AutoCheckout
		{
			get
			{
				OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
				return page.AutoCheckout || page.AutoCheckoutOnEdit;
			}
		}

		public bool AutoCheckoutPrompt
		{
			get
			{
				OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
				return page.AutoCheckoutPrompt;
			}
		}


        public bool AutoCheckoutOnEdit
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.AutoCheckoutOnEdit;
            }
        }

        public string GetWorkspaceName(int index)
        {
            OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
            // This is truly awful
            switch (index)
            {
                case -1:
                    if (page.AllowEnvironment) return "(Use Environment)";
                    else return "";

                case 0:
                    return page.ClientName;
                case 1:
                    return page.ClientName2;
                case 2:
                    return page.ClientName3;
                case 3:
                    return page.ClientName4;
                case 4:
                    return page.ClientName5;
                case 5:
                    return page.ClientName6;
            }
            throw new IndexOutOfRangeException();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="P4EditVS"/> class.
        /// </summary>
        public P4EditVS()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
            Trace.WriteLine(string.Format("Hello from P4EditVS"));
        }

        /// <summary>
        /// Create a SolutionOptions object for the current settings.
        /// </summary>
        /// <remarks>
        /// The SolutionOptions object is written to the .suo file.
        /// </remarks>
        /// <returns></returns>
        private SolutionOptions GetSolutionOptions()
        {
            var options = new SolutionOptions();

            options.WorkspaceIndex = SelectedWorkspace;

            return options;
        }

        /// <summary>
        /// Set the current settings from a SolutionOptions object.
        /// </summary>
        /// <remarks>
        /// The SolutionOptions is just whatever was in the .suo file.
        /// </remarks>
        /// <param name="options"></param>
        private void SetSolutionOptions(SolutionOptions options)
        {
            if (options.WorkspaceIndex >= -1 && options.WorkspaceIndex <= 6) SelectedWorkspace = options.WorkspaceIndex;
        }

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
            await Commands.InitializeAsync(this);
        }

        public string GetGlobalP4CmdLineOptions()
        {
            if (_selectedWorkspace == -1)
            {
                return "";
            }
            else
            {
                // 
                return string.Format("-c {0} -u {1} -p {2}", ClientName, UserName, Server);
            }
        }

        public bool ValidateUserSettings()
        {
            if (ClientName == "")
            {
                VsShellUtilities.ShowMessageBox(
                this,
                "Client name is empty. This must be set under Tools->Options->P4EditVS->Settings",
                "Invalid Settings",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                return false;
            }

            // If Allow Use Environment is disabled, and the environment is the
            // selected workspace, the previous check will fail.
            //
            // So if things got this far, and the environment is the selected
            // workspace, it's all good. User name and server quite unnecessary.
            if (_selectedWorkspace == -1)
            {
                return true;
            }

            if (UserName == "")
            {
                VsShellUtilities.ShowMessageBox(
                this,
                "User name is empty. This must be set under Tools->Options->P4EditVS->Settings",
                "Invalid Settings",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                return false;
            }

            if (Server == "")
            {
                VsShellUtilities.ShowMessageBox(
                this,
                "Server is empty. This must be set under Tools->Options->P4EditVS->Settings",
                "Invalid Settings",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                return false;
            }

            return true;
        }

        //
        // Summary:
        //     Saves user options for a given solution.
        //
        // Parameters:
        //   pPersistence:
        //     [in] Pointer to the Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence
        //     interface on which the VSPackage should call its Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence.SavePackageUserOpts(Microsoft.VisualStudio.Shell.Interop.IVsPersistSolutionOpts,System.String)
        //     method for each stream name it wants to write to the user options file.
        //
        // Returns:
        //     If the method succeeds, it returns Microsoft.VisualStudio.VSConstants.S_OK. If
        //     it fails, it returns an error code.
        public int SaveUserOptions(IVsSolutionPersistence pPersistence)
        {
            Trace.WriteLine(String.Format("P4EditVS SaveUserOptions"));

            ThreadHelper.ThrowIfNotOnUIThread();

            // https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs#L300
            //
            // This function gets called by the shell when the SUO file is
            // saved. The provider calls the shell back to let it know which
            // options keys it will use in the suo file. The shell will create a
            // stream for the section of interest, and will call back the
            // provider on IVsPersistSolutionProps.WriteUserOptions() to save
            // specific options under the specified key.

            pPersistence.SavePackageUserOpts(this, SolutionUserOptionsKey);
            return VSConstants.S_OK;
        }

        //
        // Summary:
        //     Loads user options for a given solution.
        //
        // Parameters:
        //   pPersistence:
        //     [in] Pointer to the Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence
        //     interface on which the VSPackage should call its Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence.LoadPackageUserOpts(Microsoft.VisualStudio.Shell.Interop.IVsPersistSolutionOpts,System.String)
        //     method for each stream name it wants to read from the user options (.opt) file.
        //
        //   grfLoadOpts:
        //     [in] User options whose value is taken from the Microsoft.VisualStudio.Shell.Interop.__VSLOADUSEROPTS
        //     DWORD.
        //
        // Returns:
        //     If the method succeeds, it returns Microsoft.VisualStudio.VSConstants.S_OK. If
        //     it fails, it returns an error code.
        public int LoadUserOptions(IVsSolutionPersistence pPersistence, [ComAliasName("Microsoft.VisualStudio.Shell.Interop.VSLOADUSEROPTS")] uint grfLoadOpts)
        {
            Trace.WriteLine(String.Format("P4EditVS LoadUserOptions (grfLoadOpts={0})", grfLoadOpts));

            ThreadHelper.ThrowIfNotOnUIThread();

            // https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs#L359
            //
            // Note this can be during opening a new solution, or may be during
            // merging of 2 solutions. The provider calls the shell back to let
            // it know which options keys from the suo file were written by this
            // provider. If the shell will find in the suo file a section that
            // belong to this package, it will create a stream, and will call
            // back the provider on IVsPersistSolutionProps.ReadUserOptions() to
            // read specific options under that option key.

            pPersistence.LoadPackageUserOpts(this, SolutionUserOptionsKey);
            return VSConstants.S_OK;
        }

        //
        // Summary:
        //     Writes user options for a given solution.
        //
        // Parameters:
        //   pOptionsStream:
        //     [in] Pointer to the IStream interface to which the VSPackage should write the
        //     user-specific options.
        //
        //   pszKey:
        //     [in] Name of the stream, as provided by the VSPackage by means of the method
        //     Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence.SavePackageUserOpts(Microsoft.VisualStudio.Shell.Interop.IVsPersistSolutionOpts,System.String).
        //
        // Returns:
        //     If the method succeeds, it returns Microsoft.VisualStudio.VSConstants.S_OK. If
        //     it fails, it returns an error code.
        public int WriteUserOptions(IStream pOptionsStream, [ComAliasName("Microsoft.VisualStudio.OLE.Interop.LPCOLESTR")] string pszKey)
        {
            Trace.WriteLine(String.Format("P4EditVS WriteUserOptions (key=\"{0}\")", pszKey));

            // https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs#L318
            //
            // This function gets called by the shell to let the package write
            // user options under the specified key. The key was declared in
            // SaveUserOptions(), when the shell started saving the suo file.
            var stream = new DataStreamFromComStream(pOptionsStream);

            var options = GetSolutionOptions();

            Misc.WriteXml(stream, options);

            return VSConstants.S_OK;
        }

        //
        // Summary:
        //     Reads user options for a given solution.
        //
        // Parameters:
        //   pOptionsStream:
        //     [in] Pointer to the IStream interface from which the VSPackage should read the
        //     user-specific options.
        //
        //   pszKey:
        //     [in] Name of the stream, as provided by the VSPackage by means of the method
        //     Microsoft.VisualStudio.Shell.Interop.IVsSolutionPersistence.LoadPackageUserOpts(Microsoft.VisualStudio.Shell.Interop.IVsPersistSolutionOpts,System.String).
        //
        // Returns:
        //     If the method succeeds, it returns Microsoft.VisualStudio.VSConstants.S_OK. If
        //     it fails, it returns an error code.
        public int ReadUserOptions(IStream pOptionsStream, [ComAliasName("Microsoft.VisualStudio.OLE.Interop.LPCOLESTR")] string pszKey)
        {
            Trace.WriteLine(String.Format("P4EditVS ReadUserOptions (key=\"{0}\")", pszKey));

            ThreadHelper.ThrowIfNotOnUIThread();

            // https://github.com/microsoft/VSSDK-Extensibility-Samples/blob/2643adab7d534aee60dd9405901f15d35c136dbd/ArchivedSamples/Source_Code_Control_Provider/C%23/SccProvider.cs#L376
            //
            // This function is called by the shell if the
            // _strSolutionUserOptionsKey section declared in LoadUserOptions()
            // as being written by this package has been found in the suo file. 
            // Note this can be during opening a new solution, or may be during
            // merging of 2 solutions. A good source control provider may need
            // to persist this data until OnAfterOpenSolution or
            // OnAfterMergeSolution is called

            var stream = new DataStreamFromComStream(pOptionsStream);

            var options = new SolutionOptions();

            if (stream.Length > 0) options = Misc.ReadXmlOrCreateDefault<SolutionOptions>(stream);

            SetSolutionOptions(options);

            return VSConstants.S_OK;
        }

        #endregion
    }

    public class OptionPageGrid : DialogPage
    {
        // This seems like a terrible way to do it

        private bool _allowEnvironment = true;

        [Category("Options")]
        [DisplayName("Allow Environment")]
        [Description("Allow use of environment for workspace/connection settings. (See p4v, Connection > Environment Settings...; or see \"p4 set\")")]
        public bool AllowEnvironment
        {
            get { return _allowEnvironment; }
            set { _allowEnvironment = value; }
        }

		private bool _autoCheckout = true;

		[Category("Options")]
		[DisplayName("Auto-Checkout Enabled")]
		[Description("Automatically checks out files save/build")]
		public bool AutoCheckout
		{
			get { return _autoCheckout; }
			set { _autoCheckout = value; }
		}

        private bool _autoCheckoutOnEdit = false;

        [Category("Options")]
        [DisplayName("Auto-Checkout On Edit")]
        [Description("Automatically checks out files when edited, does not work projects or solutions. Disabled by default as it is more expensive than doing it on save/build. (requires restart)")]
        public bool AutoCheckoutOnEdit
        {
            get { return _autoCheckoutOnEdit; }
            set { _autoCheckoutOnEdit = value; }
        }

        private bool _autoCheckoutPrompt = false;

		[Category("Options")]
		[DisplayName("Prompt Before Auto-Checkout")]
		[Description("Prompts message to automatically check out files on build and save. Auto-checkout must be enabled.")]
		public bool AutoCheckoutPrompt
		{
			get { return _autoCheckoutPrompt; }
			set { _autoCheckoutPrompt = value; }
		}

        private string _userName = "";
        private string _clientName = "";
        private string _server = "";

        [Category("Workspace 1")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName
        {
            get { return _userName; }
            set { _userName = value.Trim(); }
        }

        [Category("Workspace 1")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName
        {
            get { return _clientName; }
            set { _clientName = value.Trim(); }
        }

        [Category("Workspace 1")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server
        {
            get { return _server; }
            set { _server = value.Trim(); }
        }

        private string _userName2 = "";
        private string _clientName2 = "";
        private string _server2 = "";

        [Category("Workspace 2")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName2
        {
            get { return _userName2; }
            set { _userName2 = value.Trim(); }
        }

        [Category("Workspace 2")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName2
        {
            get { return _clientName2; }
            set { _clientName2 = value.Trim(); }
        }

        [Category("Workspace 2")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server2
        {
            get { return _server2; }
            set { _server2 = value.Trim(); }
        }

        private string _userName3 = "";
        private string _clientName3 = "";
        private string _server3 = "";

        [Category("Workspace 3")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName3
        {
            get { return _userName3; }
            set { _userName3 = value.Trim(); }
        }

        [Category("Workspace 3")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName3
        {
            get { return _clientName3; }
            set { _clientName3 = value.Trim(); }
        }

        [Category("Workspace 3")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server3
        {
            get { return _server3; }
            set { _server3 = value.Trim(); }
        }

        private string _userName4 = "";
        private string _clientName4 = "";
        private string _server4 = "";

        [Category("Workspace 4")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName4
        {
            get { return _userName4; }
            set { _userName4 = value.Trim(); }
        }

        [Category("Workspace 4")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName4
        {
            get { return _clientName4; }
            set { _clientName4 = value.Trim(); }
        }

        [Category("Workspace 4")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server4
        {
            get { return _server4; }
            set { _server4 = value.Trim(); }
        }

        private string _userName5 = "";
        private string _clientName5 = "";
        private string _server5 = "";

        [Category("Workspace 5")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName5
        {
            get { return _userName5; }
            set { _userName5 = value.Trim(); }
        }

        [Category("Workspace 5")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName5
        {
            get { return _clientName5; }
            set { _clientName5 = value.Trim(); }
        }

        [Category("Workspace 5")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server5
        {
            get { return _server5; }
            set { _server5 = value.Trim(); }
        }

        private string _userName6 = "";
        private string _clientName6 = "";
        private string _server6 = "";

        [Category("Workspace 6")]
        [DisplayName("Perforce User Name")]
        [Description("User name")]
        public string UserName6
        {
            get { return _userName6; }
            set { _userName6 = value.Trim(); }
        }

        [Category("Workspace 6")]
        [DisplayName("Perforce Client Name")]
        [Description("Client name, i.e. workspace name")]
        public string ClientName6
        {
            get { return _clientName6; }
            set { _clientName6 = value.Trim(); }
        }

        [Category("Workspace 6")]
        [DisplayName("Perforce Server")]
        [Description("e.g. localhost:1666")]
        public string Server6
        {
            get { return _server6; }
            set { _server6 = value.Trim(); }
        }

    }
}
