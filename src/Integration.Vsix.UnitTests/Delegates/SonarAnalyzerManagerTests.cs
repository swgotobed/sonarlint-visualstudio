/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */


using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarAnalyzer.Helpers;
using SonarLint.VisualStudio.Integration.NewConnectedMode;
using SonarLint.VisualStudio.Integration.Persistence;
using SonarLint.VisualStudio.Integration.Vsix;
using SonarQube.Client.Services;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    [TestClass]
    public class SonarAnalyzerManagerTests
    {
        private AdhocWorkspace workspace;
        private ConfigurableActiveSolutionBoundTracker activeSolutionBoundTracker;
        private Mock<ILogger> loggerMock;
        private Mock<ISonarQubeService> sonarQubeServiceMock;
        private Mock<IVsSolution> vsSolutionMock;
        private Mock<IProjectsRuleSetProvider> rulesetProviderMock;

        [TestInitialize]
        public void TestInitialize()
        {
            workspace = new AdhocWorkspace();
            activeSolutionBoundTracker = new ConfigurableActiveSolutionBoundTracker();
            loggerMock = new Mock<ILogger>();
            sonarQubeServiceMock = new Mock<ISonarQubeService>();
            vsSolutionMock = new Mock<IVsSolution>();
            vsSolutionMock.As<IVsSolution5>(); // Allows to cast IVsSolution into IVsSolution5
            rulesetProviderMock = new Mock<IProjectsRuleSetProvider>();
        }

        [TestMethod]
        public void Ctor_WhenIActiveSolutionBoundTrackerIsNull_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new SonarAnalyzerManager(null, sonarQubeServiceMock.Object, workspace,
                vsSolutionMock.Object, rulesetProviderMock.Object, loggerMock.Object);

            // Assert
            act.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("activeSolutionBoundTracker");
        }

        [TestMethod]
        public void Ctor_WhenISonarQubeServiceIsNull_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new SonarAnalyzerManager(activeSolutionBoundTracker, null, workspace,
                vsSolutionMock.Object, rulesetProviderMock.Object, loggerMock.Object);

            // Assert
            act.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("sonarQubeService");
        }

        [TestMethod]
        public void Ctor_WhenVisualStudioWorkspaceIsNull_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new SonarAnalyzerManager(activeSolutionBoundTracker, sonarQubeServiceMock.Object, null,
                vsSolutionMock.Object, rulesetProviderMock.Object, loggerMock.Object);

            // Assert
            act.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("workspace");
        }

        [TestMethod]
        public void Ctor_WhenIVsSolutionIsNull_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new SonarAnalyzerManager(activeSolutionBoundTracker, sonarQubeServiceMock.Object,
                workspace, null, rulesetProviderMock.Object, loggerMock.Object);

            // Assert
            act.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("vsSolution");
        }

        [TestMethod]
        public void Ctor_WhenIProjectsRuleSetProviderIsNull_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new SonarAnalyzerManager(activeSolutionBoundTracker, sonarQubeServiceMock.Object,
                workspace, vsSolutionMock.Object, null, loggerMock.Object);

            // Assert
            act.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("ruleSetProvider");
        }

        [TestMethod]
        public void Ctor_WhenILoggerIsNull_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new SonarAnalyzerManager(activeSolutionBoundTracker, sonarQubeServiceMock.Object,
                workspace, vsSolutionMock.Object, rulesetProviderMock.Object, null);

            // Assert
            act.ShouldThrow<ArgumentNullException>().And.ParamName.Should().Be("logger");
        }

        [TestMethod]
        public void Ctor_WhenCurrentModeIsStandalone_TriggersStandaloneWorkflow()
        {
            // Arrange
            this.activeSolutionBoundTracker.CurrentConfiguration = BindingConfiguration.Standalone;

            // Act
            var testSubject = CreateTestSubject();

            // Assert
            testSubject.currentWorklow.Should().BeOfType<SonarAnalyzerStandaloneWorkflow>();
        }

        [TestMethod]
        public void Ctor_WhenCurrentModeIsConnected_TriggersConnectedWorkflow()
        {
            // Arrange
            this.activeSolutionBoundTracker.CurrentConfiguration = new BindingConfiguration(
                new BoundSonarQubeProject { ProjectKey = "ProjectKey" }, SonarLintMode.Connected);

            // Act
            var testSubject = CreateTestSubject();

            // Assert
            testSubject.currentWorklow.Should().BeOfType<SonarAnalyzerConnectedWorkflow>();
        }

        [TestMethod]
        public void Ctor_WhenCurrentModeIsLegacyConnected_TriggersLegacyConnectedWorkflow()
        {
            // Arrange
            this.activeSolutionBoundTracker.CurrentConfiguration = new BindingConfiguration(
                new BoundSonarQubeProject { ProjectKey = "ProjectKey" }, SonarLintMode.LegacyConnected);

            // Act
            var testSubject = CreateTestSubject();

            // Assert
            testSubject.currentWorklow.Should().BeOfType<SonarAnalyzerLegacyConnectedWorkflow>();
        }

        [TestMethod]
        public void Ctor_WhenCurrentModeIsUndefined_DoesNothing()
        {
            // Arrange
            this.activeSolutionBoundTracker.CurrentConfiguration = new BindingConfiguration(null, (SonarLintMode)42);

            // Act
            var testSubject = CreateTestSubject();

            // Assert
            testSubject.currentWorklow.Should().BeNull();
        }

        [TestMethod]
        public void WhenBoundSolutionChanges_TriggersNewWorkflow()
        {
            // Arrange
            this.activeSolutionBoundTracker.CurrentConfiguration = BindingConfiguration.Standalone;
            var testSubject = CreateTestSubject();

            // Sanity Check
            testSubject.currentWorklow.Should().BeOfType<SonarAnalyzerStandaloneWorkflow>();

            // Act
            this.activeSolutionBoundTracker.SimulateSolutionBindingChanged(new ActiveSolutionBindingEventArgs(
                new BindingConfiguration(new BoundSonarQubeProject { ProjectKey = "ProjectKey" }, SonarLintMode.Connected)));

            // Assert
            testSubject.currentWorklow.Should().BeOfType<SonarAnalyzerConnectedWorkflow>();
        }

        [TestMethod]
        public void WhenSolutionBindingUpdated_WhenNewConnectedMode_TriggersNewInstanceOfSameKindOfWorkflow()
        {
            // Arrange
            this.activeSolutionBoundTracker.CurrentConfiguration = new BindingConfiguration(
                new BoundSonarQubeProject { ProjectKey = "ProjectKey" }, SonarLintMode.Connected);
            var testSubject = CreateTestSubject();
            var workflow = testSubject.currentWorklow;

            // Sanity check
            workflow.Should().BeOfType<SonarAnalyzerConnectedWorkflow>();

            // Act
            activeSolutionBoundTracker.SimulateSolutionBindingUpdated();

            // Assert
            testSubject.currentWorklow.Should().NotBe(workflow);
            testSubject.currentWorklow.Should().BeOfType<SonarAnalyzerConnectedWorkflow>();
        }

        [TestMethod]
        public void WhenSolutionBindingUpdated_WhenLegacyConnectedMode_TriggersNewInstanceOfSameKindOfWorkflow()
        {
            // Arrange
            this.activeSolutionBoundTracker.CurrentConfiguration = new BindingConfiguration(
                new BoundSonarQubeProject { ProjectKey = "ProjectKey" }, SonarLintMode.LegacyConnected);
            var testSubject = CreateTestSubject();
            var workflow = testSubject.currentWorklow;

            // Sanity check
            workflow.Should().BeOfType<SonarAnalyzerLegacyConnectedWorkflow>();

            // Act
            activeSolutionBoundTracker.SimulateSolutionBindingUpdated();

            // Assert
            testSubject.currentWorklow.Should().NotBe(workflow);
            testSubject.currentWorklow.Should().BeOfType<SonarAnalyzerLegacyConnectedWorkflow>();
        }

        [TestMethod]
        public void Dispose_ResetAllDelegates()
        {
            // Arrange
            int callCount = 0;
            Func<IEnumerable<DiagnosticDescriptor>, bool> expectedShouldRegisterContextAction =
                list => { callCount++; return false; };
            Func<IEnumerable<DiagnosticDescriptor>, SyntaxTree, bool> expectedShouldExecuteRegisteredAction =
                (list, tree) => { callCount++; return false; };
            Func<SyntaxTree, Diagnostic, bool> expectedShouldDiagnosticBeReported =
                (t, d) => { callCount++; return false; };
            Action<IReportingContext> expectedReportDiagnostic =
                ctx => { callCount++; };

            var testSubject = CreateTestSubject();

            SonarAnalysisContext.ShouldRegisterContextAction = expectedShouldRegisterContextAction;
            SonarAnalysisContext.ShouldExecuteRegisteredAction = expectedShouldExecuteRegisteredAction;
            SonarAnalysisContext.ShouldDiagnosticBeReported = expectedShouldDiagnosticBeReported;
            SonarAnalysisContext.ReportDiagnostic = expectedReportDiagnostic;

            // Act
            testSubject.Dispose();

            // Assert
            SonarAnalysisContext.ShouldRegisterContextAction.Should().NotBe(expectedShouldRegisterContextAction);
            SonarAnalysisContext.ShouldDiagnosticBeReported.Should().NotBe(expectedShouldDiagnosticBeReported);
            SonarAnalysisContext.ShouldExecuteRegisteredAction.Should().NotBe(expectedShouldExecuteRegisteredAction);
            SonarAnalysisContext.ReportDiagnostic.Should().NotBe(expectedReportDiagnostic);
        }

        private SonarAnalyzerManager CreateTestSubject() => new SonarAnalyzerManager(activeSolutionBoundTracker,
            sonarQubeServiceMock.Object, workspace, vsSolutionMock.Object, rulesetProviderMock.Object, loggerMock.Object);
    }
}