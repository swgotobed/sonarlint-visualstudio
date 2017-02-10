/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using FluentAssertions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.VisualStudio.Progress.Controller.ErrorNotification;

namespace SonarLint.VisualStudio.Progress.UnitTests
{
    /// <summary>
    /// Tests for <see cref="VsOutputWindowPaneNotifier"/>
    /// </summary>
    [TestClass]
    public class VsOutputWindowPaneNotifierTests
    {
        private ConfigurableServiceProvider serviceProvider;
        private Exception expectedException;

        #region Test plumbing

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            this.serviceProvider = new ConfigurableServiceProvider();
            this.serviceProvider.RegisterService(typeof(SVsTaskSchedulerService), new SingleThreadedTaskSchedulerService());

            this.expectedException = new Exception(this.TestContext.TestName, new Exception(Environment.TickCount.ToString()));
        }

        #endregion Test plumbing

        #region Tests

        [TestMethod]
        [Description("Arg check tests")]
        public void VsOutputWindowPaneNotifier_Args()
        {
            // Arrange
            var outputWindowPane = this.CreateOutputPane(true);

            // Act + Assert
            // Test case: 1st argument invalid
            Exceptions.Expect<ArgumentNullException>(() => new VsOutputWindowPaneNotifier(null, outputWindowPane, true, "{0}", false));

            // Test case: 2nd argument invalid
            Exceptions.Expect<ArgumentNullException>(() => new VsOutputWindowPaneNotifier(this.serviceProvider, null, true, "{0}", false));

            // Test case: 4th argument invalid
            Exceptions.Expect<ArgumentNullException>(() => new VsOutputWindowPaneNotifier(this.serviceProvider, outputWindowPane, false, null, false));
            Exceptions.Expect<ArgumentNullException>(() => new VsOutputWindowPaneNotifier(this.serviceProvider, outputWindowPane, false, string.Empty, false));
            Exceptions.Expect<ArgumentNullException>(() => new VsOutputWindowPaneNotifier(this.serviceProvider, outputWindowPane, false, " \t", false));

            // Test case: All valid
            new VsOutputWindowPaneNotifier(this.serviceProvider, outputWindowPane, true, "{0}", false);
            new VsOutputWindowPaneNotifier(this.serviceProvider, outputWindowPane, false, "{0}", true);
        }

        [TestMethod]
        [Description("Verifies notifying of an exception message using an output window")]
        public void VsOutputWindowPaneNotifier_MessageOnly()
        {
            // Arrange
            bool logFullException = true;
            bool ensureOutputVisible = false;

            StubVsOutputWindowPane outputPane = this.CreateOutputPane(logFullException);
            StubVsUIShell.StubWindowFrame frame = this.CreateAndRegisterFrame();

            IProgressErrorNotifier testSubject = this.CreateTestSubject(outputPane, ensureOutputVisible, logFullException);

            // Act
            testSubject.Notify(this.expectedException);

            // Assert
            frame.WasShown.Should().BeFalse();
            outputPane.IsActivated.Should().BeFalse();
            outputPane.IsWrittenToOutputWindow.Should().BeTrue();
        }

        [TestMethod]
        [Description("Verifies notifying of a full exception using an output window")]
        public void VsOutputWindowPaneNotifier_FullException()
        {
            // Arrange
            bool logFullException = true;
            bool ensureOutputVisible = false;

            StubVsOutputWindowPane outputPane = this.CreateOutputPane(logFullException);
            StubVsUIShell.StubWindowFrame frame = this.CreateAndRegisterFrame();

            IProgressErrorNotifier testSubject = this.CreateTestSubject(outputPane, ensureOutputVisible, logFullException);

            // Act
            testSubject.Notify(this.expectedException);

            // Assert
            frame.WasShown.Should().BeFalse();
            outputPane.IsActivated.Should().BeFalse();
            outputPane.IsWrittenToOutputWindow.Should().BeTrue();
        }

        [TestMethod]
        [Description("Verifies notifying of a full exception using an output window and ensure that the output is visible")]
        public void VsOutputWindowPaneNotifier_EnsureOutputVisible()
        {
            // Arrange
            bool logFullException = true;
            bool ensureOutputVisible = true;

            StubVsOutputWindowPane outputPane = this.CreateOutputPane(logFullException);
            StubVsUIShell.StubWindowFrame frame = this.CreateAndRegisterFrame();

            IProgressErrorNotifier testSubject = this.CreateTestSubject(outputPane, ensureOutputVisible, logFullException);

            // Act
            testSubject.Notify(this.expectedException);

            // Assert
            frame.WasShown.Should().BeTrue();
            outputPane.IsActivated.Should().BeTrue();
            outputPane.IsWrittenToOutputWindow.Should().BeTrue();
        }

        #endregion Tests

        #region Helpers

        private string CreateTestMessageFormat()
        {
            return this.TestContext.TestName + "{0}";
        }

        private VsOutputWindowPaneNotifier CreateTestSubject(IVsOutputWindowPane pane, bool ensureOutputVisible, bool logFullException)
        {
            return new VsOutputWindowPaneNotifier(this.serviceProvider, pane, ensureOutputVisible, this.CreateTestMessageFormat(), logFullException);
        }

        private StubVsUIShell.StubWindowFrame CreateAndRegisterFrame()
        {
            var frame = new StubVsUIShell.StubWindowFrame();

            var uiShell = new StubVsUIShell
            {
                FindToolWindowAction = (windowSlotGuid) =>
                {
                    windowSlotGuid.Should().Be(VSConstants.StandardToolWindows.Output, "Unexpected window slot guid");
                    return frame;
                }
            };

            this.serviceProvider.RegisterService(typeof(SVsUIShell), uiShell);

            return frame;
        }

        private StubVsOutputWindowPane CreateOutputPane(bool logFullException)
        {
            return new StubVsOutputWindowPane
            {
                OutputStringThreadSafeAction = (actualMessage) =>
                {
                    string expectedFormat = this.CreateTestMessageFormat() + Environment.NewLine;
                    MessageVerificationHelper.VerifyNotificationMessage(actualMessage, expectedFormat, this.expectedException, logFullException);
                }
            };
        }

        #endregion Helpers
    }
}