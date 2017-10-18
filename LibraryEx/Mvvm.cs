using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LibraryEx
{
    /// <summary>
    /// Interface to be used by the View/UI side
    /// </summary>
    public interface IBindingObject
    {
        void UpdateUI();
    }

    /// <summary>
    /// Interface to be used by the ViewModel Classes
    /// </summary>
    public interface IModelObserver
    {
        IRefObjectObserver MO { get; }
        bool IsModelUpdated { get; }
        bool UpdateModel();
    }

    public interface IModelObserverForVM
    {
        ICommandProxy CreateBindingObjects(Dictionary<string, IRefObjectObserver> models);
        ICommandProxy GetChangedItemList();
    }

    #region ViewModel classes
    /// <summary>
    /// This class is used to help the UI thread in passing ONLY the objects that need to be updated.
    /// </summary>
    public class ModelObserverForVM : IModelObserverForVM
    {
        private ActiveObjectPart _aop;
        private KeyValuePair<string, IModelObserver>[] _bindingObjects = new KeyValuePair<string, IModelObserver>[0];
        private HashSet<string> _changedItems = new HashSet<string>();

        #region Datatypes

        public class BindingObjectCollection : IOutputParams { public Dictionary<string, IBindingObject> Collection { get; set; } }
        public class ChangedItemsParams : IOutputParams { public string[] ChangedItems { get; set; } }

        #endregion

        #region Constructor and Destructor

        public ModelObserverForVM()
        {
            _aop = new ActiveObjectPart("ModelObserverForUi") { ServiceFunc = Poll };
            _aop.Initialize();
        }

        #endregion

        private void Poll()
        {
            foreach (var bindingObject in _bindingObjects)
            {
                if (!bindingObject.Value.IsModelUpdated) continue;
                bindingObject.Value.UpdateModel();
                _changedItems.Add(bindingObject.Key);
            }
        }

        public ICommandProxy CreateBindingObjects(Dictionary<string, IRefObjectObserver> models) => _aop.CreateCommand("CreateBindingObjects", x => PerformCreateBindingObjects(x, models));
        private string PerformCreateBindingObjects(ICommandParams cmdParams, Dictionary<string, IRefObjectObserver> models)
        {
            _bindingObjects = models.Select(x => new KeyValuePair<string, IModelObserver>(x.Key, BindingObjectFactory.CreateModelObserver(x.Value))).ToArray();
            cmdParams.SetOutputParams(new BindingObjectCollection() { Collection = _bindingObjects.ToDictionary(x => x.Key, y => y.Value as IBindingObject) });
            return string.Empty;
        }

        public ICommandProxy GetChangedItemList() => _aop.CreateCommand("GetChangedItemList", x => PerformGetChangedItemList(x));
        private string PerformGetChangedItemList(ICommandParams commandParams)
        {
            commandParams.SetOutputParams(new ChangedItemsParams() { ChangedItems = _changedItems.ToArray() });
            _changedItems.Clear();
            return string.Empty;
        }
    }

    public static class BindingObjectFactory
    {
        #region Datatypes

        /// <summary>
        /// Class used by two classes namely:
        /// 1. VM(ModelObserverForVM to apply data update from Model)
        /// 2. View(UIThread to update the NotifyPropertyChanged event for the changes reported by VM).
        /// Note: The xaml binds to the MO.Object (where Object is recognized in the implementation class of IRefObjectObserver)
        /// </summary>
        private class BindingObject : INotifyPropertyChanged, IBindingObject, IModelObserver
        {
            #region INotifyPropertyChanged

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnNotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            #endregion

            #region IBindingObject

            public void UpdateUI() => OnNotifyPropertyChanged(nameof(MO));

            #endregion

            #region IModelObserver

            public IRefObjectObserver MO { get; set; }
            public bool IsModelUpdated => MO.IsUpdateRequired;
            public bool UpdateModel() => MO.Update();

            #endregion
        }

        #endregion

        public static IModelObserver CreateModelObserver(IRefObjectObserver modelData) => new BindingObject() { MO = modelData };
    }
    #endregion

    #region Model Class

    /// <summary>
    /// Developer Usage Caveat:
    /// Assumes the 'AddModelObserverToCollection' will be done at startup ONLY(by the model classes) followed by call to 'Models' property by the VM to get the whole collection.
    /// Calls to this class for 'Models' and 'AddModelObserverToCollection' is NOT INTENDED to be invoked in PARALLEL. 
    /// </summary>
    public static class ModelCollection
    {
        private static object _lockObj = new object();
        public static Dictionary<string, IRefObjectObserver> Models { get; private set; } = new Dictionary<string, IRefObjectObserver>();
        public static void AddModelObserverToCollection(Dictionary<string, IRefObjectObserver> items)
        {
            lock (_lockObj)
            {
                foreach (var item in items) { Models[item.Key] = item.Value; }
            }
        }
    }
    #endregion
}
