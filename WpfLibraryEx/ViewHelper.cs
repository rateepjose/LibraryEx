using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LibraryEx
{
    public class ViewHelper
    {
        private DispatcherTimer _dispatcherTimer;
        private Window _windowMain;
        public Action OnUiUpdate { get; set; } = null;
        public ViewHelper(Window windowMain, TimeSpan? uiUpdateInterval = null)
        {
            _windowMain = windowMain;
            _dispatcherTimer = new DispatcherTimer(uiUpdateInterval ?? TimeSpan.FromMilliseconds(333), DispatcherPriority.Background, OnTimer, windowMain.Dispatcher);
            _dispatcherTimer.Start();
        }

        private ICommandStatus _icsCreateBindingObjects = null;
        private Dictionary<string, IBindingObject> _bindingObjects = null;
        public IModelObserverForVM ViewModel;
        public ViewHelper Initialize(Dictionary<string, IRefObjectObserver> models)
        {
            _initialized = false;
            _icsCreateBindingObjects = ViewModel.CreateBindingObjects(models).Start();
            return this;
        }

        public IBindingObject this[string key] => _bindingObjects.TryGetValue(key, out IBindingObject data) ? data : BindingObjectFactory.DefaultBindingObject;

        private bool _initialized = false;
        private bool CheckIfFullyInitialized()
        {
            if (_initialized) return true;
            if (_icsCreateBindingObjects == null) return false;
            if (!_icsCreateBindingObjects.IsComplete) return false;
            _bindingObjects = (_icsCreateBindingObjects.OutputParams as ModelObserverForVM.BindingObjectCollection).Collection;
            _windowMain.DataContext = null;//Added to since we are binding to the same object and DataContext populates only on change of object
            _windowMain.DataContext = this;
            _initialized = true;
            return true;
        }

        ICommandStatus _icsChangedItemsList = null;
        private void OnTimer(object sender, EventArgs e)
        {
            if (!CheckIfFullyInitialized()) return;

            OnUiUpdate?.Invoke();

            //Binding update happens here{{{
            if (_icsChangedItemsList == null) { _icsChangedItemsList = ViewModel.GetChangedItemList().Start(); }
            if (_icsChangedItemsList.IsComplete) { UpdateUiBindings((_icsChangedItemsList.OutputParams as ModelObserverForVM.ChangedItemsParams).ChangedItems); _icsChangedItemsList = null; }
            //Binding update happens here}}}
        }

        private void UpdateUiBindings(string[] changedItems)
        {
            foreach (var item in changedItems) { _bindingObjects[item].UpdateUI(); }
        }

        public System.Windows.Input.ICommand Command { get; set; }
    }
}
