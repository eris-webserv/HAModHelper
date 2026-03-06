using HAModHelper.GamePlugin.Base.Events;
using Xunit;

namespace HAModHelper.Tests
{
    public class EventBusTests
    {
        private class DummyEvent1 : BaseEvent { }
        private class DummyEvent2 : BaseEvent { }
        private class DummyEvent3 : BaseEvent { }
        private class DummyEvent4 : BaseEvent { }
        private class DummyEvent5 : BaseEvent { }
        private class DummyEvent7 : BaseEvent { }
        private class DummyEvent6 : BaseEvent
        {
            public string Wawa { get; set; }

            public DummyEvent6(string wawa)
            {
                Wawa = wawa;
            }
        }


        [Fact]
        public void NoSubscribers_FiredReturnsFalse()
        {
            var bus = EventBus.Instance;
            var ev = new DummyEvent1();
            var result = bus.Fire(ev);
            Assert.False(result.Fired);
        }

        [Fact]
        public void Firing_WithHandler_Invokes()
        {
            var bus = EventBus.Instance;
            bool called = false;
            bus.Subscribe<DummyEvent2>(e =>
            {
                called = true;
                e.Handled = true;
            });

            var ev = new DummyEvent2();
            var result = bus.Fire(ev);
            Assert.NotNull(result);
            Assert.True(called);
            Assert.True(ev.Handled);
        }

        [Fact]
        public void MultipleSubscribers_AreAllCalled()
        {
            var bus = EventBus.Instance;
            int count = 0;
            bus.Subscribe<DummyEvent3>(_ => count++);
            bus.Subscribe<DummyEvent3>(_ => count++);

            var ev = new DummyEvent3();
            bus.Fire(ev);
            Assert.Equal(2, count);
        }

        [Fact]
        public void Unsubscribe_RemovesHandler()
        {
            var bus = EventBus.Instance;
            int count = 0;
            var sub = bus.Subscribe<DummyEvent4>(_ => count++);

            bus.Fire(new DummyEvent4());
            Assert.Equal(1, count);
            sub.Dispose();
            bus.Fire(new DummyEvent4());
            Assert.Equal(1, count);
        }

        [Fact]
        public void FiringSameInstance_Twice_Throws()
        {
            var bus = EventBus.Instance;
            bus.Subscribe<DummyEvent5>(_ => { });
            var ev = new DummyEvent5();
            bus.Fire(ev);
            Assert.Throws<Exception>(() => bus.Fire(ev));
        }

        [Fact]
        public void EventBusInstanceIdentical()
        {
            var bus = EventBus.Instance;
            var alsobus = EventBus.Instance;

            bus.Subscribe<DummyEvent7>(_ => { });
            
            Assert.Equal(bus, alsobus);

            var ev = new DummyEvent7();
            var result = bus.Fire(ev);
            Assert.True(result.Fired);
        }

        [Fact]
        public void EventArgsHandled()
        {
            var bus = EventBus.Instance;
            bus.Subscribe<DummyEvent6>(e =>
            {
                e.Wawa = "awawa";
            });

            var ev = new DummyEvent6(wawa: "awawa");
            var result = bus.Fire(ev);
            Assert.Equal("awawa", ev.Wawa);
        }
    }
}