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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using Microsoft.CookiecutterTools;
using Microsoft.CookiecutterTools.ViewModel;
using Microsoft.CookiecutterTools.Model;

namespace CookiecutterTests {
    [TestClass]
    public class CookiecutterViewModelTests {
        private const string GitHubTemplatePath = "https://github.com/audreyr/Cookiecutter-pypackage";
        private const string NoUserConfigFilePath = "";

        private static string LocalTemplatePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "template");
        private static string UserConfigFilePath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "userconfig.yaml");
        private static string LocalFeedPath => Path.Combine(TestData.GetPath("TestData"), "Cookiecutter", "feed.txt");

        private MockRedirector _redirector;
        private MockGitClient _gitClient;
        private MockGitHubClient _gitHubClient;
        private MockCookiecutterClient _cutterClient;
        private CookiecutterViewModel _vm;
        private MockTemplateSource _installedTemplateSource;
        private MockTemplateSource _gitHubTemplateSource;
        private MockTemplateSource _feedTemplateSource;

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestInitialize]
        public void SetupTest() {
            _redirector = new MockRedirector();
            _gitClient = new MockGitClient();
            _gitHubClient = new MockGitHubClient();
            _cutterClient = new MockCookiecutterClient();
            _installedTemplateSource = new MockTemplateSource();
            _gitHubTemplateSource = new MockTemplateSource();
            _feedTemplateSource = new MockTemplateSource();

            var output = TestData.GetTempPath("Cookiecutter", true);
            var outputProjectFolder = Path.Combine(output, "project");

            _vm = new CookiecutterViewModel(_cutterClient, _gitHubClient, _gitClient, _redirector, _installedTemplateSource, _feedTemplateSource, _gitHubTemplateSource, null);
            _vm.UserConfigFilePath = UserConfigFilePath;
            _vm.OutputFolderPath = outputProjectFolder;
        }

        // TODO: does not work yet
        //[TestMethod]
        public async Task Search() {
            _installedTemplateSource.Templates.Add(
                Tuple.Create(string.Empty, (string)null),
                Tuple.Create(new Template[] {
                    new Template() {
                        Name = "owner1/template1",
                        LocalFolderPath = Path.Combine(_vm.InstalledFolderPath, "template1"),
                        RemoteUrl = "https://github.com/owner1/template1",
                        Description = string.Empty }
                }, (string)null));
            await _vm.SearchAsync();
            Assert.IsTrue(_vm.Installed.Templates.Count == 1);
        }
    }
}
