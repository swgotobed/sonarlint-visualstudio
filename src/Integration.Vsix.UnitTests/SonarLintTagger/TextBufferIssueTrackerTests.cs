﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2020 SonarSource SA
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
using System.Linq;
using System.Text;
using System.Threading;
using EnvDTE;
using FluentAssertions;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Sonarlint;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.Suppression;
using SonarLint.VisualStudio.Integration.Vsix;
using SonarLint.VisualStudio.Integration.Vsix.Analysis;

namespace SonarLint.VisualStudio.Integration.UnitTests
{
    /*
     * Note: the TextBufferIssueTracker and TaggerProvider are tightly coupled so it isn't possible
     * to test them completely independently without substantial refactoring.
     * These unit tests are dependent on both classes behaving correctly.
     */

    [TestClass]
    public class TextBufferIssueTrackerTests
    {
        private Mock<IAnalyzerController> mockAnalyzerController;
        private TaggerProvider taggerProvider;
        private Mock<ITextDocument> mockedJavascriptDocumentFooJs;
        private Mock<IIssuesFilter> issuesFilter;
        private AnalysisLanguage[] javascriptLanguage = new[] { AnalysisLanguage.Javascript };
        private TextBufferIssueTracker testSubject;

        [TestInitialize]
        public void SetUp()
        {
            mockAnalyzerController = new Mock<IAnalyzerController>();
            issuesFilter = new Mock<IIssuesFilter>();
            taggerProvider = CreateTaggerProvider();
            mockedJavascriptDocumentFooJs = CreateDocumentMock("foo.js");
            javascriptLanguage = new[] { AnalysisLanguage.Javascript };

            testSubject = new TextBufferIssueTracker(taggerProvider.dte, taggerProvider,
                mockedJavascriptDocumentFooJs.Object, javascriptLanguage, new TestLogger(), issuesFilter.Object);
        }

        #region Triggering analysis tests

