using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LibraryEx
{
    public class CtrlExtn : DependencyObject
    {
        #region Public Constants

        public static string ModuleName => nameof(ModuleName);
        public static string CommandName => nameof(CommandName);
        public static string Parameters => nameof(Parameters);
        public static string KeyName => nameof(KeyName);
        public static string KeyValue => nameof(KeyValue);
        public static string DisableReason => nameof(DisableReason);

        #endregion

        public static readonly DependencyProperty ModuleNameProperty = DependencyProperty.RegisterAttached(ModuleName, typeof(string), typeof(CtrlExtn), new PropertyMetadata(string.Empty, OnInit));
        public static string GetModuleName(DependencyObject d) => (string)d.GetValue(ModuleNameProperty);
        public static void SetModuleName(DependencyObject d, string value) => d.SetValue(ModuleNameProperty, value);

        public static readonly DependencyProperty CommandNameProperty = DependencyProperty.RegisterAttached(CommandName, typeof(string), typeof(CtrlExtn), new PropertyMetadata(string.Empty));
        public static string GetCommandName(DependencyObject d) => (string)d.GetValue(CommandNameProperty);
        public static void SetCommandName(DependencyObject d, string value) => d.SetValue(CommandNameProperty, value);

        public static readonly DependencyProperty KeyNameProperty = DependencyProperty.RegisterAttached(KeyName, typeof(string), typeof(CtrlExtn), new PropertyMetadata(string.Empty));
        public static string GetKeyName(DependencyObject d) => (string)d.GetValue(KeyNameProperty);
        public static void SetKeyName(DependencyObject d, string value) => d.SetValue(KeyNameProperty, value);

        public static readonly DependencyProperty KeyValueProperty = DependencyProperty.RegisterAttached(KeyValue, typeof(string), typeof(CtrlExtn), new PropertyMetadata(string.Empty));
        public static string GetKeyValue(DependencyObject d) => (string)d.GetValue(KeyValueProperty);
        public static void SetKeyValue(DependencyObject d, string value) => d.SetValue(KeyValueProperty, value);

        public static readonly DependencyProperty DisableReasonProperty = DependencyProperty.RegisterAttached(DisableReason, typeof(string), typeof(CtrlExtn), new PropertyMetadata(string.Empty, OnDisablePropertySet));
        public static string GetDisableReason(DependencyObject d) => (string)d.GetValue(DisableReasonProperty);
        public static void SetDisableReason(DependencyObject d, string value) => d.SetValue(DisableReasonProperty, value);

        private static void OnDisablePropertySet(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var f = d as FrameworkElement;
            if (f == null) return;
            string disableReason = GetDisableReason(d);
            if (disableReason.IsNullOrEmpty())
            {
                f.IsEnabled = true;
                f.ToolTip = null;
            }
            else
            {
                f.IsEnabled = false;
                f.ToolTip = disableReason;
            }
        }

        /// <summary>
        /// Subscribed only by Module Name UNDER THE ASSUMPTION that this property is always PRESENT for any command to be sent to the models via the Dispatcher
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnInit(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d == null) return;
            System.Windows.Controls.ToolTipService.SetShowOnDisabled(d, true);
            switch (d)
            {
                case System.Windows.Controls.Primitives.ButtonBase b:
                    {
                        b.Initialized -= ButtonBase_Initialized;
                        b.Initialized += ButtonBase_Initialized;
                    }
                    break;
                case System.Windows.Controls.TextBox t:
                    {
                        t.Initialized -= TextBox_Initialized;
                        t.Initialized += TextBox_Initialized;
                    }
                    break;
                default: return;
            }
            return;
        }

        private static void ButtonBase_Initialized(object sender, EventArgs e)
        {
            var b = sender as System.Windows.Controls.Primitives.ButtonBase;
            b.CommandParameter = CreateCommandParameter(b);
            if (!GetKeyName(b).IsNullOrEmpty()) { b.Click += OnButtonBaseClicked; }
        }

        private static void OnButtonBaseClicked(object sender, RoutedEventArgs e)
        {
            var b = sender as System.Windows.Controls.Primitives.ButtonBase;
            ((b.CommandParameter as Dictionary<string, object>)[Parameters] as Dictionary<string, object>)[GetKeyName(b)] = GetKeyValue(b);
        }

        private static void TextBox_Initialized(object sender, EventArgs e)
        {
            var t = sender as System.Windows.Controls.TextBox;
            t.InputBindings[0].CommandParameter = CreateCommandParameter(t);
            if (!GetKeyName(t).IsNullOrEmpty()) { t.PreviewKeyDown += OnTextBoxKeyUp; }
        }

        private static void OnTextBoxKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) { return; }
            var t = sender as System.Windows.Controls.TextBox;
            ((t.InputBindings[0].CommandParameter as Dictionary<string, object>)[Parameters] as Dictionary<string, object>)[GetKeyName(t)] = GetKeyValue(t);
        }

        private static Dictionary<string, object> CreateCommandParameter(DependencyObject d) => new Dictionary<string, object>() { { ModuleName, GetModuleName(d) },
                                                                                                                                   { CommandName, GetCommandName(d) },
                                                                                                                                   { Parameters, new Dictionary<string, object>() } };
    }
}
