using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AppleNotepad {
 internal static class TrafficVerification {
  static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
  internal static void Run(Window window,Button[] buttons,Action<string> capture) {
   IInputElement previous=Keyboard.FocusedElement;
   try {
    window.Activate();Keyboard.ClearFocus();window.UpdateLayout();Pump();
    for(int i=0;i<buttons.Length;i++) {
     var button=buttons[i];button.ApplyTemplate();var symbol=(ContentPresenter)button.Template.FindName("Symbol",button);
     if(symbol==null||!(button.Content is System.Windows.Shapes.Path))throw new Exception("Caption button is missing vector symbol");
     bool hover=button.Template.Triggers.OfType<Trigger>().Any(t=>t.Property==UIElement.IsMouseOverProperty&&Object.Equals(t.Value,true)&&t.Setters.OfType<Setter>().Any(s=>s.TargetName=="Symbol"&&s.Property==UIElement.OpacityProperty&&Convert.ToDouble(s.Value)>0));
     if(!hover)throw new Exception("Caption symbol must be configured to appear on hover");
     Keyboard.ClearFocus();Pump();if(!button.IsMouseOver&&symbol.Opacity!=0)throw new Exception("Caption symbol must be hidden in idle state");
     Keyboard.Focus(button);Pump();if(!button.IsKeyboardFocused||symbol.Opacity<=0)throw new Exception("Caption symbol must appear for keyboard focus");
     capture(new[]{"caption-close.png","caption-minimize.png","caption-maximize.png"}[i]);
     Keyboard.ClearFocus();Pump();if(!button.IsMouseOver&&symbol.Opacity!=0)throw new Exception("Caption symbol must hide when inactive");
    }
   }finally{if(previous!=null)Keyboard.Focus(previous);else Keyboard.ClearFocus();Pump();}
  }
 }
}
