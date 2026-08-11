using HAModHelper.GamePlugin.Gui.Interfaces;
using HAModHelper.GamePlugin.Gui.Systems;
using Xunit;

namespace HAModHelper.Tests
{
    public class PopupManagerTests
    {
        private class FakePopupControl : IPopupControl
        {
            public bool PopupOpen { get; set; }
            public string? LastMessage;
            public bool LastWasYesNo;
            public Action? LastOnYes;
            public Action? LastOnNo;
            public bool HideAllCalled;

            public void ShowMessage(string message)
            {
                LastMessage = message;
                LastWasYesNo = false;
                PopupOpen = true;
            }

            public void ShowYesNo(string message, string yesLabel, string noLabel, Action? onYes, Action? onNo)
            {
                LastMessage = message;
                LastWasYesNo = true;
                LastOnYes = onYes;
                LastOnNo = onNo;
                PopupOpen = true;
            }

            public void HideAll()
            {
                HideAllCalled = true;
                PopupOpen = false;
            }
        }

        public PopupManagerTests()
        {
            PopupManager.Instance.Reset();
        }

        [Fact]
        public void ShowMessageDelegatesToControl()
        {
            var pm = PopupManager.Instance;
            var fake = new FakePopupControl();
            pm.DebugPopupControlSource = fake;

            var result = pm.ShowMessage("hello");

            Assert.True(result);
            Assert.Equal("hello", fake.LastMessage);
            Assert.False(fake.LastWasYesNo);
        }

        [Fact]
        public void ShowMessageReturnsFalseWhenControlUnavailable()
        {
            var pm = PopupManager.Instance;
            pm.DebugPopupControlSource = new DebugNoLoadPopupControl();

            var result = pm.ShowMessage("hello");

            Assert.False(result);
        }

        [Fact]
        public void ShowYesNoWiresUpCallbacks()
        {
            var pm = PopupManager.Instance;
            var fake = new FakePopupControl();
            pm.DebugPopupControlSource = fake;

            var yesCalled = false;
            var noCalled = false;

            pm.ShowYesNo("Are you sure?", "Yes", "No", () => yesCalled = true, () => noCalled = true);

            Assert.True(fake.LastWasYesNo);
            Assert.Equal("Are you sure?", fake.LastMessage);

            fake.LastOnYes?.Invoke();
            Assert.True(yesCalled);
            Assert.False(noCalled);

            fake.LastOnNo?.Invoke();
            Assert.True(noCalled);
        }

        [Fact]
        public void HideAllDelegatesToControl()
        {
            var pm = PopupManager.Instance;
            var fake = new FakePopupControl();
            pm.DebugPopupControlSource = fake;

            var result = pm.HideAll();

            Assert.True(result);
            Assert.True(fake.HideAllCalled);
        }
    }
}
