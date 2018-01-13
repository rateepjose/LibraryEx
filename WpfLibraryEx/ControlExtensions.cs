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
        public static string ParamName => nameof(ParamName);
        public static string ParamValue => nameof(ParamValue);
        public static string DisableReason => nameof(DisableReason);

        #endregion

        public static readonly DependencyProperty ModuleNameProperty = DependencyProperty.RegisterAttached(ModuleName, typeof(string), typeof(CtrlExtn), new PropertyMetadata(string.Empty, OnInit));
        public static string GetModuleName(DependencyObject d) => (string)d.GetValue(ModuleNameProperty);
        public static void SetModuleName(DependencyObject d, string value) => d.SetValue(ModuleNameProperty, value);

        public static readonly DependencyProperty CommandNameProperty = DependencyProperty.RegisterAttached(CommandName, typeof(string), typeof(CtrlExtn), new PropertyMetadata(string.Empty));
        public static string GetCommandName(DependencyObject d) => (string)d.GetValue(CommandNameProperty);
        public static void SetCommandName(DependencyObject d, string value) => d.SetValue(CommandNameProperty, value);

        public static readonly DependencyProperty ParamNameProperty = DependencyProperty.RegisterAttached(ParamName, typeof(string), typeof(CtrlExtn), new PropertyMetadata(string.Empty));
        public static string GetParamName(DependencyObject d) => (string)d.GetValue(ParamNameProperty);
        public static void SetParamName(DependencyObject d, string value) => d.SetValue(ParamNameProperty, value);

        public static readonly DependencyProperty ParamValueProperty = DependencyProperty.RegisterAttached(ParamValue, typeof(object), typeof(CtrlExtn), new PropertyMetadata(null));
        public static object GetParamValue(DependencyObject d) => (object)d.GetValue(ParamValueProperty);
        public static void SetParamValue(DependencyObject d, object value) => d.SetValue(ParamValueProperty, value);

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

        #region Button Events

        private static void ButtonBase_Initialized(object sender, EventArgs e)
        {
            var b = sender as System.Windows.Controls.Primitives.ButtonBase;
            if (!GetModuleName(b).IsNullOrEmpty()) { b.Click += OnButtonBaseClicked; }
        }
        private static void OnButtonBaseClicked(object sender, RoutedEventArgs e)
        {
            var b = sender as System.Windows.Controls.Primitives.ButtonBase;
            CommandDispatcher.CmdDispatchMgr.DispatchCommand(GetModuleName(b), GetCommandName(b), new Dictionary<string, object>() { { GetParamName(b), GetParamValue(b) } }).Start();
            Logging.Logger.Debug.WriteLine("ButtonClicked");
        }

        #endregion

        #region Textbox Events

        private static void TextBox_Initialized(object sender, EventArgs e)
        {
            var t = sender as System.Windows.Controls.TextBox;
            if (!GetParamName(t).IsNullOrEmpty()) { t.PreviewKeyDown += OnTextBoxKeyUp; }
        }
        private static void OnTextBoxKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) { return; }
            var t = sender as System.Windows.Controls.TextBox;
            CommandDispatcher.CmdDispatchMgr.DispatchCommand(GetModuleName(t), GetCommandName(t), new Dictionary<string, object>() { { GetParamName(t), GetParamValue(t) } }).Start();
        }

        #endregion
    }
}