        [TestMethod]
        public void WhenTaggerIsRegistered_AnalysisIsRequested()
        {
            // 1. No tagger -> analysis not requested
            CheckAnalysisWasNotRequested();

            // 2. Add a tagger -> analysis requested
            using (var tagger = new IssueTagger(testSubject))
            {
                mockAnalyzerController.Verify(x => x.ExecuteAnalysis("foo.js", "utf-8", new AnalysisLanguage[] { AnalysisLanguage.Javascript }, testSubject, It.IsAny<ProjectItem>(),
                    (IAnalyzerOptions)null /* no expecting any options when a new tagger is added */,
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [TestMethod]
        public void WhenFileRenamed_FileNameIsUpdated_AndAnalysisIsNotRequested()
        {
            var errorListSink = RegisterNewErrorListSink();
            testSubject.Factory.CurrentSnapshot.VersionNumber.Should().Be(0); // sanity check

            // Act
            RaiseRenameEvent(mockedJavascriptDocumentFooJs, "newPath.js");

            // Assert
            testSubject.FilePath.Should().Be("newPath.js");

            // Check the snapshot was updated and the error list notified
            testSubject.Factory.CurrentSnapshot.VersionNumber.Should().Be(1);
            CheckSinkNotified(errorListSink, 1);

            CheckAnalysisWasNotRequested();
        }

        [TestMethod]
        public void WhenFileIsSaved_ButNoTaggers_AnalysisIsNotRequested()
        {
            // Arrange
            CheckAnalysisWasNotRequested();

            // Act
            RaiseFileLoadedEvent(mockedJavascriptDocumentFooJs);

            // Assert
            CheckAnalysisWasNotRequested();
        }

        [TestMethod]
        public void WhenFileIsSaved_AnalysisIsRequested_ButOnlyIfATaggerIsRegistered()
        {
            // 1. No tagger -> analysis not requested
            RaiseFileSavedEvent(mockedJavascriptDocumentFooJs);
            CheckAnalysisWasNotRequested();

            // 2. Add a tagger and raise -> analysis requested
            var tagger = new IssueTagger(testSubject);
            mockAnalyzerController.Invocations.Clear();


            RaiseFileSavedEvent(mockedJavascriptDocumentFooJs);
            mockAnalyzerController.Verify(x => x.ExecuteAnalysis("foo.js", "utf-8", new AnalysisLanguage[] { AnalysisLanguage.Javascript }, testSubject, It.IsAny<ProjectItem>(),
                (IAnalyzerOptions)null /* no expecting any options when the settings file is updated */,
                It.IsAny<CancellationToken>()), Times.Once);

            // 3. Unregister tagger and raise -> analysis not requested
            tagger.Dispose();
            mockAnalyzerController.Invocations.Clear();

            RaiseFileSavedEvent(mockedJavascriptDocumentFooJs);
            CheckAnalysisWasNotRequested();
        }

        [TestMethod]
        public void WhenFileIsLoaded_AnalysisIsNotRequested()
        {
            using (var tagger = new IssueTagger(testSubject))
            {
                mockAnalyzerController.Invocations.Clear();

                // Act
                RaiseFileLoadedEvent(mockedJavascriptDocumentFooJs);
                CheckAnalysisWasNotRequested();

                // Sanity check (that the test setup is correct and that events are actually being handled)
                RaiseFileSavedEvent(mockedJavascriptDocumentFooJs);
                mockAnalyzerController.Verify(x => x.ExecuteAnalysis("foo.js", "utf-8", new AnalysisLanguage[] { AnalysisLanguage.Javascript }, testSubject, It.IsAny<ProjectItem>(), It.IsAny<IAnalyzerOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        private static void RaiseRenameEvent(Mock<ITextDocument> mockDocument, string newFileName)
        {
            var args = new TextDocumentFileActionEventArgs(newFileName, DateTime.UtcNow, FileActionTypes.DocumentRenamed);
            mockDocument.Raise(x => x.FileActionOccurred += null, args);
        }

        private static void RaiseFileSavedEvent(Mock<ITextDocument> mockDocument)
        {
            var args = new TextDocumentFileActionEventArgs(mockDocument.Object.FilePath, DateTime.UtcNow, FileActionTypes.ContentSavedToDisk);
            mockDocument.Raise(x => x.FileActionOccurred += null, args);
        }

        private static void RaiseFileLoadedEvent(Mock<ITextDocument> mockDocument)
        {
            var args = new TextDocumentFileActionEventArgs(mockDocument.Object.FilePath, DateTime.UtcNow, FileActionTypes.ContentLoadedFromDisk);
            mockDocument.Raise(x => x.FileActionOccurred += null, args);
        }

        private void CheckAnalysisWasNotRequested()
        {
            mockAnalyzerController.Verify(x => x.ExecuteAnalysis(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AnalysisLanguage>>(),
                It.IsAny<IIssueConsumer>(), It.IsAny<ProjectItem>(), It.IsAny<IAnalyzerOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Processing analysis results tests

        [TestMethod]
        public void WhenNewIssuesAreFound_ButForIncorrectFile_FilterNotCalledAndListenersAreNotUpdated()
        {
            // Arrange
            var issues = new[] { new Issue { RuleKey = "S123", StartLine = 1, EndLine = 1 } };

            // Add a couple of error list listeners
            var errorListSinkMock1 = RegisterNewErrorListSink();
            var errorListSinkMock2 = RegisterNewErrorListSink();

            // Act
            using (new AssertIgnoreScope())
            {
                ((IIssueConsumer)testSubject).Accept("aRandomFile.xxx", issues);
            }

            // Assert
            CheckSinkNotNotified(errorListSinkMock1);
            CheckSinkNotNotified(errorListSinkMock2);

            issuesFilter.Verify(x => x.Filter(It.IsAny<IEnumerable<IFilterableIssue>>()), Times.Never);
        }

        [TestMethod]
        public void WhenNewIssuesAreFound_OutOfRangeIssuesAreIgnored_ListenersAreUpdated()
        {
            // Arrange
            var issues = new[]
            {
                new Issue { RuleKey = "S111", StartLine = 1, EndLine = 1 },
                // The next issue is outside the range of the new snapshot and should be ignored
                new Issue { RuleKey = "S222", StartLine = 99999998, EndLine = 99999999 },
                new Issue { RuleKey = "S333", StartLine = 100, EndLine = 101 }
            };

            // Setup up the filter to return whatever was supplied
            SetupIssuesFilter(out var _);

            // Add a couple of error list listeners
            var errorListSinkMock1 = RegisterNewErrorListSink();
            var errorListSinkMock2 = RegisterNewErrorListSink();

            // Sanity check
            testSubject.LastIssues.Should().BeNull();
            testSubject.Factory.CurrentSnapshot.VersionNumber.Should().Be(0);
            testSubject.Factory.CurrentSnapshot.IssueMarkers.Count().Should().Be(0);

            // Act
            ((IIssueConsumer)testSubject).Accept(mockedJavascriptDocumentFooJs.Object.FilePath, issues);

            // Assert
            // We can't check that the editors listeners are notified: we can't mock
            // SnapshotSpan well enough for the product code to work -> affected span
            // is always null so the taggers don't notify their listeners.
            CheckSinkNotified(errorListSinkMock1, 1);
            CheckSinkNotified(errorListSinkMock2, 1);

            testSubject.LastIssues.Should().NotBeNull();
            testSubject.Factory.CurrentSnapshot.VersionNumber.Should().Be(1);
            testSubject.Factory.CurrentSnapshot.IssueMarkers.Count().Should().Be(2);

            var actualMarkers = testSubject.Factory.CurrentSnapshot.IssueMarkers.ToArray();
            actualMarkers[0].Issue.RuleKey.Should().Be("S111");
            actualMarkers[1].Issue.RuleKey.Should().Be("S333");
        }

        [TestMethod]
        public void WhenNewIssuesAreFound_FilterIsApplied_ListenersAreUpdated()
        {
            // Arrange
            var inputIssues = new[]
            {
                new Issue { RuleKey = "S111", StartLine = 1, EndLine = 1 },
                new Issue { RuleKey = "S222", StartLine = 2, EndLine = 2 }
            };

            var issuesToReturnFromFilter = new[]
            {
                new DaemonIssueAdapter(
                    new Issue { RuleKey = "xxx", StartLine = 3 }, "text1", "hash1")
            };

            SetupIssuesFilter(out var issuesPassedToFilter, issuesToReturnFromFilter);

            var errorListSinkMock1 = RegisterNewErrorListSink();

            // Act
            ((IIssueConsumer)testSubject).Accept(mockedJavascriptDocumentFooJs.Object.FilePath, inputIssues);

            // Assert
            // Check the expected issues were passed to the filter
            issuesPassedToFilter.Count.Should().Be(2);
            issuesPassedToFilter[0].RuleId.Should().Be("S111");
            issuesPassedToFilter[1].RuleId.Should().Be("S222");

            CheckSinkNotified(errorListSinkMock1, 1);

            // Check the post-filter issues
            testSubject.Factory.CurrentSnapshot.IssueMarkers.Count().Should().Be(1);
            testSubject.Factory.CurrentSnapshot.IssueMarkers.First().Issue.RuleKey.Should().Be("xxx");
        }

        [TestMethod]
        public void WhenNewIssuesAreFound_AndFilterRemovesAllIssues_ListenersAreUpdated()
        {
            // Arrange
            var inputIssues = new[]
            {
                new Issue { RuleKey = "single issue", StartLine = 1, EndLine = 1 }
            };

            var issuesToReturnFromFilter = Enumerable.Empty<DaemonIssueAdapter>();
            SetupIssuesFilter(out var capturedFilterInput, issuesToReturnFromFilter);

            var errorListSinkMock1 = RegisterNewErrorListSink();

            // Act
            ((IIssueConsumer)testSubject).Accept(mockedJavascriptDocumentFooJs.Object.FilePath, inputIssues);

            // Assert
            // Check the expected issues were passed to the filter
            capturedFilterInput.Count.Should().Be(1);
            capturedFilterInput[0].RuleId.Should().Be("single issue");

            CheckSinkNotified(errorListSinkMock1, 1);

            // Check there are no markers
            testSubject.Factory.CurrentSnapshot.IssueMarkers.Count().Should().Be(0);
        }

        [TestMethod]
        public void WhenNoIssuesAreFound_ListenersAreUpdated()
        {
            // Arrange
            SetupIssuesFilter(out var capturedFilterInput, Enumerable.Empty<IFilterableIssue>());

            var errorListSinkMock1 = RegisterNewErrorListSink();

            // Act
            ((IIssueConsumer)testSubject).Accept(mockedJavascriptDocumentFooJs.Object.FilePath, Enumerable.Empty<Issue>());

            // Assert
            capturedFilterInput.Should().BeEmpty();

            CheckSinkNotified(errorListSinkMock1, 1);

            testSubject.Factory.CurrentSnapshot.IssueMarkers.Count().Should().Be(0);
        }

        private void SetupIssuesFilter(out IList<IFilterableIssue> capturedFilterInput,
            IEnumerable<IFilterableIssue> optionalDataToReturn = null /* if null then the supplied input will be returned */)
        {
            var captured = new List<IFilterableIssue>();
            issuesFilter.Setup(x => x.Filter(It.IsAny<IEnumerable<IFilterableIssue>>()))
                .Callback((IEnumerable<IFilterableIssue> inputIssues) => captured.AddRange(inputIssues))
                .Returns(optionalDataToReturn ?? captured);
            capturedFilterInput = captured;
        }

        private Mock<ITableDataSink> RegisterNewErrorListSink()
        {
            // Adds a new error list sink to the tagger provider.
            // Should be notified when issues change.
            var newSink = new Mock<ITableDataSink>();
            taggerProvider.Subscribe(newSink.Object);
            return newSink;
        }

        private void CheckSinkNotNotified(Mock<ITableDataSink> sinkMock)
        {
            sinkMock.Verify(x => x.FactorySnapshotChanged(It.IsAny<ITableEntriesSnapshotFactory>()), Times.Never);
        }

        private void CheckSinkNotified(Mock<ITableDataSink> sinkMock, int count)
        {
            sinkMock.Verify(x => x.FactorySnapshotChanged(It.IsAny<ITableEntriesSnapshotFactory>()), Times.Exactly(count));
        }

        #endregion

        private TaggerProvider CreateTaggerProvider()
        {
            var tableManagerProviderMock = new Mock<ITableManagerProvider>();
            tableManagerProviderMock.Setup(t => t.GetTableManager(StandardTables.ErrorsTable))
                .Returns(new Mock<ITableManager>().Object);

            var textDocFactoryServiceMock = new Mock<ITextDocumentFactoryService>();

            var contentTypeRegistryServiceMock = new Mock<IContentTypeRegistryService>();
            contentTypeRegistryServiceMock.Setup(c => c.ContentTypes).Returns(Enumerable.Empty<IContentType>());
            var fileExtensionRegistryServiceMock = new Mock<IFileExtensionRegistryService>();
            var languageRecognizer = new SonarLanguageRecognizer(contentTypeRegistryServiceMock.Object, fileExtensionRegistryServiceMock.Object);

            // DTE object setup
            var mockProject = new Mock<Project>();
            mockProject.Setup(p => p.Name).Returns("MyProject");
            var project = mockProject.Object;

            var mockProjectItem = new Mock<ProjectItem>();
            mockProjectItem.Setup(s => s.ContainingProject).Returns(project);
            var projectItem = mockProjectItem.Object;

            var mockSolution = new Mock<Solution>();
            mockSolution.Setup(s => s.FindProjectItem(It.IsAny<string>())).Returns(projectItem);
            var solution = mockSolution.Object;

            var mockDTE = new Mock<DTE>();
            mockDTE.Setup(d => d.Solution).Returns(solution);

            var mockVsStatusBar = new Mock<Microsoft.VisualStudio.Shell.Interop.IVsStatusbar>();

            var serviceProvider = new ConfigurableServiceProvider();
            serviceProvider.RegisterService(typeof(DTE), mockDTE.Object);
            serviceProvider.RegisterService(typeof(Microsoft.VisualStudio.Shell.Interop.IVsStatusbar), mockVsStatusBar.Object);

            var mockAnalysisRequester = new Mock<IAnalysisRequester>();

            var mockAnalysisScheduler = new Mock<IScheduler>();
            mockAnalysisScheduler.Setup(x => x.Schedule(It.IsAny<string>(), It.IsAny<Action<CancellationToken>>()))
                .Callback((string file, Action<CancellationToken> analyze) => analyze(CancellationToken.None));

            var provider = new TaggerProvider(tableManagerProviderMock.Object, textDocFactoryServiceMock.Object, issuesFilter.Object, mockAnalyzerController.Object,
                serviceProvider, languageRecognizer, mockAnalysisRequester.Object, new TestLogger(), mockAnalysisScheduler.Object);
            return provider;
        }

        private static Mock<ITextDocument> CreateDocumentMock(string fileName)
        {
            // Text buffer with a properties collection and current snapshot
            var mockTextBuffer = new Mock<ITextBuffer>();

            var dummyProperties = new PropertyCollection();
            mockTextBuffer.Setup(p => p.Properties).Returns(dummyProperties);

            var mockSnapshot = CreateMockTextSnapshot(1000, "some text");
            mockTextBuffer.Setup(x => x.CurrentSnapshot).Returns(mockSnapshot.Object);

            // Create the document and associate the buffer with the it
            var mockTextDocument = new Mock<ITextDocument>();
            mockTextDocument.Setup(d => d.FilePath).Returns(fileName);
            mockTextDocument.Setup(d => d.Encoding).Returns(Encoding.UTF8);

            mockTextDocument.Setup(x => x.TextBuffer).Returns(mockTextBuffer.Object);

            return mockTextDocument;
        }

        private static Mock<ITextSnapshot> CreateMockTextSnapshot(int lineCount, string textToReturn)
        {
            var mockSnapshotLine = new Mock<ITextSnapshotLine>();
            mockSnapshotLine.Setup(x => x.GetText()).Returns(textToReturn);

            var mockSnapshot = new Mock<ITextSnapshot>();
            mockSnapshot.Setup(x => x.Length).Returns(9999);
            mockSnapshot.Setup(x => x.LineCount).Returns(lineCount);
            mockSnapshot.Setup(x => x.GetLineFromLineNumber(It.IsAny<int>()))
                .Returns(mockSnapshotLine.Object);

            return mockSnapshot;
        }
    }
}
