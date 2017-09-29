using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibraryEx
{

    public class SequencedPair<Obj>
        where Obj : class
    {
        public uint SequenceNumber { get; private set; }
        public Obj RefObject { get; private set; }

        public SequencedPair(uint sequenceNumber, Obj obj) { SequenceNumber = sequenceNumber; RefObject = obj; }
    }

    public interface IRefObjectObeserverViewOfPublisher<Obj>
        where Obj : class
    {
        SequencedPair<Obj> Pair { get; }
    }
    public interface IRefObjectPublisher<Obj> : IRefObjectObeserverViewOfPublisher<Obj>
        where Obj : class
    {
        Obj Object { get; }
    }
    public class RefObjectPublisher<Obj> : IRefObjectPublisher<Obj>
        where Obj : class
    {
        private volatile SequencedPair<Obj> _pair;
        public SequencedPair<Obj> Pair => _pair;
        public Obj Object { get => Pair.RefObject; set => _pair = new SequencedPair<Obj>(null == _pair ? 0 : _pair.SequenceNumber + 1, value); }
    }


    public interface IRefObjectObserver<Obj>
        where Obj : class
    {
        bool IsUpdateRequired { get; }
        bool Update();
        Obj Object { get; }
    }

    public class RefObjectObserver<Obj> : IRefObjectObserver<Obj>
        where Obj : class
    {
        private SequencedPair<Obj> _cachedPair;
        public bool IsUpdateRequired => _cachedPair.SequenceNumber != _publisher.Pair.SequenceNumber;
        public Obj Object => _cachedPair.RefObject;
        public bool Update()
        {
            bool updated = IsUpdateRequired;
            _cachedPair = _publisher.Pair;
            return updated;
        }

        public RefObjectObserver<Obj> Update(out bool updated) { updated = Update(); return this; }

        IRefObjectObeserverViewOfPublisher<Obj> _publisher;
        public RefObjectObserver(IRefObjectPublisher<Obj> publisher)
        {
            _publisher = publisher;
            _cachedPair = _publisher.Pair;
        }
    }
}
