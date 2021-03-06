﻿// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.CookiecutterTools.Commands;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Model;
using Microsoft.CookiecutterTools.View;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.CookiecutterTools {
    [Guid("AC207EBF-16F8-4AA4-A0A8-70AF37308FCD")]
    sealed class CookiecutterToolWindow : ToolWindowPane, IVsInfoBarUIEvents {
        private IServiceProvider _site;
        private Redirector _outputWindow;
        private IVsStatusbar _statusBar;
        private IVsUIShell _uiShell;
        private EnvDTE.DTE _dte;
        private CookiecutterControl _cookiecutterControl;
        private IVsInfoBarUIFactory _infoBarFactory;
        private IVsInfoBarUIElement _infoBar;
        private IVsInfoBar _infoBarModel;
        private uint _infoBarAdviseCookie;

        private readonly object _commandsLock = new object();
        private readonly Dictionary<Command, MenuCommand> _commands = new Dictionary<Command, MenuCommand>();

        public CookiecutterToolWindow() {
            BitmapImageMoniker = KnownMonikers.DockPanel;
            Caption = Strings.ToolWindowCaption;
            ToolBar = new CommandID(PackageGuids.guidCookiecutterCmdSet, PackageIds.WindowToolBarId);
        }

        protected override void Dispose(bool disposing) {
            if (_cookiecutterControl != null) {
                _cookiecutterControl.ContextMenuRequested -= OnContextMenuRequested;
            }

            base.Dispose(disposing);
        }

        protected override void OnCreate() {
            _site = (IServiceProvider)this;

            _outputWindow = OutputWindowRedirector.GetGeneral(CookiecutterPackage.Instance);
            Debug.Assert(_outputWindow != null);
            _statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            _uiShell = _site.GetService(typeof(SVsUIShell)) as IVsUIShell;
            _dte = _site.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            _infoBarFactory = _site.GetService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;

            object control = null;

            if (!CookiecutterClientProvider.IsCompatiblePythonAvailable()) {
                control = new MissingDependencies();
            } else {
                string feedUrl = CookiecutterPackage.Instance.RecommendedFeed;
                if (string.IsNullOrEmpty(feedUrl)) {
                    feedUrl = UrlConstants.DefaultRecommendedFeed;
                }

                _cookiecutterControl = new CookiecutterControl(_outputWindow, new Uri(feedUrl), OpenGeneratedFolder, UpdateCommandUI);
                _cookiecutterControl.ContextMenuRequested += OnContextMenuRequested;
                control = _cookiecutterControl;
            }

            Content = control;

            RegisterCommands(new Command[] {
                new HomeCommand(this),
                new RunCommand(this),
                new GitHubCommand(this, PackageIds.cmdidLinkGitHubHome),
                new GitHubCommand(this, PackageIds.cmdidLinkGitHubIssues),
                new GitHubCommand(this, PackageIds.cmdidLinkGitHubWiki),
            }, PackageGuids.guidCookiecutterCmdSet);

            RegisterCommands(new Command[] {
                new DeleteInstalledTemplateCommand(this),
            }, VSConstants.GUID_VSStandardCommandSet97);

            base.OnCreate();

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            if (CookiecutterPackage.Instance.ShowHelp) {
                AddInfoBar();
            }
        }

        public void OnClosed(IVsInfoBarUIElement infoBarUIElement) {
            if (_infoBar != null) {
                if (_infoBarAdviseCookie != 0) {
                    _infoBar.Unadvise(_infoBarAdviseCookie);
                    _infoBarAdviseCookie = 0;
                }

                // Remember this for next time
                CookiecutterPackage.Instance.ShowHelp = false;

                RemoveInfoBar(_infoBar);
                _infoBar.Close();
                _infoBar = null;
            }
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem) {
            ((Action)actionItem.ActionContext)();
        }

        private void AddInfoBar() {
            Action showHelp = () => Process.Start(UrlConstants.HelpUrl);

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(Strings.InfoBarMessage));
            actions.Add(new InfoBarHyperlink(Strings.InfoBarMessageLink, showHelp));

            _infoBarModel = new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true);
            _infoBar = _infoBarFactory.CreateInfoBar(_infoBarModel);
            AddInfoBar(_infoBar);
            _infoBar.Advise(this, out _infoBarAdviseCookie);
        }

        internal void RegisterCommands(IEnumerable<Command> commands, Guid cmdSet) {
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs) {
                lock (_commandsLock) {
                    foreach (var command in commands) {
                        var beforeQueryStatus = command.BeforeQueryStatus;
                        CommandID toolwndCommandID = new CommandID(cmdSet, command.CommandId);
                        OleMenuCommand menuToolWin = new OleMenuCommand(command.DoCommand, toolwndCommandID);
                        if (beforeQueryStatus != null) {
                            menuToolWin.BeforeQueryStatus += beforeQueryStatus;
                        }
                        mcs.AddCommand(menuToolWin);
                        _commands[command] = menuToolWin;
                    }
                }
            }
        }

        private void UpdateCommandUI() {
            _uiShell.UpdateCommandUI(0);
        }

        private void OpenGeneratedFolder(string folderPath) {
#if DEV15_OR_LATER
            OpenInSolutionExplorer(folderPath);
#else
            OpenInWindowsExplorer(folderPath);
#endif
        }

        internal static Guid openFolderCommandGroupGuid = new Guid("CFB400F1-5C60-4F3C-856E-180D28DEF0B7");
        internal const int OpenFolderCommandId = 260;
        internal const string vsWindowKindSolutionExplorer = "{3AE79031-E1BC-11D0-8F78-00A0C9110057}";

        private void OpenInSolutionExplorer(string folderPath) {
            var res = MessageBox.Show(Strings.OpenInSolutionExplorerQuestion, Strings.ProductTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) {
                return;
            }

            _uiShell.PostExecCommand(ref openFolderCommandGroupGuid, OpenFolderCommandId, 0, folderPath);
            _dte.Windows.Item(vsWindowKindSolutionExplorer)?.Activate();
        }

        private void OpenInWindowsExplorer(string folderPath) {
            Process.Start(new ProcessStartInfo() {
                FileName = folderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        internal void NavigateToGitHub(int commandId) {
            switch (commandId) {
                case PackageIds.cmdidLinkGitHubHome:
                    _cookiecutterControl?.NavigateToGitHubHome();
                    break;
                case PackageIds.cmdidLinkGitHubIssues:
                    _cookiecutterControl?.NavigateToGitHubIssues();
                    break;
                case PackageIds.cmdidLinkGitHubWiki:
                    _cookiecutterControl?.NavigateToGitHubWiki();
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        internal bool CanNavigateToGitHub() {
            return _cookiecutterControl != null ? _cookiecutterControl.CanNavigateToGitHub() : false;
        }

        internal void Home() {
            _cookiecutterControl?.Home();
        }

        internal void DeleteSelection() {
            _cookiecutterControl?.DeleteSelection();
        }

        internal bool CanDeleteSelection() {
            return _cookiecutterControl != null ? _cookiecutterControl.CanDeleteSelection() : false;
        }

        internal void RunSelection() {
            _cookiecutterControl?.RunSelection();
        }

        internal bool CanRunSelection() {
            return _cookiecutterControl != null ? _cookiecutterControl.CanRunSelection() : false;
        }

        private void OnContextMenuRequested(object sender, PointEventArgs e) {
            ShowContextMenu(e.Point);
        }

        private void ShowContextMenu(Point point) {
            CookiecutterPackage.ShowContextMenu(
                new CommandID(PackageGuids.guidCookiecutterCmdSet, PackageIds.ContextMenu),
                (int)point.X,
                (int)point.Y,
                this
            );
        }
    }
}
